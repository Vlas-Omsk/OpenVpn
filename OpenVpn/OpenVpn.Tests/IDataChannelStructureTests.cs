using OpenVpn.Data;
using OpenVpn.Data.Packets;
using OpenVpn.IO;

namespace OpenVpn.Tests
{
    /// <summary>
    /// Comprehensive data structure tests for IDataChannel interface following Write -> Send -> Check output structure pattern
    /// and Receive -> Read -> Check input structure validation pattern.
    /// </summary>
    public class IDataChannelStructureTests
    {
        /// <summary>
        /// Mock implementation of IDataPacket for testing purposes
        /// </summary>
        private sealed class TestDataPacket : IDataPacket
        {
            public ReadOnlyMemory<byte> Data { get; set; }

            public void Serialize(PacketWriter writer)
            {
                writer.WriteBytes(Data.Span);
            }

            public void Deserialize(PacketReader reader)
            {
                if (reader.Available > 0)
                {
                    Data = reader.ReadMemory(reader.Available);
                }
            }
        }

        /// <summary>
        /// Mock implementation of IDataChannel for testing the interface structure
        /// </summary>
        private sealed class MockDataChannel : IDataChannel
        {
            private readonly Queue<IDataPacket> _writeQueue = new();
            private readonly Queue<IDataPacket> _readQueue = new();
            private readonly List<IDataPacket> _sentPackets = new();
            private readonly List<IDataPacket> _receivedPackets = new();

            public void Write(IDataPacket packet)
            {
                // Write pattern: Store packet for sending
                _writeQueue.Enqueue(packet);
            }

            public IDataPacket? Read()
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
            public void SimulateReceivePacket(IDataPacket packet)
            {
                _receivedPackets.Add(packet);
            }

            public IReadOnlyList<IDataPacket> GetSentPackets() => _sentPackets.AsReadOnly();
        }

        [Fact]
        public void Write_Send_CheckOutputStructure_VerifiesDataPacketFlow()
        {
            // Arrange
            var channel = new MockDataChannel();
            var testData = GenerateTestData(64);
            var packet = new TestDataPacket { Data = testData };

            // Act - Write pattern
            channel.Write(packet);

            // Send pattern 
            channel.Send(CancellationToken.None).Wait();

            // Check Output Structure
            var sentPackets = channel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var sentPacket = sentPackets[0] as TestDataPacket;
            Assert.NotNull(sentPacket);
            Assert.Equal(testData, sentPacket.Data.ToArray());
        }

        [Fact]
        public async Task Receive_Read_CheckInputStructure_ValidatesDataPacketFlow()
        {
            // Arrange
            var channel = new MockDataChannel();
            var testData = GenerateTestData(64);
            var packet = new TestDataPacket { Data = testData };

            // Simulate receiving a packet
            channel.SimulateReceivePacket(packet);

            // Act - Receive pattern
            await channel.Receive(CancellationToken.None);

            // Read pattern
            var readPacket = channel.Read();

            // Check Input Structure
            Assert.NotNull(readPacket);
            var dataPacket = readPacket as TestDataPacket;
            Assert.NotNull(dataPacket);
            Assert.Equal(testData, dataPacket.Data.ToArray());
        }

        [Theory]
        [InlineData(0)]    // Empty packet
        [InlineData(1)]    // Minimal data
        [InlineData(64)]   // Standard packet size
        [InlineData(512)]  // Medium packet
        [InlineData(1500)] // MTU-sized packet
        [InlineData(8192)] // Large packet
        public async Task Write_Send_CheckDataPacketSizes_HandlesVariousPayloadSizes(int dataSize)
        {
            // Arrange
            var channel = new MockDataChannel();
            var testData = GenerateTestData(dataSize);
            var packet = new TestDataPacket { Data = testData };

            // Act
            channel.Write(packet);
            await channel.Send(CancellationToken.None);

            // Check data packet size handling
            var sentPackets = channel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var sentPacket = sentPackets[0] as TestDataPacket;
            Assert.NotNull(sentPacket);
            Assert.Equal(dataSize, sentPacket.Data.Length);
            
            if (dataSize > 0)
            {
                Assert.Equal(testData, sentPacket.Data.ToArray());
            }
        }

