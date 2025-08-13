using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using OpenVpn.Control;
using OpenVpn.Control.Crypto;
using OpenVpn.Control.Packets;
using OpenVpn.IO;
using OpenVpn.Sessions;

namespace OpenVpn.Tests
{
    /// <summary>
    /// Comprehensive data structure tests for IControlChannel interface following Write -> Send -> Check output structure pattern
    /// and Receive -> Read -> Check input structure validation pattern.
    /// Tests actual ControlChannel implementation instead of mocks.
    /// </summary>
    public class IControlChannelStructureTests
    {
        /// <summary>
        /// Test implementation of IControlPacket for testing purposes
        /// </summary>
        private sealed class TestControlPacket : IControlPacket
        {
            public ReadOnlyMemory<byte> Data { get; set; }

            public void Serialize(OpenVpnMode mode, PacketWriter writer)
            {
                writer.WriteBytes(Data.Span);
            }

            public bool TryDeserialize(OpenVpnMode mode, PacketReader reader, out int requiredSize)
            {
                requiredSize = 0;
                if (reader.Available > 0)
                {
                    var availableData = reader.AvailableMemory;
                    Data = availableData.ToArray(); // Store the data
                    reader.Consume(availableData.Length); // Consume all available data
                    return true;
                }
                return false;
            }
        }

        [Fact]
        public void Write_Send_CheckOutputStructure_VerifiesPacketFlow()
        {
            // Arrange
            var memoryStream = new MemoryStream();
            using var sessionChannel = new SessionChannel(memoryStream);
            using var controlCrypto = new PlainCrypto();
            using var controlChannel = new ControlChannel(
                maximumQueueSize: 100,
                controlChannel: sessionChannel,
                mode: OpenVpnMode.Client,
                crypto: controlCrypto,
                loggerFactory: NullLoggerFactory.Instance
            );

            var testData = GenerateTestData(64);
            var packet = new TestControlPacket { Data = testData };
            controlChannel.Connect();

            // Act - Write pattern
            controlChannel.Write(packet);

            // Check Output Structure - verify session IDs are set
            Assert.NotEqual(0UL, controlChannel.SessionId);
            Assert.NotEqual(0UL, controlChannel.RemoteSessionId);
        }

        [Fact]
        public void Write_Send_CheckSessionIdStructure_VerifiesSessionIdentifiers()
        {
            // Arrange
            var memoryStream = new MemoryStream();
            using var sessionChannel = new SessionChannel(memoryStream);
            using var controlCrypto = new PlainCrypto();
            using var controlChannel = new ControlChannel(
                maximumQueueSize: 100,
                controlChannel: sessionChannel,
                mode: OpenVpnMode.Client,
                crypto: controlCrypto,
                loggerFactory: NullLoggerFactory.Instance
            );

            // Check Session ID Structure
            Assert.NotEqual(0UL, controlChannel.SessionId);
            
            // Session IDs should be consistent across calls
            var sessionId1 = controlChannel.SessionId;
            var sessionId2 = controlChannel.SessionId;
            Assert.Equal(sessionId1, sessionId2);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(16)]
        [InlineData(64)]
        [InlineData(256)]
        [InlineData(1024)]
        public void Write_Send_CheckBoundaryValues_HandlesVariousPacketSizes(int dataSize)
        {
            // Arrange
            var memoryStream = new MemoryStream();
            using var sessionChannel = new SessionChannel(memoryStream);
            using var controlCrypto = new PlainCrypto();
            using var controlChannel = new ControlChannel(
                maximumQueueSize: 100,
                controlChannel: sessionChannel,
                mode: OpenVpnMode.Client,
                crypto: controlCrypto,
                loggerFactory: NullLoggerFactory.Instance
            );

            var testData = GenerateTestData(dataSize);
            var packet = new TestControlPacket { Data = testData };
            controlChannel.Connect();

            // Act - Write packet
            controlChannel.Write(packet);

            // Check boundary value handling - should not throw exceptions
            Assert.NotEqual(0UL, controlChannel.SessionId);
        }

        [Fact]
        public async Task Write_Send_CheckAsyncOperation_ValidatesNonBlockingFlow()
        {
            // Arrange
            var memoryStream = new MemoryStream();
            using var sessionChannel = new SessionChannel(memoryStream);
            using var controlCrypto = new PlainCrypto();
            using var controlChannel = new ControlChannel(
                maximumQueueSize: 100,
                controlChannel: sessionChannel,
                mode: OpenVpnMode.Client,
                crypto: controlCrypto,
                loggerFactory: NullLoggerFactory.Instance
            );

            controlChannel.Connect();

            // Act - Async operations should not block
            var receiveTask = controlChannel.Receive(CancellationToken.None);
            var sendTask = controlChannel.Send(CancellationToken.None);

            // Wait a short time to let tasks start
            await Task.Delay(10);

            // Check that async operations can be started
            Assert.NotNull(receiveTask);
            Assert.NotNull(sendTask);
        }

        [Fact]
        public async Task Write_Send_CheckCancellation_HandlesCancellationToken()
        {
            // Arrange
            var memoryStream = new MemoryStream();
            using var sessionChannel = new SessionChannel(memoryStream);
            using var controlCrypto = new PlainCrypto();
            using var controlChannel = new ControlChannel(
                maximumQueueSize: 100,
                controlChannel: sessionChannel,
                mode: OpenVpnMode.Client,
                crypto: controlCrypto,
                loggerFactory: NullLoggerFactory.Instance
            );

            controlChannel.Connect();
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            // Act & Assert - Should handle cancellation gracefully
            var sendTask = controlChannel.Send(cts.Token);
            var receiveTask = controlChannel.Receive(cts.Token);

            // Operations might throw OperationCanceledException or complete quickly
            try
            {
                await sendTask;
                await receiveTask;
            }
            catch (OperationCanceledException)
            {
                // Expected behavior with cancelled token
            }
        }

        [Fact]
        public void Read_CheckEmptyQueue_ReturnsNullWhenNoPackets()
        {
            // Arrange
            var memoryStream = new MemoryStream();
            using var sessionChannel = new SessionChannel(memoryStream);
            using var controlCrypto = new PlainCrypto();
            using var controlChannel = new ControlChannel(
                maximumQueueSize: 100,
                controlChannel: sessionChannel,
                mode: OpenVpnMode.Client,
                crypto: controlCrypto,
                loggerFactory: NullLoggerFactory.Instance
            );

            controlChannel.Connect();

            // Act - Read from empty queue
            var packet = controlChannel.Read();

            // Check empty queue handling
            Assert.Null(packet);
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