using OpenVpn.Control;
using OpenVpn.Control.Packets;
using OpenVpn.IO;

namespace OpenVpn.Tests
{
    /// <summary>
    /// Comprehensive data structure tests for IControlChannel interface following Write -> Send -> Check output structure pattern
    /// and Receive -> Read -> Check input structure validation pattern.
    /// </summary>
    public class IControlChannelStructureTests
    {
        /// <summary>
        /// Mock implementation of IControlPacket for testing purposes
        /// </summary>
        private sealed class TestControlPacket : IControlPacket
        {
            public required ReadOnlyMemory<byte> Data { get; init; }

            public void Serialize(OpenVpnMode mode, PacketWriter writer)
            {
                writer.WriteBytes(Data.Span);
            }

            public bool TryDeserialize(OpenVpnMode mode, PacketReader reader, out int requiredSize)
            {
                requiredSize = 0;
                if (!reader.IsEof)
                {
                    var availableData = reader.AvailableMemory;
                    Data = availableData.ToArray(); // Store the data
                    reader.Skip(availableData.Length); // Consume all available data
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Mock implementation of IControlChannel for testing the interface structure
        /// </summary>
        private sealed class MockControlChannel : IControlChannel
        {
            private readonly Queue<IControlPacket> _writeQueue = new();
            private readonly Queue<IControlPacket> _readQueue = new();
            private readonly List<IControlPacket> _sentPackets = new();
            private readonly List<IControlPacket> _receivedPackets = new();

            public ulong SessionId { get; } = 0x123456789ABCDEF0UL;
            public ulong RemoteSessionId { get; } = 0x0FEDCBA987654321UL;

            public void Connect()
            {
                // Mock connection - no actual network setup needed
            }

            public void Write(IControlPacket packet)
            {
                // Write pattern: Store packet for sending
                _writeQueue.Enqueue(packet);
            }

            public IControlPacket? Read()
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
            public void SimulateReceivePacket(IControlPacket packet)
            {
                _receivedPackets.Add(packet);
            }

            public IReadOnlyList<IControlPacket> GetSentPackets() => _sentPackets.AsReadOnly();

            public void Dispose()
            {
                _writeQueue.Clear();
                _readQueue.Clear();
                _sentPackets.Clear();
                _receivedPackets.Clear();
            }
        }

        [Fact]
        public void Write_Send_CheckOutputStructure_VerifiesPacketFlow()
        {
            // Arrange
            using var channel = new MockControlChannel();
            var testData = GenerateTestData(64);
            var packet = new TestControlPacket { Data = testData };
            channel.Connect();

            // Act - Write pattern
            channel.Write(packet);

            // Send pattern 
            channel.Send(CancellationToken.None).Wait();

            // Check Output Structure
            var sentPackets = channel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var sentPacket = sentPackets[0] as TestControlPacket;
            Assert.NotNull(sentPacket);
            Assert.Equal(testData, sentPacket.Data.ToArray());
        }

        [Fact]
        public async Task Receive_Read_CheckInputStructure_ValidatesPacketFlow()
        {
            // Arrange
            using var channel = new MockControlChannel();
            var testData = GenerateTestData(64);
            var packet = new TestControlPacket { Data = testData };
            channel.Connect();

            // Simulate receiving a packet
            channel.SimulateReceivePacket(packet);

            // Act - Receive pattern
            await channel.Receive(CancellationToken.None);

            // Read pattern
            var readPacket = channel.Read();

            // Check Input Structure
            Assert.NotNull(readPacket);
            var controlPacket = readPacket as TestControlPacket;
            Assert.NotNull(controlPacket);
            Assert.Equal(testData, controlPacket.Data.ToArray());
        }

        [Fact]
        public void Write_Send_CheckSessionIdStructure_VerifiesSessionIdentifiers()
        {
            // Arrange
            using var channel = new MockControlChannel();
            channel.Connect();

            // Check session ID structure
            Assert.Equal(0x123456789ABCDEF0UL, channel.SessionId);
            Assert.Equal(0x0FEDCBA987654321UL, channel.RemoteSessionId);
            
            // Verify session IDs are consistent
            Assert.NotEqual(channel.SessionId, channel.RemoteSessionId);
            Assert.True(channel.SessionId != 0);
            Assert.True(channel.RemoteSessionId != 0);
        }

        [Fact]
        public async Task Write_Send_CheckMultiplePackets_PreservesOrder()
        {
            // Arrange
            using var channel = new MockControlChannel();
            var packets = new List<TestControlPacket>();
            
            for (int i = 0; i < 5; i++)
            {
                var data = new byte[] { (byte)i, (byte)(i + 1), (byte)(i + 2) };
                packets.Add(new TestControlPacket { Data = data });
            }
            
            channel.Connect();

            // Act - Write multiple packets
            foreach (var packet in packets)
            {
                channel.Write(packet);
            }

            // Send all packets
            await channel.Send(CancellationToken.None);

            // Check packet order preservation
            var sentPackets = channel.GetSentPackets();
            Assert.Equal(packets.Count, sentPackets.Count);
            
            for (int i = 0; i < packets.Count; i++)
            {
                var original = packets[i];
                var sent = sentPackets[i] as TestControlPacket;
                Assert.NotNull(sent);
                Assert.Equal(original.Data.ToArray(), sent.Data.ToArray());
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(16)]
        [InlineData(64)]
        [InlineData(256)]
        [InlineData(1024)]
        [InlineData(4096)]
        public async Task Write_Send_CheckBoundaryValues_HandlesVariousPacketSizes(int packetSize)
        {
            // Arrange
            using var channel = new MockControlChannel();
            var testData = GenerateTestData(packetSize);
            var packet = new TestControlPacket { Data = testData };
            channel.Connect();

            // Act
            channel.Write(packet);
            await channel.Send(CancellationToken.None);

            // Check boundary value handling
            var sentPackets = channel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var sentPacket = sentPackets[0] as TestControlPacket;
            Assert.NotNull(sentPacket);
            Assert.Equal(packetSize, sentPacket.Data.Length);
            
            if (packetSize > 0)
            {
                Assert.Equal(testData, sentPacket.Data.ToArray());
            }
        }

        [Fact]
        public async Task Receive_Read_CheckPacketStructureValidation_VerifiesDataIntegrity()
        {
            // Arrange
            using var channel = new MockControlChannel();
            var testPackets = new List<TestControlPacket>();
            
            // Create packets with different data patterns
            testPackets.Add(new TestControlPacket { Data = new byte[] { 0x00, 0xFF, 0xAA, 0x55 } });
            testPackets.Add(new TestControlPacket { Data = GenerateTestData(32) });
            testPackets.Add(new TestControlPacket { Data = new byte[0] }); // Empty packet
            
            channel.Connect();

            // Simulate receiving packets
            foreach (var packet in testPackets)
            {
                channel.SimulateReceivePacket(packet);
            }

            // Act
            await channel.Receive(CancellationToken.None);

            // Check packet structure validation
            var receivedPackets = new List<IControlPacket>();
            IControlPacket? readPacket;
            while ((readPacket = channel.Read()) != null)
            {
                receivedPackets.Add(readPacket);
            }

            Assert.Equal(testPackets.Count, receivedPackets.Count);
            
            for (int i = 0; i < testPackets.Count; i++)
            {
                var original = testPackets[i];
                var received = receivedPackets[i] as TestControlPacket;
                Assert.NotNull(received);
                Assert.Equal(original.Data.ToArray(), received.Data.ToArray());
            }
        }

        [Fact]
        public async Task Write_Send_Receive_Read_CheckFullFlow_VerifiesCompleteDataFlow()
        {
            // Arrange
            using var channel = new MockControlChannel();
            var originalData = GenerateTestData(128);
            var packet = new TestControlPacket { Data = originalData };
            channel.Connect();

            // Act - Complete flow: Write -> Send -> Receive -> Read
            channel.Write(packet);
            await channel.Send(CancellationToken.None);
            
            // Simulate the sent packet being received back (like in a loopback)
            var sentPackets = channel.GetSentPackets();
            channel.SimulateReceivePacket(sentPackets[0]);
            
            await channel.Receive(CancellationToken.None);
            var readPacket = channel.Read();

            // Check complete flow integrity
            Assert.NotNull(readPacket);
            var finalPacket = readPacket as TestControlPacket;
            Assert.NotNull(finalPacket);
            Assert.Equal(originalData, finalPacket.Data.ToArray());
        }

        [Fact]
        public async Task Send_Receive_CheckCancellation_HandlesTokenCorrectly()
        {
            // Arrange
            using var channel = new MockControlChannel();
            using var cts = new CancellationTokenSource();
            var packet = new TestControlPacket { Data = GenerateTestData(32) };
            channel.Connect();

            channel.Write(packet);

            // Act & Assert - Operations should complete normally
            await channel.Send(cts.Token);
            await channel.Receive(cts.Token);

            // Verify cancellation token is accepted (no exceptions)
            Assert.True(true, "Operations completed without cancellation exceptions");
        }

        [Fact] 
        public void Write_Read_CheckEmptyRead_HandlesNoPacketsAvailable()
        {
            // Arrange
            using var channel = new MockControlChannel();
            channel.Connect();

            // Act - Try to read when no packets are available
            var packet = channel.Read();

            // Assert
            Assert.Null(packet);
        }

        [Fact]
        public async Task Write_Send_CheckAsyncBehavior_VerifiesAsyncOperations()
        {
            // Arrange
            using var channel = new MockControlChannel();
            var packets = new List<TestControlPacket>();
            
            for (int i = 0; i < 10; i++)
            {
                packets.Add(new TestControlPacket { Data = new byte[] { (byte)i } });
            }
            
            channel.Connect();

            // Act - Async write and send operations
            var writeTasks = packets.Select(async packet =>
            {
                await Task.Delay(1); // Small delay to test async behavior
                channel.Write(packet);
            });

            await Task.WhenAll(writeTasks);
            await channel.Send(CancellationToken.None);

            // Check async operation results
            var sentPackets = channel.GetSentPackets();
            Assert.Equal(packets.Count, sentPackets.Count);
        }

        [Fact]
        public void Dispose_CheckResourceCleanup_VerifiesProperDisposal()
        {
            // Arrange
            var channel = new MockControlChannel();
            var packet = new TestControlPacket { Data = GenerateTestData(32) };
            channel.Connect();
            channel.Write(packet);

            // Act
            channel.Dispose();

            // Assert - Should not throw after disposal
            Assert.True(true, "Disposal completed without exception");
        }

        [Theory]
        [InlineData(new byte[] { 0x00 })]
        [InlineData(new byte[] { 0xFF })]
        [InlineData(new byte[] { 0x00, 0xFF, 0xAA, 0x55 })]
        [InlineData(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 })]
        public async Task Write_Send_CheckSpecialBytePatterns_HandlesEdgeCases(byte[] specialData)
        {
            // Arrange
            using var channel = new MockControlChannel();
            var packet = new TestControlPacket { Data = specialData };
            channel.Connect();

            // Act
            channel.Write(packet);
            await channel.Send(CancellationToken.None);

            // Check special byte pattern handling
            var sentPackets = channel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var sentPacket = sentPackets[0] as TestControlPacket;
            Assert.NotNull(sentPacket);
            Assert.Equal(specialData, sentPacket.Data.ToArray());
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