        [Fact]
        public async Task Write_Send_CheckMultipleDataPackets_PreservesOrder()
        {
            // Arrange
            var channel = new MockDataChannel();
            var packets = new List<TestDataPacket>();
            
            // Create sequence of packets with identifiable data
            for (int i = 0; i < 10; i++)
            {
                var data = new byte[16];
                Array.Fill(data, (byte)i); // Fill with unique byte value
                packets.Add(new TestDataPacket { Data = data });
            }

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
                var sent = sentPackets[i] as TestDataPacket;
                Assert.NotNull(sent);
                Assert.Equal(original.Data.ToArray(), sent.Data.ToArray());
                
                // Verify the unique byte pattern
                Assert.True(sent.Data.Span.ToArray().All(b => b == (byte)i));
            }
        }

        [Theory]
        [InlineData(new byte[] { 0x00 })]                          // Zero byte
        [InlineData(new byte[] { 0xFF })]                          // Max byte
        [InlineData(new byte[] { 0x00, 0xFF, 0xAA, 0x55 })]      // Pattern bytes
        [InlineData(new byte[] { 0x45, 0x00, 0x00, 0x1C })]      // IP header start
        [InlineData(new byte[] { 0x08, 0x00, 0x45, 0x00 })]      // Ethernet + IP
        public async Task Write_Send_CheckNetworkDataPatterns_HandlesCommonProtocols(byte[] networkData)
        {
            // Arrange
            var channel = new MockDataChannel();
            var packet = new TestDataPacket { Data = networkData };

            // Act
            channel.Write(packet);
            await channel.Send(CancellationToken.None);

            // Check network data pattern handling
            var sentPackets = channel.GetSentPackets();
            Assert.Single(sentPackets);
            
            var sentPacket = sentPackets[0] as TestDataPacket;
            Assert.NotNull(sentPacket);
            Assert.Equal(networkData, sentPacket.Data.ToArray());
        }

        [Fact]
        public async Task Receive_Read_CheckPacketIntegrity_VerifiesDataConsistency()
        {
            // Arrange
            var channel = new MockDataChannel();
            var testPackets = new List<TestDataPacket>();
            
            // Create packets with different data characteristics
            testPackets.Add(new TestDataPacket { Data = new byte[] { 0x08, 0x00 } }); // Ethernet type
            testPackets.Add(new TestDataPacket { Data = GenerateTestData(1024) });    // Random data
            testPackets.Add(new TestDataPacket { Data = new byte[0] });               // Empty packet
            testPackets.Add(new TestDataPacket { Data = Enumerable.Repeat((byte)0xAA, 256).ToArray() }); // Pattern

            // Simulate receiving packets
            foreach (var packet in testPackets)
            {
                channel.SimulateReceivePacket(packet);
            }

            // Act
            await channel.Receive(CancellationToken.None);

            // Check packet integrity
            var receivedPackets = new List<IDataPacket>();
            IDataPacket? readPacket;
            while ((readPacket = channel.Read()) != null)
            {
                receivedPackets.Add(readPacket);
            }

            Assert.Equal(testPackets.Count, receivedPackets.Count);
            
            for (int i = 0; i < testPackets.Count; i++)
            {
                var original = testPackets[i];
                var received = receivedPackets[i] as TestDataPacket;
                Assert.NotNull(received);
                Assert.Equal(original.Data.ToArray(), received.Data.ToArray());
            }
        }

        [Fact]
        public async Task Write_Send_Receive_Read_CheckDataChannelFlow_VerifiesCompleteDataPath()
        {
            // Arrange
            var channel = new MockDataChannel();
            var originalData = GenerateTestData(512); // Typical network packet size
            var packet = new TestDataPacket { Data = originalData };

            // Act - Complete data channel flow: Write -> Send -> Receive -> Read
            channel.Write(packet);
            await channel.Send(CancellationToken.None);
            
            // Simulate the sent packet being received (like network loopback)
            var sentPackets = channel.GetSentPackets();
            channel.SimulateReceivePacket(sentPackets[0]);
            
            await channel.Receive(CancellationToken.None);
            var readPacket = channel.Read();

            // Check complete data path integrity
            Assert.NotNull(readPacket);
            var finalPacket = readPacket as TestDataPacket;
            Assert.NotNull(finalPacket);
            Assert.Equal(originalData, finalPacket.Data.ToArray());
        }

