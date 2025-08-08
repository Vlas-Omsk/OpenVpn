using OpenVpn.IO;
using PinkSystem.Net.Sockets;
using System.Net.Sockets;
using System.Net;

namespace OpenVpn.Tests
{
    public class CrossplatformNetworkStreamTests
    {
        private class MockSocket : ISocket
        {
            private readonly Queue<byte> _readBuffer = new();
            private readonly Queue<byte> _writeBuffer = new();
            private bool _connected = true;
            private int _available = 0;

            public bool Connected => _connected;
            public int Available => _available;

            public bool Blocking { get; set; } = false;
            public bool DontFragment { get; set; } = false;
            public bool DualMode { get; set; } = false;
            public bool EnableBroadcast { get; set; } = false;
            public bool NoDelay { get; set; } = false;
            public LingerOption LingerState { get; set; } = new LingerOption(false, 0);
            public bool ExclusiveAddressUse { get; set; } = false;
            public bool IsBound { get; set; } = false;
            public bool MulticastLoopback { get; set; } = false;
            public short Ttl { get; set; } = 64;

            public void SetConnected(bool connected) => _connected = connected;
            public void SetAvailable(int available) => _available = available;

            public void AddToReadBuffer(params byte[] data)
            {
                foreach (var b in data)
                    _readBuffer.Enqueue(b);
                _available = _readBuffer.Count;
            }

            public byte[] GetWrittenData()
            {
                var result = _writeBuffer.ToArray();
                _writeBuffer.Clear();
                return result;
            }

            public ValueTask<int> ReceiveAsync(Memory<byte> buffer, SocketFlags socketFlags, CancellationToken cancellationToken)
            {
                var bytesToRead = Math.Min(buffer.Length, _readBuffer.Count);
                for (int i = 0; i < bytesToRead; i++)
                {
                    buffer.Span[i] = _readBuffer.Dequeue();
                }
                _available = _readBuffer.Count;
                return ValueTask.FromResult(bytesToRead);
            }

            public ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, SocketFlags socketFlags, CancellationToken cancellationToken)
            {
                foreach (var b in buffer.Span)
                    _writeBuffer.Enqueue(b);
                return ValueTask.FromResult(buffer.Length);
            }

            public bool Poll(TimeSpan timeout, SelectMode mode)
            {
                return mode switch
                {
                    SelectMode.SelectRead => _readBuffer.Count > 0,
                    SelectMode.SelectError => !_connected,
                    _ => false
                };
            }

            public void Shutdown(SocketShutdown how) { _connected = false; }
            public void Close(TimeSpan timeout) { _connected = false; }
            public void Dispose() { }

