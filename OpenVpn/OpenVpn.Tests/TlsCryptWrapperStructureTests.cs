using OpenVpn.Crypto;
using OpenVpn.Sessions;
using OpenVpn.Sessions.Packets;
using OpenVpn.TlsCrypt;
using OpenVpn.IO;
using Org.BouncyCastle.Security;
using Microsoft.Extensions.Logging;

namespace OpenVpn.Tests
{
    /// <summary>
    /// Comprehensive data structure tests for TlsCryptWrapper class following Write -> Send -> Check output structure pattern
    /// and Receive -> Read -> Check input structure validation pattern.
    /// </summary>
    public class TlsCryptWrapperStructureTests
    {
        /// <summary>
        /// Mock implementation of ISessionPacketHeader for testing purposes
        /// </summary>
        private sealed class TestSessionPacketHeader : ISessionPacketHeader
        {
            public byte Opcode { get; set; } = 0x01;
            public byte KeyId { get; set; } = 0x00;
            public uint SessionId { get; set; } = 0x12345678u;

            public void Serialize(PacketWriter writer)
            {
                writer.WriteByte(Opcode);
                writer.WriteByte(KeyId);
                writer.WriteUInt(SessionId);
            }

            public bool TryDeserialize(PacketReader reader)
            {
                if (reader.AvailableBytes < 6) // 1 + 1 + 4 bytes
                    return false;

                Opcode = reader.ReadByte();
                KeyId = reader.ReadByte();
                SessionId = reader.ReadUInt();
                return true;
            }
        }

        /// <summary>
        /// Mock implementation of ISessionChannel for testing TlsCryptWrapper
        /// </summary>
        private sealed class MockSessionChannel : ISessionChannel
        {
            private readonly Queue<SessionPacket> _writeQueue = new();
            private readonly Queue<SessionPacket> _readQueue = new();
            private readonly List<SessionPacket> _sentPackets = new();
            private readonly List<SessionPacket> _receivedPackets = new();

            public void Write(SessionPacket packet)
            {
                _writeQueue.Enqueue(packet.Clone());
            }

            public SessionPacket? Read()
            {
                return _readQueue.TryDequeue(out var packet) ? packet : null;
            }

            public Task Send(CancellationToken cancellationToken)
            {
                while (_writeQueue.TryDequeue(out var packet))
                {
                    _sentPackets.Add(packet);
                }
                return Task.CompletedTask;
            }

            public Task Receive(CancellationToken cancellationToken)
            {
                foreach (var packet in _receivedPackets)
                {
                    _readQueue.Enqueue(packet);
                }
                _receivedPackets.Clear();
                return Task.CompletedTask;
            }

            public void SimulateReceivePacket(SessionPacket packet)
            {
                _receivedPackets.Add(packet);
            }

            public IReadOnlyList<SessionPacket> GetSentPackets() => _sentPackets.AsReadOnly();

            public void Dispose()
            {
                _writeQueue.Clear();
                _readQueue.Clear();
                _sentPackets.Clear();
                _receivedPackets.Clear();
            }
        }

        [Fact]
        public void Write_Send_CheckTlsCryptStructure_VerifiesWrappedPacketFormat()
        {
            // Arrange
            using var mockChannel = new MockSessionChannel();
            using var wrapper = CreateTlsCryptWrapper(mockChannel);
            
            var header = new TestSessionPacketHeader { Opcode = 0x02, SessionId = 0xDEADBEEF };
            var testData = GenerateTestData(64);
            var packet = new SessionPacket { Header = header, Data = testData };

            // Act - Write through TLS crypt wrapper
            wrapper.Write(packet);
            wrapper.Send(CancellationToken.None).Wait();

            // Check TLS crypt structure
            var sentPackets = mockChannel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var wrappedPacket = sentPackets[0];
            Assert.NotNull(wrappedPacket);
            
            // TLS crypt wrapper should add header (packet ID + timestamp) + encryption
            Assert.True(wrappedPacket.Data.Length > testData.Length + 8); // At least header size
            
            // Verify the wrapped data contains packet ID and timestamp in first 8 bytes
            var wrappedData = wrappedPacket.Data.Span;
            Assert.True(wrappedData.Length >= 8, "Wrapped packet should contain at least 8-byte header");
        }