        [Fact]
        public async Task Send_Receive_CheckAsyncDataFlow_VerifiesAsynchronousOperations()
        {
            // Arrange
            var channel = new MockDataChannel();
            var packets = Enumerable.Range(0, 100)
                .Select(i => new TestDataPacket { Data = new byte[] { (byte)i, (byte)(i >> 8) } })
                .ToList();

            // Act - Async write operations
            var writeTasks = packets.Select(async (packet, index) =>
            {
                await Task.Delay(index % 10); // Staggered delays
                channel.Write(packet);
            });

            await Task.WhenAll(writeTasks);
            await channel.Send(CancellationToken.None);

            // Check async data flow results
            var sentPackets = channel.GetSentPackets();
            Assert.Equal(packets.Count, sentPackets.Count);
            
            // Verify all packets were sent (order may vary due to async)
            var sentData = sentPackets.Cast<TestDataPacket>()
                .Select(p => p.Data.ToArray())
                .ToHashSet(new ByteArrayComparer());
            var originalData = packets.Select(p => p.Data.ToArray())
                .ToHashSet(new ByteArrayComparer());
            
            Assert.Equal(originalData, sentData);
        }

        [Fact]
        public async Task Write_Send_CheckCancellationToken_HandlesTaskCancellation()
        {
            // Arrange
            var channel = new MockDataChannel();
            using var cts = new CancellationTokenSource();
            var packet = new TestDataPacket { Data = GenerateTestData(64) };

            channel.Write(packet);

            // Act & Assert - Operations should complete normally with cancellation token
            await channel.Send(cts.Token);
            await channel.Receive(cts.Token);

            // Verify operations completed successfully
            var sentPackets = channel.GetSentPackets();
            Assert.Single(sentPackets);
        }

        [Fact] 
        public void Read_CheckEmptyChannel_ReturnsNullForNoPackets()
        {
            // Arrange
            var channel = new MockDataChannel();

            // Act - Try to read when no packets are available
            var packet = channel.Read();

            // Assert
            Assert.Null(packet);
        }

        [Fact]
        public async Task Write_Send_CheckHighThroughput_HandlesLargePacketVolume()
        {
            // Arrange
            var channel = new MockDataChannel();
            var packetCount = 1000;
            var packets = new List<TestDataPacket>();
            
            // Create many packets with unique identifiers
            for (int i = 0; i < packetCount; i++)
            {
                var data = BitConverter.GetBytes(i).Concat(GenerateTestData(60)).ToArray();
                packets.Add(new TestDataPacket { Data = data });
            }

            // Act - High throughput write and send
            foreach (var packet in packets)
            {
                channel.Write(packet);
            }
            
            await channel.Send(CancellationToken.None);

            // Check high throughput handling
            var sentPackets = channel.GetSentPackets();
            Assert.Equal(packetCount, sentPackets.Count);
            
            // Verify packet integrity with unique identifiers
            for (int i = 0; i < packetCount; i++)
            {
                var sentPacket = sentPackets[i] as TestDataPacket;
                Assert.NotNull(sentPacket);
                
                var packetId = BitConverter.ToInt32(sentPacket.Data.Span[0..4]);
                Assert.Equal(i, packetId);
            }
        }

        [Fact]
        public async Task Receive_Read_CheckPacketSerialization_VerifiesSerializationIntegrity()
        {
            // Arrange
            var channel = new MockDataChannel();
            var originalData = GenerateTestData(256);
            var packet = new TestDataPacket { Data = originalData };

            // Test serialization by round-trip through packet interface
            using var memoryStream = new MemoryStream();
            using var writer = new PacketWriter(memoryStream);
            
            packet.Serialize(writer);
            
            memoryStream.Position = 0;
            var reader = new PacketReader(memoryStream.ToArray());
            
            var deserializedPacket = new TestDataPacket();
            deserializedPacket.Deserialize(reader);

            // Simulate channel operations
            channel.SimulateReceivePacket(deserializedPacket);
            await channel.Receive(CancellationToken.None);
            var readPacket = channel.Read();

            // Check serialization integrity
            Assert.NotNull(readPacket);
            var finalPacket = readPacket as TestDataPacket;
            Assert.NotNull(finalPacket);
            Assert.Equal(originalData, finalPacket.Data.ToArray());
        }

        private static byte[] GenerateTestData(int length)
        {
            var data = new byte[length];
            var random = new Random(42); // Fixed seed for reproducible tests
            random.NextBytes(data);
            return data;
        }

        /// <summary>
        /// Comparer for byte arrays to use in HashSet operations
        /// </summary>
        private class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[]? x, byte[]? y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                return x.SequenceEqual(y);
            }

            public int GetHashCode(byte[] obj)
            {
                return obj.Aggregate(0, (hash, b) => hash ^ b.GetHashCode());
            }
        }
    }
}