            // Minimal implementations for unused methods
            public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, object optionValue) { }
            public object? GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName) => null;
            public void Bind(EndPoint localEP) { }
            public void BindToDevice(string deviceName) { }
            public ValueTask ConnectAsync(EndPoint remoteEP, CancellationToken cancellationToken) => ValueTask.CompletedTask;
            public ValueTask DisconnectAsync(bool reuseSocket, CancellationToken cancellationToken) => ValueTask.CompletedTask;
            public ValueTask<ISocket> AcceptAsync(CancellationToken cancellationToken) => ValueTask.FromResult<ISocket>(new MockSocket());
            public void Listen() { }
            public ValueTask<SocketReceiveFromResult> ReceiveFromAsync(Memory<byte> buffer, SocketFlags socketFlags, EndPoint remoteEP, CancellationToken cancellationToken) =>
                ValueTask.FromResult(default(SocketReceiveFromResult));
            public ValueTask<int> SendToAsync(ReadOnlyMemory<byte> buffer, SocketFlags socketFlags, EndPoint remoteEP, CancellationToken cancellationToken) =>
                ValueTask.FromResult(buffer.Length);
        }

        [Fact]
        public void CanSeek_Always_ReturnsFalse()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            Assert.False(stream.CanSeek);
        }

        [Fact]
        public void Length_ThrowsNotSupportedException()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            Assert.Throws<NotSupportedException>(() => stream.Length);
        }

        [Fact]
        public void Position_Get_ThrowsNotSupportedException()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            Assert.Throws<NotSupportedException>(() => stream.Position);
        }

        [Fact]
        public void Position_Set_ThrowsNotSupportedException()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        }

        [Fact]
        public void Seek_ThrowsNotSupportedException()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        }

        [Fact]
        public void SetLength_ThrowsNotSupportedException()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            Assert.Throws<NotSupportedException>(() => stream.SetLength(100));
        }

        [Fact]
        public async Task ReadAsync_WithDataAvailable_ReturnsData()
        {
            var mockSocket = new MockSocket();
            var testData = new byte[] { 1, 2, 3, 4, 5 };
            mockSocket.AddToReadBuffer(testData);
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var buffer = new byte[10];

            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

            Assert.Equal(5, bytesRead);
            Assert.Equal(testData, buffer[..5]);
        }

        [Fact]
        public async Task ReadAsync_NoDataAvailable_ReturnsZero()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var buffer = new byte[10];

            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public async Task ReadAsync_SocketNotConnected_ThrowsIOException()
        {
            var mockSocket = new MockSocket();
            mockSocket.SetConnected(false);
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var buffer = new byte[10];

            await Assert.ThrowsAsync<IOException>(() => stream.ReadAsync(buffer, 0, buffer.Length));
        }

        [Fact]
        public async Task ReadAsync_EmptyBuffer_ReturnsZero()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var buffer = new byte[0];

            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public void Read_WithDataAvailable_ReturnsData()
        {
            var mockSocket = new MockSocket();
            var testData = new byte[] { 0x42, 0x43, 0x44 };
            mockSocket.AddToReadBuffer(testData);
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var buffer = new byte[10];

            var bytesRead = stream.Read(buffer, 0, buffer.Length);

            Assert.Equal(3, bytesRead);
            Assert.Equal(testData, buffer[..3]);
        }

        [Fact]
        public void Read_NoDataAvailable_ReturnsData()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var buffer = new byte[10];

            var bytesRead = stream.Read(buffer, 0, buffer.Length);

            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public void Read_SocketNotConnected_ThrowsIOException()
        {
            var mockSocket = new MockSocket();
            mockSocket.SetConnected(false);
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var buffer = new byte[10];

            Assert.Throws<IOException>(() => stream.Read(buffer, 0, buffer.Length));
        }

        [Fact]
        public void Read_EmptyBuffer_ReturnsZero()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var buffer = new byte[0];

            var bytesRead = stream.Read(buffer, 0, buffer.Length);

            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public async Task WriteAsync_ValidData_WritesToSocket()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var testData = new byte[] { 1, 2, 3, 4, 5 };

            await stream.WriteAsync(testData, 0, testData.Length);

            Assert.Equal(testData, mockSocket.GetWrittenData());
        }

        [Fact]
        public async Task WriteAsync_EmptyData_DoesNothing()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var testData = new byte[0];

            await stream.WriteAsync(testData, 0, testData.Length);

            Assert.Empty(mockSocket.GetWrittenData());
        }

        [Fact]
        public async Task WriteAsync_SocketNotConnected_ThrowsIOException()
        {
            var mockSocket = new MockSocket();
            mockSocket.SetConnected(false);
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var testData = new byte[] { 1, 2, 3 };

            await Assert.ThrowsAsync<IOException>(() => stream.WriteAsync(testData, 0, testData.Length));
        }

        [Fact]
        public void Write_ValidData_WritesToSocket()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var testData = new byte[] { 0xAA, 0xBB, 0xCC };

            stream.Write(testData, 0, testData.Length);

            Assert.Equal(testData, mockSocket.GetWrittenData());
        }

        [Fact]
        public void Write_EmptyData_WritesToSocket()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var testData = new byte[0];

            stream.Write(testData, 0, testData.Length);

            Assert.Empty(mockSocket.GetWrittenData());
        }

        [Fact]
        public void Write_SocketNotConnected_ThrowsIOException()
        {
            var mockSocket = new MockSocket();
            mockSocket.SetConnected(false);
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var testData = new byte[] { 1, 2, 3 };

            Assert.Throws<IOException>(() => stream.Write(testData, 0, testData.Length));
        }

        [Fact]
        public async Task FlushAsync_DoesNotThrow()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            await stream.FlushAsync();
        }

        [Fact]
        public void Flush_DoesNotThrow()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            stream.Flush();
        }

        [Fact]
        public void CanRead_ConnectedSocket_ReturnsTrue()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            Assert.True(stream.CanRead);
        }

        [Fact]
        public void CanRead_DisconnectedSocket_ReturnsFalse()
        {
            var mockSocket = new MockSocket();
            mockSocket.SetConnected(false);
            using var stream = new CrossplatformNetworkStream(mockSocket);

            Assert.False(stream.CanRead);
        }

        [Fact]
        public void CanWrite_ConnectedSocket_ReturnsTrue()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            Assert.True(stream.CanWrite);
        }

        [Fact]
        public void CanWrite_DisconnectedSocket_ReturnsFalse()
        {
            var mockSocket = new MockSocket();
            mockSocket.SetConnected(false);
            using var stream = new CrossplatformNetworkStream(mockSocket);

            Assert.False(stream.CanWrite);
        }

        [Fact]
        public void Dispose_WithOwnedSocket_DisposesSocket()
        {
            var mockSocket = new MockSocket();
            var stream = new CrossplatformNetworkStream(mockSocket, ownsSocket: true);

            stream.Dispose();

            Assert.False(mockSocket.Connected);
        }

        [Fact]
        public void Dispose_WithoutOwnedSocket_DoesNotDisposeSocket()
        {
            var mockSocket = new MockSocket();
            var stream = new CrossplatformNetworkStream(mockSocket, ownsSocket: false);

            stream.Dispose();

            Assert.True(mockSocket.Connected);
        }

        [Fact]
        public async Task DisposeAsync_WithOwnedSocket_DisposesSocket()
        {
            var mockSocket = new MockSocket();
            var stream = new CrossplatformNetworkStream(mockSocket, ownsSocket: true);

            await stream.DisposeAsync();

            Assert.False(mockSocket.Connected);
        }

        [Fact]
        public async Task DisposeAsync_WithoutOwnedSocket_DoesNotDisposeSocket()
        {
            var mockSocket = new MockSocket();
            var stream = new CrossplatformNetworkStream(mockSocket, ownsSocket: false);

            await stream.DisposeAsync();

            Assert.True(mockSocket.Connected);
        }

        [Fact]
        public async Task ReadAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            var mockSocket = new MockSocket();
            var stream = new CrossplatformNetworkStream(mockSocket);

            var buffer = new byte[10];

            stream.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => stream.ReadAsync(buffer, 0, buffer.Length));
        }

        [Fact]
        public async Task WriteAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            var mockSocket = new MockSocket();
            var stream = new CrossplatformNetworkStream(mockSocket);

            var data = new byte[] { 1, 2, 3 };

            stream.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => stream.WriteAsync(data, 0, data.Length));
        }

        [Fact]
        public void Read_AfterDispose_ThrowsObjectDisposedException()
        {
            var mockSocket = new MockSocket();
            var stream = new CrossplatformNetworkStream(mockSocket);

            var buffer = new byte[10];

            stream.Dispose();

            Assert.Throws<ObjectDisposedException>(() => stream.Read(buffer, 0, buffer.Length));
        }

        [Fact]
        public void Write_AfterDispose_ThrowsObjectDisposedException()
        {
            var mockSocket = new MockSocket();
            var stream = new CrossplatformNetworkStream(mockSocket);

            var data = new byte[] { 1, 2, 3 };

            stream.Dispose();

            Assert.Throws<ObjectDisposedException>(() => stream.Write(data, 0, data.Length));
        }

        [Fact]
        public async Task ReadWriteRoundTrip_PreservesData()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var originalData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

            // Write data
            await stream.WriteAsync(originalData, 0, originalData.Length);
            var writtenData = mockSocket.GetWrittenData();

            // Simulate receiving the same data
            mockSocket.AddToReadBuffer(writtenData);

            // Read data back
            var readBuffer = new byte[10];
            var bytesRead = await stream.ReadAsync(readBuffer, 0, readBuffer.Length);

            Assert.Equal(originalData.Length, bytesRead);
            Assert.Equal(originalData, readBuffer[..bytesRead]);
        }

        [Fact]
        public async Task LargeDataTransfer_HandlesCorrectly()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var largeData = new byte[10000];

            for (int i = 0; i < largeData.Length; i++)
                largeData[i] = (byte)(i % 256);

            await stream.WriteAsync(largeData, 0, largeData.Length);
            var writtenData = mockSocket.GetWrittenData();

            Assert.Equal(largeData, writtenData);
        }

        [Fact]
        public async Task ConcurrentOperations_HandleCorrectly()
        {
            var mockSocket = new MockSocket();
            using var stream = new CrossplatformNetworkStream(mockSocket);

            var data1 = new byte[] { 1, 2, 3 };
            var data2 = new byte[] { 4, 5, 6 };

            // Concurrent write operations
            var writeTask1 = stream.WriteAsync(data1, 0, data1.Length);
            var writeTask2 = stream.WriteAsync(data2, 0, data2.Length);

            await Task.WhenAll(writeTask1, writeTask2);

            var writtenData = mockSocket.GetWrittenData();
            Assert.Equal(6, writtenData.Length);
        }
    }
}