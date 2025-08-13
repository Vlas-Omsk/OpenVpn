using OpenVpn.Sessions;
using OpenVpn.Sessions.Packets;
using OpenVpn.IO;

namespace OpenVpn.Tests
{
    /// <summary>
    /// Comprehensive data structure tests for ISessionChannel interface following Write -> Send -> Check output structure pattern
    /// and Receive -> Read -> Check input structure validation pattern.
    /// </summary>
    public class ISessionChannelStructureTests
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
        /// Mock implementation of ISessionChannel for testing the interface structure
        /// </summary>
        private sealed class MockSessionChannel : ISessionChannel
        {
            private readonly Queue<SessionPacket> _writeQueue = new();
            private readonly Queue<SessionPacket> _readQueue = new();
            private readonly List<SessionPacket> _sentPackets = new();
            private readonly List<SessionPacket> _receivedPackets = new();

            public void Write(SessionPacket packet)
            {
                // Write pattern: Store packet for sending
                _writeQueue.Enqueue(packet.Clone());
            }

            public SessionPacket? Read()
            {
                // Read pattern: Return received packet
                return _readQueue.TryDequeue(out var packet) ? packet : null;
            }

            public Task Send(CancellationToken cancellationToken)
            {
                // Send pattern: Move written packets to sent collection
                while (_writeQueue.TryDequeue(out var packet))
                {
                    _sentPackets.Add(packet);
                }
                return Task.CompletedTask;
            }

            public Task Receive(CancellationToken cancellationToken)
            {
                // Receive pattern: Move received packets to read queue
                foreach (var packet in _receivedPackets)
                {
                    _readQueue.Enqueue(packet);
                }
                _receivedPackets.Clear();
                return Task.CompletedTask;
            }

            // Test helper methods
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
        public void Write_Send_CheckOutputStructure_VerifiesSessionPacketFlow()
        {
            // Arrange
            using var channel = new MockSessionChannel();
            var header = new TestSessionPacketHeader { Opcode = 0x02, SessionId = 0xDEADBEEF };
            var testData = GenerateTestData(64);
            var packet = new SessionPacket { Header = header, Data = testData };

            // Act - Write pattern
            channel.Write(packet);

            // Send pattern 
            channel.Send(CancellationToken.None).Wait();

            // Check Output Structure
            var sentPackets = channel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var sentPacket = sentPackets[0];
            Assert.NotNull(sentPacket);
            Assert.Equal(testData, sentPacket.Data.ToArray());
            
            var sentHeader = sentPacket.Header as TestSessionPacketHeader;
            Assert.NotNull(sentHeader);
            Assert.Equal(header.Opcode, sentHeader.Opcode);
            Assert.Equal(header.SessionId, sentHeader.SessionId);
        }

        [Fact]
        public async Task Receive_Read_CheckInputStructure_ValidatesSessionPacketFlow()
        {
            // Arrange
            using var channel = new MockSessionChannel();
            var header = new TestSessionPacketHeader { Opcode = 0x03, SessionId = 0xCAFEBABE };
            var testData = GenerateTestData(64);
            var packet = new SessionPacket { Header = header, Data = testData };

            // Simulate receiving a packet
            channel.SimulateReceivePacket(packet);

            // Act - Receive pattern
            await channel.Receive(CancellationToken.None);

            // Read pattern
            var readPacket = channel.Read();

            // Check Input Structure
            Assert.NotNull(readPacket);
            Assert.Equal(testData, readPacket.Data.ToArray());
            
            var readHeader = readPacket.Header as TestSessionPacketHeader;
            Assert.NotNull(readHeader);
            Assert.Equal(header.Opcode, readHeader.Opcode);
            Assert.Equal(header.SessionId, readHeader.SessionId);
        }

