using Microsoft.Extensions.Logging;
using OpenVpn.Sessions.Packets;
using OpenVpn.IO;
using Xunit.Abstractions;

namespace OpenVpn.Tests
{
    public class StrictOrderPacketsQueueTests
    {
        private readonly ILogger<StrictOrderPacketsQueue<SessionPacket>> _logger;

        public StrictOrderPacketsQueueTests(ITestOutputHelper outputHelper)
        {
            _logger = LoggerFactory.Create(x => x.AddXUnit(outputHelper))
                .CreateLogger<StrictOrderPacketsQueue<SessionPacket>>();
        }

        private class MockSessionPacketHeader : ISessionPacketHeader
        {
            public byte Opcode { get; set; }
            public byte KeyId { get; set; }

            public void Serialize(PacketWriter writer) { }
            public bool TryDeserialize(PacketReader reader) => true;
        }

        private static SessionPacket CreateMockPacket(byte opcode = 1, byte keyId = 0)
        {
            return new SessionPacket
            {
                Header = new MockSessionPacketHeader { Opcode = opcode, KeyId = keyId },
                Data = new byte[] { 1, 2, 3, 4 }
            };
        }

        [Fact]
        public void TryEnqueue_FirstPacket_ReturnsTrue()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(10, 0, _logger);
            var packet = CreateMockPacket();

            var result = queue.TryEnqueue(0, packet);

            Assert.True(result);
        }

        [Fact]
        public void TryEnqueue_SequentialPackets_ReturnsTrue()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(10, 0, _logger);
            var packet1 = CreateMockPacket();
            var packet2 = CreateMockPacket();

            var result1 = queue.TryEnqueue(0, packet1);
            var result2 = queue.TryEnqueue(1, packet2);

