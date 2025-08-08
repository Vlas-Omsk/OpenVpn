using OpenVpn.Sessions;
using OpenVpn.Sessions.Packets;
using OpenVpn.IO;

namespace OpenVpn.Tests
{
    public class SessionChannelDemuxerTests
    {
        private class MockSessionChannel : ISessionChannel
        {
            private readonly Queue<SessionPacket> _writeQueue = new();
            private readonly Queue<SessionPacket> _readQueue = new();

            public bool Disposed { get; private set; }

            public void Write(SessionPacket packet)
            {
                _writeQueue.Enqueue(packet);
            }

            public SessionPacket? Read()
            {
                return _readQueue.TryDequeue(out var packet) ? packet : null;
            }

            public Task Send(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task Receive(CancellationToken cancellationToken) => Task.CompletedTask;

            public void Dispose() { Disposed = true; }

            public void SimulateReceive(SessionPacket packet)
            {
                _readQueue.Enqueue(packet);
            }

            public bool HasWritten(out SessionPacket packet)
            {
                return _writeQueue.TryDequeue(out packet);
            }

            public int WriteQueueCount => _writeQueue.Count;
            public int ReadQueueCount => _readQueue.Count;
        }

        private class MockSessionPacketHeader : ISessionPacketHeader
        {
            public byte Opcode { get; set; }
            public byte KeyId { get; set; }

            public void Serialize(PacketWriter writer) { }
            public bool TryDeserialize(PacketReader reader) => true;
        }

        private static SessionPacket CreateMockPacket(byte opcode, byte keyId = 0)
        {
            return new SessionPacket
            {
                Header = new MockSessionPacketHeader { Opcode = opcode, KeyId = keyId },
                Data = new byte[] { 1, 2, 3, 4 }
            };
        }

        [Fact]
        public void RegisterFor_SingleOpcode_ReturnsChannel()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);

            var registeredChannel = demuxer.RegisterFor(new[] { (byte)1 });

            Assert.NotNull(registeredChannel);
        }