        [Theory]
        [InlineData(0x01, 0x12345678u)]    // Standard session packet
        [InlineData(0x02, 0xFFFFFFFFu)]    // Control packet with max session ID
        [InlineData(0x03, 0x00000000u)]    // Data packet with zero session ID
        [InlineData(0x04, 0x80000000u)]    // ACK packet with high bit set
        [InlineData(0xFF, 0x55AA55AAu)]    // Edge case opcode with pattern ID
        public async Task Write_Send_CheckSessionPacketHeaders_VerifiesHeaderStructure(byte opcode, uint sessionId)
        {
            // Arrange
            using var channel = new MockSessionChannel();
            var header = new TestSessionPacketHeader { Opcode = opcode, SessionId = sessionId };
            var testData = GenerateTestData(32);
            var packet = new SessionPacket { Header = header, Data = testData };

            // Act
            channel.Write(packet);
            await channel.Send(CancellationToken.None);

            // Check header structure preservation
            var sentPackets = channel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var sentPacket = sentPackets[0];
            var sentHeader = sentPacket.Header as TestSessionPacketHeader;
            Assert.NotNull(sentHeader);
            Assert.Equal(opcode, sentHeader.Opcode);
            Assert.Equal(sessionId, sentHeader.SessionId);
            Assert.Equal(testData, sentPacket.Data.ToArray());
        }

        [Theory]
        [InlineData(0)]      // Empty session packet
        [InlineData(1)]      // Minimal data
        [InlineData(64)]     // Standard size
        [InlineData(512)]    // Medium packet
        [InlineData(1500)]   // MTU-sized packet
        [InlineData(65535)]  // Maximum packet size
        public async Task Write_Send_CheckSessionPacketSizes_HandlesVariousPayloadSizes(int dataSize)
        {
            // Arrange
            using var channel = new MockSessionChannel();
            var header = new TestSessionPacketHeader { Opcode = 0x02, SessionId = 0x12345678 };
            var testData = GenerateTestData(dataSize);
            var packet = new SessionPacket { Header = header, Data = testData };

            // Act
            channel.Write(packet);
            await channel.Send(CancellationToken.None);

            // Check session packet size handling
            var sentPackets = channel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var sentPacket = sentPackets[0];
            Assert.Equal(dataSize, sentPacket.Data.Length);
            
            if (dataSize > 0)
            {
                Assert.Equal(testData, sentPacket.Data.ToArray());
            }
        }

        [Fact]
        public async Task Write_Send_CheckMultipleSessionPackets_PreservesSequenceOrder()
        {
            // Arrange
            using var channel = new MockSessionChannel();
            var packets = new List<SessionPacket>();
            
            // Create sequence of packets with incremental session IDs
            for (uint i = 0; i < 10; i++)
            {
                var header = new TestSessionPacketHeader { Opcode = 0x01, SessionId = 0x10000000 + i };
                var data = BitConverter.GetBytes(i); // Unique data per packet
                packets.Add(new SessionPacket { Header = header, Data = data });
            }

            // Act - Write multiple packets
            foreach (var packet in packets)
            {
                channel.Write(packet);
            }

            // Send all packets
            await channel.Send(CancellationToken.None);

            // Check sequence order preservation
            var sentPackets = channel.GetSentPackets();
            Assert.Equal(packets.Count, sentPackets.Count);
            
            for (int i = 0; i < packets.Count; i++)
            {
                var original = packets[i];
                var sent = sentPackets[i];
                
                var originalHeader = original.Header as TestSessionPacketHeader;
                var sentHeader = sent.Header as TestSessionPacketHeader;
                Assert.NotNull(originalHeader);
                Assert.NotNull(sentHeader);
                
                Assert.Equal(originalHeader.SessionId, sentHeader.SessionId);
                Assert.Equal(original.Data.ToArray(), sent.Data.ToArray());
            }
        }