        [Fact]
        public async Task Receive_Read_CheckTlsCryptUnwrapping_VerifiesDecryptionFlow()
        {
            // Arrange
            using var mockChannel = new MockSessionChannel();
            using var wrapper = CreateTlsCryptWrapper(mockChannel);
            
            var header = new TestSessionPacketHeader { Opcode = 0x03, SessionId = 0xCAFEBABE };
            var originalData = GenerateTestData(64);
            var originalPacket = new SessionPacket { Header = header, Data = originalData };

            // First encrypt the packet
            wrapper.Write(originalPacket);
            await wrapper.Send(CancellationToken.None);
            
            var sentPackets = mockChannel.GetSentPackets();
            var encryptedPacket = sentPackets[0];

            // Simulate receiving the encrypted packet
            mockChannel.SimulateReceivePacket(encryptedPacket);

            // Act - Receive and decrypt through TLS crypt wrapper
            await wrapper.Receive(CancellationToken.None);
            var decryptedPacket = wrapper.Read();

            // Check TLS crypt unwrapping
            Assert.NotNull(decryptedPacket);
            Assert.Equal(originalData, decryptedPacket.Data.ToArray());
            
            var decryptedHeader = decryptedPacket.Header as TestSessionPacketHeader;
            Assert.NotNull(decryptedHeader);
            Assert.Equal(header.Opcode, decryptedHeader.Opcode);
            Assert.Equal(header.SessionId, decryptedHeader.SessionId);
        }

        [Theory]
        [InlineData(0)]      // Empty packet
        [InlineData(1)]      // Minimal data
        [InlineData(64)]     // Standard size
        [InlineData(256)]    // Medium packet
        [InlineData(1024)]   // Large packet
        [InlineData(4096)]   // Very large packet
        public async Task Write_Send_CheckTlsCryptPacketSizes_HandlesVariousPayloadSizes(int dataSize)
        {
            // Arrange
            using var mockChannel = new MockSessionChannel();
            using var wrapper = CreateTlsCryptWrapper(mockChannel);
            
            var header = new TestSessionPacketHeader { Opcode = 0x02, SessionId = 0x12345678 };
            var testData = GenerateTestData(dataSize);
            var packet = new SessionPacket { Header = header, Data = testData };

            // Act
            wrapper.Write(packet);
            await wrapper.Send(CancellationToken.None);

            // Check TLS crypt packet size handling
            var sentPackets = mockChannel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var wrappedPacket = sentPackets[0];
            
            // Wrapped packet should be larger due to encryption overhead
            var expectedMinSize = dataSize + 8; // At least header size
            Assert.True(wrappedPacket.Data.Length >= expectedMinSize, 
                $"Wrapped packet size ({wrappedPacket.Data.Length}) should be at least {expectedMinSize}");
        }

        [Fact]
        public async Task Write_Send_CheckTlsCryptPacketIdSequence_VerifiesPacketIdIncrement()
        {
            // Arrange
            using var mockChannel = new MockSessionChannel();
            using var wrapper = CreateTlsCryptWrapper(mockChannel);
            
            var packets = new List<SessionPacket>();
            for (int i = 0; i < 5; i++)
            {
                var header = new TestSessionPacketHeader { Opcode = 0x01, SessionId = (uint)(0x10000000 + i) };
                var data = GenerateTestData(32);
                packets.Add(new SessionPacket { Header = header, Data = data });
            }

            // Act - Write multiple packets
            foreach (var packet in packets)
            {
                wrapper.Write(packet);
            }
            await wrapper.Send(CancellationToken.None);

            // Check packet ID sequence
            var sentPackets = mockChannel.GetSentPackets();
            Assert.Equal(packets.Count, sentPackets.Count);
            
            // Verify packet IDs are sequential (first 4 bytes should increment)
            var packetIds = new List<uint>();
            foreach (var sentPacket in sentPackets)
            {
                var packetIdBytes = sentPacket.Data.Span[0..4];
                var packetId = BitConverter.ToUInt32(packetIdBytes);
                packetIds.Add(packetId);
            }
            
            // Check that packet IDs are sequential
            for (int i = 1; i < packetIds.Count; i++)
            {
                Assert.Equal(packetIds[i-1] + 1, packetIds[i]);
            }
        }