            Assert.True(result1);
            Assert.True(result2);
        }

        [Fact]
        public void TryEnqueue_DuplicatePacket_ReturnsFalse()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(10, 0, _logger);
            var packet1 = CreateMockPacket();
            var packet2 = CreateMockPacket();

            queue.TryEnqueue(0, packet1);
            var result = queue.TryEnqueue(0, packet2);

            Assert.False(result);
        }

        [Fact]
        public void TryEnqueue_OutOfOrderPacket_CreatesGaps()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(10, 0, _logger);
            var packet = CreateMockPacket();

            var result = queue.TryEnqueue(5, packet);

            Assert.True(result);
        }

        [Fact]
        public void TryEnqueue_ExceedsCapacity_ReturnsFalse()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(2, 0, _logger);
            var packet = CreateMockPacket();

            var result = queue.TryEnqueue(10, packet);

            Assert.False(result);
        }

        [Fact]
        public void TryEnqueue_OldPacket_ReturnsTrue()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(10, 0, _logger);
            var packet1 = CreateMockPacket();
            var packet2 = CreateMockPacket();

            // First packet creates gaps 0-4
            queue.TryEnqueue(5, packet1);

            // Now try to enqueue 3, should succeed because it fills an existing gap
            var result = queue.TryEnqueue(3, packet2);

            Assert.True(result);
        }

        [Fact]
        public void TryDequeue_EmptyQueue_ReturnsFalse()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(10, 0, _logger);

            var result = queue.TryDequeue(out var packet);

            Assert.False(result);
            Assert.Null(packet);
        }

        [Fact]
        public void TryDequeue_WithPacket_ReturnsPacket()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(10, 0, _logger);
            var expectedPacket = CreateMockPacket();

            queue.TryEnqueue(0, expectedPacket);
            var result = queue.TryDequeue(out var packet);

            Assert.True(result);
            Assert.NotNull(packet);
            Assert.Equal(expectedPacket.Header.Opcode, packet.Header.Opcode);
        }

        [Fact]
        public void TryDequeue_WithGap_ReturnsFalse()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(10, 0, _logger);
            var packet = CreateMockPacket();

            queue.TryEnqueue(1, packet); // Skip packet 0

            var result = queue.TryDequeue(out var dequeuedPacket);

            Assert.False(result);
            Assert.Null(dequeuedPacket);
        }

        [Fact]
        public void TryDequeue_FillsGap_ReturnsPacket()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(10, 0, _logger);
            var packet1 = CreateMockPacket();
            var packet2 = CreateMockPacket();

            // Enqueue sequential packets normally
            queue.TryEnqueue(0, packet1);
            queue.TryEnqueue(1, packet2);

            // Should be able to dequeue both in order
            var result1 = queue.TryDequeue(out var dequeuedPacket1);
            Assert.True(result1);
            Assert.NotNull(dequeuedPacket1);

            var result2 = queue.TryDequeue(out var dequeuedPacket2);
            Assert.True(result2);
            Assert.NotNull(dequeuedPacket2);
        }

        [Fact]
        public void TryDequeue_SequentialCalls_MaintainsOrder()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(10, 0, _logger);
            var packet1 = CreateMockPacket(opcode: 1);
            var packet2 = CreateMockPacket(opcode: 2);

            queue.TryEnqueue(0, packet1);
            queue.TryEnqueue(1, packet2);

            var result1 = queue.TryDequeue(out var dequeued1);
            var result2 = queue.TryDequeue(out var dequeued2);

            Assert.True(result1);
            Assert.True(result2);
            Assert.Equal(1, dequeued1!.Header.Opcode);
            Assert.Equal(2, dequeued2!.Header.Opcode);
        }

        [Fact]
        public void GetReceivedIds_EmptyQueue_ReturnsEmpty()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(10, 0, _logger);

            var ids = queue.GetReceivedIds().ToList();

            Assert.Empty(ids);
        }

        [Fact]
        public void GetReceivedIds_WithPackets_ReturnsCorrectIds()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(10, 0, _logger);
            var packet1 = CreateMockPacket();
            var packet2 = CreateMockPacket();

            queue.TryEnqueue(0, packet1);
            queue.TryEnqueue(2, packet2); // Leave gap at 1

            var ids = queue.GetReceivedIds().ToList();

            Assert.Contains(0u, ids);
            Assert.Contains(2u, ids);
            Assert.DoesNotContain(1u, ids);
        }

        [Fact]
        public void TryEnqueue_FillsGapsInQueue_ReturnsTrue()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(10, 0, _logger);
            var packet1 = CreateMockPacket();
            var packet2 = CreateMockPacket();
            var packet3 = CreateMockPacket();

            queue.TryEnqueue(0, packet1);
            queue.TryEnqueue(2, packet3); // Create gap
            var result = queue.TryEnqueue(1, packet2); // Fill gap

            Assert.True(result);
        }

        [Fact]
        public void MultipleOperations_MaintainsConsistency()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(5, 0, _logger);
            var packets = new SessionPacket[5];

            for (int i = 0; i < 5; i++)
            {
                packets[i] = CreateMockPacket((byte)(i + 1));
            }

            // Enqueue packets in order (since gaps can't be filled after last id is updated)
            for (int i = 0; i < 5; i++)
            {
                var result = queue.TryEnqueue((uint)i, packets[i]);
                Assert.True(result);
            }

            // Dequeue in order
            for (int i = 0; i < 5; i++)
            {
                var result = queue.TryDequeue(out var packet);
                Assert.True(result);
                Assert.Equal((byte)(i + 1), packet!.Header.Opcode);
            }

            // Queue should be empty
            var finalResult = queue.TryDequeue(out var finalPacket);
            Assert.False(finalResult);
        }

        [Fact]
        public void CapacityLimit_PreventsFurtherEnqueuing()
        {
            var queue = new StrictOrderPacketsQueue<SessionPacket>(3, 0, _logger);
            var packet1 = CreateMockPacket();
            var packet2 = CreateMockPacket();

            queue.TryEnqueue(0, packet1);
            queue.TryEnqueue(1, packet1);
            queue.TryEnqueue(2, packet1);

            var result = queue.TryEnqueue(3, packet2);

            Assert.False(result);
        }
    }
}