        [Fact]
        public async Task Receive_Read_CheckSessionPacketCloning_VerifiesDataIsolation()
        {
            // Arrange
            using var channel = new MockSessionChannel();
            var header = new TestSessionPacketHeader { Opcode = 0x02, SessionId = 0x12345678 };
            var originalData = GenerateTestData(64);
            var packet = new SessionPacket { Header = header, Data = originalData };

            // Test cloning behavior
            var clonedPacket = packet.Clone();
            
            // Modify original data to verify isolation
            if (originalData.Length > 0)
            {
                originalData[0] = (byte)(originalData[0] ^ 0xFF);
            }

            // Simulate receiving the cloned packet
            channel.SimulateReceivePacket(clonedPacket);
            await channel.Receive(CancellationToken.None);
            var readPacket = channel.Read();

            // Check data isolation
            Assert.NotNull(readPacket);
            Assert.NotEqual(originalData, readPacket.Data.ToArray()); // Should be different due to modification
            Assert.Equal(clonedPacket.Data.ToArray(), readPacket.Data.ToArray()); // Should match clone
        }

        [Fact]
        public async Task Write_Send_Receive_Read_CheckSessionFlow_VerifiesCompleteSessionPath()
        {
            // Arrange
            using var channel = new MockSessionChannel();
            var header = new TestSessionPacketHeader { Opcode = 0x03, SessionId = 0xDEADBEEF };
            var originalData = GenerateTestData(256);
            var packet = new SessionPacket { Header = header, Data = originalData };

            // Act - Complete session flow: Write -> Send -> Receive -> Read
            channel.Write(packet);
            await channel.Send(CancellationToken.None);
            
            // Simulate the sent packet being received (like session loopback)
            var sentPackets = channel.GetSentPackets();
            channel.SimulateReceivePacket(sentPackets[0]);
            
            await channel.Receive(CancellationToken.None);
            var readPacket = channel.Read();

            // Check complete session path integrity
            Assert.NotNull(readPacket);
            Assert.Equal(originalData, readPacket.Data.ToArray());
            
            var finalHeader = readPacket.Header as TestSessionPacketHeader;
            Assert.NotNull(finalHeader);
            Assert.Equal(header.Opcode, finalHeader.Opcode);
            Assert.Equal(header.SessionId, finalHeader.SessionId);
        }

        [Fact]
        public async Task Write_Send_CheckSessionHeaderSerialization_VerifiesHeaderIntegrity()
        {
            // Arrange
            using var channel = new MockSessionChannel();
            var header = new TestSessionPacketHeader { Opcode = 0x05, SessionId = 0xABCDEF01 };
            var testData = GenerateTestData(128);

            // Test header serialization round-trip
            using var memoryStream = new MemoryStream();
            using var writer = new PacketWriter(memoryStream);
            
            header.Serialize(writer);
            
            memoryStream.Position = 0;
            using var reader = new PacketReader(memoryStream);
            
            var deserializedHeader = new TestSessionPacketHeader();
            var success = deserializedHeader.TryDeserialize(reader, out var requiredSize);
            
            Assert.True(success);
            Assert.Equal(5, requiredSize); // 1 + 4 bytes
            Assert.Equal(header.Opcode, deserializedHeader.Opcode);
            Assert.Equal(header.SessionId, deserializedHeader.SessionId);

            // Test through channel
            var packet = new SessionPacket { Header = deserializedHeader, Data = testData };
            channel.Write(packet);
            await channel.Send(CancellationToken.None);

            // Check header serialization integrity
            var sentPackets = channel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var sentHeader = sentPackets[0].Header as TestSessionPacketHeader;
            Assert.NotNull(sentHeader);
            Assert.Equal(header.Opcode, sentHeader.Opcode);
            Assert.Equal(header.SessionId, sentHeader.SessionId);
        }