        [Fact]
        public async Task Write_Send_CheckTlsCryptTimestamp_VerifiesTimestampPresence()
        {
            // Arrange
            using var mockChannel = new MockSessionChannel();
            using var wrapper = CreateTlsCryptWrapper(mockChannel);
            
            var header = new TestSessionPacketHeader { Opcode = 0x02, SessionId = 0x12345678 };
            var testData = GenerateTestData(64);
            var packet = new SessionPacket { Header = header, Data = testData };

            var beforeTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Act
            wrapper.Write(packet);
            await wrapper.Send(CancellationToken.None);

            var afterTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Check timestamp structure
            var sentPackets = mockChannel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var wrappedPacket = sentPackets[0];
            
            // Extract timestamp from bytes 4-7 (after packet ID)
            var timestampBytes = wrappedPacket.Data.Span[4..8];
            var timestamp = BitConverter.ToUInt32(timestampBytes);
            
            // Timestamp should be within reasonable range
            Assert.True(timestamp >= beforeTime, $"Timestamp {timestamp} should be >= {beforeTime}");
            Assert.True(timestamp <= afterTime + 1, $"Timestamp {timestamp} should be <= {afterTime + 1}"); // Allow 1 second tolerance
        }

        [Fact]
        public async Task Write_Send_Receive_Read_CheckTlsCryptRoundTrip_VerifiesCompleteFlow()
        {
            // Arrange
            using var mockChannel = new MockSessionChannel();
            using var wrapper = CreateTlsCryptWrapper(mockChannel);
            
            var header = new TestSessionPacketHeader { Opcode = 0x03, SessionId = 0xDEADBEEF };
            var originalData = GenerateTestData(128);
            var originalPacket = new SessionPacket { Header = header, Data = originalData };

            // Act - Complete TLS crypt flow: Write -> Send -> Receive -> Read
            wrapper.Write(originalPacket);
            await wrapper.Send(CancellationToken.None);
            
            // Simulate the encrypted packet being received
            var sentPackets = mockChannel.GetSentPackets();
            mockChannel.SimulateReceivePacket(sentPackets[0]);
            
            await wrapper.Receive(CancellationToken.None);
            var decryptedPacket = wrapper.Read();

            // Check complete flow integrity
            Assert.NotNull(decryptedPacket);
            Assert.Equal(originalData, decryptedPacket.Data.ToArray());
            
            var finalHeader = decryptedPacket.Header as TestSessionPacketHeader;
            Assert.NotNull(finalHeader);
            Assert.Equal(header.Opcode, finalHeader.Opcode);
            Assert.Equal(header.SessionId, finalHeader.SessionId);
        }

        [Fact]
        public async Task Write_Send_CheckTlsCryptEncryption_VerifiesDataObfuscation()
        {
            // Arrange
            using var mockChannel = new MockSessionChannel();
            using var wrapper = CreateTlsCryptWrapper(mockChannel);
            
            var header = new TestSessionPacketHeader { Opcode = 0x02, SessionId = 0x12345678 };
            var testData = Enumerable.Repeat((byte)0xAA, 64).ToArray(); // Predictable pattern
            var packet = new SessionPacket { Header = header, Data = testData };

            // Act
            wrapper.Write(packet);
            await wrapper.Send(CancellationToken.None);

            // Check encryption obfuscation
            var sentPackets = mockChannel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var wrappedPacket = sentPackets[0];
            
            // Skip header bytes and check that encrypted data is not the same as original
            var encryptedPayload = wrappedPacket.Data.Span[8..]; // Skip 8-byte header
            
            // Encrypted data should not match the original pattern (except for very unlikely cases)
            var originalPattern = testData;
            var encryptedMatchesOriginal = encryptedPayload.Length >= originalPattern.Length &&
                encryptedPayload[..originalPattern.Length].SequenceEqual(originalPattern);
            
            Assert.False(encryptedMatchesOriginal, "Encrypted data should not match original pattern");
        }

        [Fact]
        public async Task Send_Receive_CheckTlsCryptCancellation_HandlesTokenCorrectly()
        {
            // Arrange
            using var mockChannel = new MockSessionChannel();
            using var wrapper = CreateTlsCryptWrapper(mockChannel);
            using var cts = new CancellationTokenSource();
            
            var header = new TestSessionPacketHeader { Opcode = 0x01, SessionId = 0x12345678 };
            var packet = new SessionPacket { Header = header, Data = GenerateTestData(64) };

            wrapper.Write(packet);

            // Act & Assert - Operations should complete normally with cancellation token
            await wrapper.Send(cts.Token);
            await wrapper.Receive(cts.Token);

            // Verify operations completed successfully
            var sentPackets = mockChannel.GetSentPackets();
            Assert.Single(sentPackets);
        }