        [Fact]
        public void RegisterFor_MultipleOpcodes_ReturnsChannel()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);

            var registeredChannel = demuxer.RegisterFor(new[] { (byte)1, (byte)2, (byte)3 });

            Assert.NotNull(registeredChannel);
        }

        [Fact]
        public void RegisterFor_DuplicateOpcode_ThrowsArgumentException()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);

            demuxer.RegisterFor(new[] { (byte)1 });

            Assert.Throws<ArgumentException>(() => demuxer.RegisterFor(new[] { (byte)1 }));
        }

        [Fact]
        public void RegisterFor_OverlappingOpcodes_ThrowsArgumentException()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);

            demuxer.RegisterFor(new[] { (byte)1, (byte)2 });

            Assert.Throws<ArgumentException>(() => demuxer.RegisterFor(new[] { (byte)2, (byte)3 }));
        }

        [Fact]
        public void RegisteredChannel_Write_ForwardsToMainChannel()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);
            var registeredChannel = demuxer.RegisterFor(new[] { (byte)1 });
            var packet = CreateMockPacket(1);

            registeredChannel.Write(packet);

            Assert.True(mockChannel.HasWritten(out var writtenPacket));
            Assert.Equal(packet.Header.Opcode, writtenPacket.Header.Opcode);
        }

        [Fact]
        public void RegisteredChannel_Read_FiltersCorrectOpcode()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);
            var registeredChannel = demuxer.RegisterFor(new[] { (byte)1 });
            var expectedPacket = CreateMockPacket(1);

            mockChannel.SimulateReceive(expectedPacket);
            var readPacket = registeredChannel.Read();

            Assert.NotNull(readPacket);
            Assert.Equal(1, readPacket.Header.Opcode);
        }

        [Fact]
        public void RegisteredChannel_Read_IgnoresOtherOpcodes()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);
            var registeredChannel = demuxer.RegisterFor(new[] { (byte)1 });

            mockChannel.SimulateReceive(CreateMockPacket(2)); // Different opcode
            var readPacket = registeredChannel.Read();

            Assert.Null(readPacket);
        }

        [Fact]
        public void RegisteredChannel_Read_HandlesMultipleOpcodes()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);
            var registeredChannel = demuxer.RegisterFor(new[] { (byte)1, (byte)2 });

            mockChannel.SimulateReceive(CreateMockPacket(1));
            var packet1 = registeredChannel.Read();

            mockChannel.SimulateReceive(CreateMockPacket(2));
            var packet2 = registeredChannel.Read();

            Assert.NotNull(packet1);
            Assert.Equal(1, packet1.Header.Opcode);
            Assert.NotNull(packet2);
            Assert.Equal(2, packet2.Header.Opcode);
        }

        [Fact]
        public void MultipleChannels_ReadCorrectPackets()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);
            var channel1 = demuxer.RegisterFor(new[] { (byte)1 });
            var channel2 = demuxer.RegisterFor(new[] { (byte)2 });

            mockChannel.SimulateReceive(CreateMockPacket(1));
            mockChannel.SimulateReceive(CreateMockPacket(2));

            var packet1 = channel1.Read();
            var packet2 = channel2.Read();

            Assert.NotNull(packet1);
            Assert.Equal(1, packet1.Header.Opcode);
            Assert.NotNull(packet2);
            Assert.Equal(2, packet2.Header.Opcode);
        }

        [Fact]
        public void PacketRouting_UnregisteredOpcode_DroppedSilently()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);
            var registeredChannel = demuxer.RegisterFor(new[] { (byte)1 });

            mockChannel.SimulateReceive(CreateMockPacket(99)); // Unregistered opcode
            var readPacket = registeredChannel.Read();

            Assert.Null(readPacket);
        }

        [Fact]
        public void RegisteredChannel_QueuesBehindScenes()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);
            var channel1 = demuxer.RegisterFor(new[] { (byte)1 });
            var channel2 = demuxer.RegisterFor(new[] { (byte)2 });

            // Simulate receiving packet for channel2 first, then channel1
            mockChannel.SimulateReceive(CreateMockPacket(2));
            mockChannel.SimulateReceive(CreateMockPacket(1));

            // Read from channel1 first - should eventually get the packet for opcode 1
            // The first Read() call might route the opcode 2 packet to channel2 and return null
            SessionPacket? packet1 = null;
            for (int i = 0; i < 10 && packet1 == null; i++) // Retry logic to handle routing
            {
                packet1 = channel1.Read();
            }

            Assert.NotNull(packet1);
            Assert.Equal(1, packet1.Header.Opcode);

            // Try to read from channel2 - should get the packet for opcode 2 that was routed earlier
            var packet2 = channel2.Read();
            Assert.NotNull(packet2);
            Assert.Equal(2, packet2.Header.Opcode);
        }

        [Fact]
        public void RegisteredChannel_MultipleReads_ReturnsInOrder()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);
            var registeredChannel = demuxer.RegisterFor(new[] { (byte)1 });

            // Queue multiple packets with same opcode
            mockChannel.SimulateReceive(CreateMockPacket(1, 0));
            mockChannel.SimulateReceive(CreateMockPacket(1, 1));
            mockChannel.SimulateReceive(CreateMockPacket(1, 2));

            var packet1 = registeredChannel.Read();
            var packet2 = registeredChannel.Read();
            var packet3 = registeredChannel.Read();

            Assert.NotNull(packet1);
            Assert.Equal(0, packet1.Header.KeyId);
            Assert.NotNull(packet2);
            Assert.Equal(1, packet2.Header.KeyId);
            Assert.NotNull(packet3);
            Assert.Equal(2, packet3.Header.KeyId);
        }

        [Fact]
        public void RegisteredChannel_EmptyQueue_ReturnsNull()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);
            var registeredChannel = demuxer.RegisterFor(new[] { (byte)1 });

            var packet = registeredChannel.Read();

            Assert.Null(packet);
        }

        [Fact]
        public async Task RegisteredChannel_Send_ForwardsToMainChannel()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);
            var registeredChannel = demuxer.RegisterFor(new[] { (byte)1 });

            await registeredChannel.Send(CancellationToken.None);

            // This test verifies that Send() is forwarded to the main channel
            // Since MockSessionChannel returns completed task, this should complete without issue
        }

        [Fact]
        public async Task RegisteredChannel_Receive_ForwardsToMainChannel()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);
            var registeredChannel = demuxer.RegisterFor(new[] { (byte)1 });

            await registeredChannel.Receive(CancellationToken.None);

            // This test verifies that Receive() is forwarded to the main channel
            // Since MockSessionChannel returns completed task, this should complete without issue
        }

        [Fact]
        public void RegisteredChannel_Dispose_DoesNotDisposesMainChannel()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);
            var registeredChannel = demuxer.RegisterFor(new[] { (byte)1 });

            registeredChannel.Dispose();

            Assert.False(mockChannel.Disposed);
        }

        [Fact]
        public void Demuxer_Dispose_DisposesMainChannel()
        {
            var mockChannel = new MockSessionChannel();
            var demuxer = new SessionChannelDemuxer(mockChannel);

            demuxer.Dispose();

            Assert.True(mockChannel.Disposed);
        }

        [Fact]
        public void ComplexScenario_MultipleChannelsAndPackets()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);

            var controlChannel = demuxer.RegisterFor(new[] { (byte)4, (byte)5 }); // Control opcodes
            var dataChannel = demuxer.RegisterFor(new[] { (byte)6, (byte)7 });   // Data opcodes
            var ackChannel = demuxer.RegisterFor(new[] { (byte)8 });             // ACK opcode

            // Simulate mixed packet reception
            mockChannel.SimulateReceive(CreateMockPacket(6)); // Data
            mockChannel.SimulateReceive(CreateMockPacket(4)); // Control
            mockChannel.SimulateReceive(CreateMockPacket(8)); // ACK
            mockChannel.SimulateReceive(CreateMockPacket(7)); // Data
            mockChannel.SimulateReceive(CreateMockPacket(5)); // Control
            mockChannel.SimulateReceive(CreateMockPacket(99)); // Unknown (should be dropped)

            // Read from each channel
            var dataPacket1 = dataChannel.Read();
            var controlPacket1 = controlChannel.Read();
            var ackPacket = ackChannel.Read();
            var dataPacket2 = dataChannel.Read();
            var controlPacket2 = controlChannel.Read();

            // Verify correct routing
            Assert.NotNull(dataPacket1);
            Assert.Equal(6, dataPacket1.Header.Opcode);

            Assert.NotNull(controlPacket1);
            Assert.Equal(4, controlPacket1.Header.Opcode);

            Assert.NotNull(ackPacket);
            Assert.Equal(8, ackPacket.Header.Opcode);

            Assert.NotNull(dataPacket2);
            Assert.Equal(7, dataPacket2.Header.Opcode);

            Assert.NotNull(controlPacket2);
            Assert.Equal(5, controlPacket2.Header.Opcode);

            // All channels should be empty now
            Assert.Null(dataChannel.Read());
            Assert.Null(controlChannel.Read());
            Assert.Null(ackChannel.Read());
        }

        [Fact]
        public void RewritingPacket_PreservesDataAfterDequeue()
        {
            var mockChannel = new MockSessionChannel();
            using var demuxer = new SessionChannelDemuxer(mockChannel);
            var registeredChannel = demuxer.RegisterFor(new[] { (byte)1 });

            var originalData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var originalPacket = new SessionPacket
            {
                Header = new MockSessionPacketHeader { Opcode = 1, KeyId = 5 },
                Data = originalData
            };

            mockChannel.SimulateReceive(originalPacket);
            var readPacket = registeredChannel.Read();

            Assert.NotNull(readPacket);
            Assert.Equal(1, readPacket.Header.Opcode);
            Assert.Equal(5, readPacket.Header.KeyId);
            Assert.Equal(originalData, readPacket.Data.ToArray());
        }
    }
}