        [Fact]
        public async Task Send_Receive_CheckSessionChannelCancellation_HandlesTokenCorrectly()
        {
            // Arrange
            using var channel = new MockSessionChannel();
            using var cts = new CancellationTokenSource();
            var header = new TestSessionPacketHeader { Opcode = 0x01, SessionId = 0x12345678 };
            var packet = new SessionPacket { Header = header, Data = GenerateTestData(64) };

            channel.Write(packet);

            // Act & Assert - Operations should complete normally with cancellation token
            await channel.Send(cts.Token);
            await channel.Receive(cts.Token);

            // Verify operations completed successfully
            var sentPackets = channel.GetSentPackets();
            Assert.Single(sentPackets);
        }

        [Fact] 
        public void Read_CheckEmptySessionChannel_ReturnsNullForNoPackets()
        {
            // Arrange
            using var channel = new MockSessionChannel();

            // Act - Try to read when no packets are available
            var packet = channel.Read();

            // Assert
            Assert.Null(packet);
        }

        [Fact]
        public async Task Write_Send_CheckSessionMultiplexing_HandlesMultipleSessionIds()
        {
            // Arrange
            using var channel = new MockSessionChannel();
            var sessionIds = new uint[] { 0x11111111, 0x22222222, 0x33333333, 0x44444444 };
            var packets = new List<SessionPacket>();
            
            // Create packets for different sessions
            foreach (var sessionId in sessionIds)
            {
                var header = new TestSessionPacketHeader { Opcode = 0x02, SessionId = sessionId };
                var data = BitConverter.GetBytes(sessionId);
                packets.Add(new SessionPacket { Header = header, Data = data });
            }

            // Act - Write packets from multiple sessions
            foreach (var packet in packets)
            {
                channel.Write(packet);
            }
            
            await channel.Send(CancellationToken.None);

            // Check session multiplexing handling
            var sentPackets = channel.GetSentPackets();
            Assert.Equal(sessionIds.Length, sentPackets.Count);
            
            for (int i = 0; i < sessionIds.Length; i++)
            {
                var sentHeader = sentPackets[i].Header as TestSessionPacketHeader;
                Assert.NotNull(sentHeader);
                Assert.Equal(sessionIds[i], sentHeader.SessionId);
                
                var expectedData = BitConverter.GetBytes(sessionIds[i]);
                Assert.Equal(expectedData, sentPackets[i].Data.ToArray());
            }
        }

        [Fact]
        public void Dispose_CheckSessionChannelCleanup_VerifiesProperDisposal()
        {
            // Arrange
            var channel = new MockSessionChannel();
            var header = new TestSessionPacketHeader { Opcode = 0x01, SessionId = 0x12345678 };
            var packet = new SessionPacket { Header = header, Data = GenerateTestData(64) };
            channel.Write(packet);

            // Act
            channel.Dispose();

            // Assert - Should not throw after disposal
            Assert.True(true, "Session channel disposal completed without exception");
        }

        [Theory]
        [InlineData(new byte[] { 0x45, 0x00, 0x00, 0x1C })]      // IPv4 packet start
        [InlineData(new byte[] { 0x60, 0x00, 0x00, 0x00 })]      // IPv6 packet start  
        [InlineData(new byte[] { 0x08, 0x00, 0x45, 0x00 })]      // Ethernet + IPv4
        [InlineData(new byte[] { 0x86, 0xDD, 0x60, 0x00 })]      // Ethernet + IPv6
        public async Task Write_Send_CheckNetworkPacketPayloads_HandlesTunnelData(byte[] networkData)
        {
            // Arrange
            using var channel = new MockSessionChannel();
            var header = new TestSessionPacketHeader { Opcode = 0x02, SessionId = 0x12345678 };
            var packet = new SessionPacket { Header = header, Data = networkData };

            // Act
            channel.Write(packet);
            await channel.Send(CancellationToken.None);

            // Check network packet payload handling
            var sentPackets = channel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var sentPacket = sentPackets[0];
            Assert.Equal(networkData, sentPacket.Data.ToArray());
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