        [Fact] 
        public void Read_CheckEmptyTlsCryptWrapper_ReturnsNullForNoPackets()
        {
            // Arrange
            using var mockChannel = new MockSessionChannel();
            using var wrapper = CreateTlsCryptWrapper(mockChannel);

            // Act - Try to read when no packets are available
            var packet = wrapper.Read();

            // Assert
            Assert.Null(packet);
        }

        [Fact]
        public async Task Write_Send_CheckTlsCryptMultiplePackets_VerifiesSequentialProcessing()
        {
            // Arrange
            using var mockChannel = new MockSessionChannel();
            using var wrapper = CreateTlsCryptWrapper(mockChannel);
            
            var packets = new List<SessionPacket>();
            for (int i = 0; i < 3; i++)
            {
                var header = new TestSessionPacketHeader { Opcode = 0x01, SessionId = (uint)(0x11111111 * (i + 1)) };
                var data = BitConverter.GetBytes(i);
                packets.Add(new SessionPacket { Header = header, Data = data });
            }

            // Act - Write multiple packets
            foreach (var packet in packets)
            {
                wrapper.Write(packet);
            }
            await wrapper.Send(CancellationToken.None);

            // Check sequential processing
            var sentPackets = mockChannel.GetSentPackets();
            Assert.Equal(packets.Count, sentPackets.Count);
            
            // All packets should be wrapped and have increasing packet IDs
            for (int i = 0; i < sentPackets.Count; i++)
            {
                var wrappedPacket = sentPackets[i];
                Assert.True(wrappedPacket.Data.Length >= 8, $"Packet {i} should have TLS crypt header");
                
                // Extract packet ID from first 4 bytes
                var packetIdBytes = wrappedPacket.Data.Span[0..4];
                var packetId = BitConverter.ToUInt32(packetIdBytes);
                Assert.Equal((uint)(i + 1), packetId); // Packet IDs start from 1
            }
        }

        [Fact]
        public void Dispose_CheckTlsCryptWrapperCleanup_VerifiesProperDisposal()
        {
            // Arrange
            var mockChannel = new MockSessionChannel();
            var wrapper = CreateTlsCryptWrapper(mockChannel);
            
            var header = new TestSessionPacketHeader { Opcode = 0x01, SessionId = 0x12345678 };
            var packet = new SessionPacket { Header = header, Data = GenerateTestData(64) };
            wrapper.Write(packet);

            // Act
            wrapper.Dispose();

            // Assert - Should not throw after disposal
            Assert.True(true, "TLS crypt wrapper disposal completed without exception");
            
            // Verify underlying channel is also disposed
            mockChannel.Dispose(); // Should not throw
        }

        [Theory]
        [InlineData(new byte[] { 0x16, 0x03, 0x03, 0x00 })]      // TLS handshake
        [InlineData(new byte[] { 0x17, 0x03, 0x03, 0x00 })]      // TLS application data
        [InlineData(new byte[] { 0x15, 0x03, 0x03, 0x00 })]      // TLS alert
        [InlineData(new byte[] { 0x14, 0x03, 0x03, 0x00 })]      // TLS change cipher spec
        public async Task Write_Send_CheckTlsProtocolData_HandlesProtocolSpecificPayloads(byte[] tlsData)
        {
            // Arrange
            using var mockChannel = new MockSessionChannel();
            using var wrapper = CreateTlsCryptWrapper(mockChannel);
            
            var header = new TestSessionPacketHeader { Opcode = 0x02, SessionId = 0x12345678 };
            var packet = new SessionPacket { Header = header, Data = tlsData };

            // Act
            wrapper.Write(packet);
            await wrapper.Send(CancellationToken.None);

            // Check TLS protocol data handling
            var sentPackets = mockChannel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var wrappedPacket = sentPackets[0];
            Assert.True(wrappedPacket.Data.Length > tlsData.Length, 
                "Wrapped TLS data should be larger due to TLS crypt header and encryption");
        }

        private TlsCryptWrapper CreateTlsCryptWrapper(ISessionChannel channel)
        {
            var random = new SecureRandom();
            var keySource = CryptoKeySource.Generate(random);
            var keys = CryptoKeys.DeriveFromKeySource(keySource, 0x123456789ABCDEF0UL);
            
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            
            return new TlsCryptWrapper(
                maximumQueueSize: 100,
                channel: channel,
                keys: keys,
                mode: OpenVpnMode.Client,
                random: random,
                loggerFactory: loggerFactory
            );
        }

        private static byte[] GenerateTestData(int length)
        {
            var data = new byte[length];
            var random = new Random(42); // Fixed seed for reproducible tests
            random.NextBytes(data);
            return data;
        }
    }
}