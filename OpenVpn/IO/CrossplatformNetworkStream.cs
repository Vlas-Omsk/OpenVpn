using System.Net.Sockets;
using PinkSystem.Net.Sockets;

namespace OpenVpn.IO
{
    /// <summary>
    /// Cross-platform socket stream wrapper that provides consistent behavior:
    /// - Returns 0 if no data is available in the stream
    /// </summary>
    internal sealed class CrossplatformNetworkStream : Stream
    {
        private readonly ISocket _socket;
        private readonly bool _ownsSocket;
        private bool _disposed;

        public CrossplatformNetworkStream(ISocket socket, bool ownsSocket = false)
        {
            _socket = socket;
            _ownsSocket = ownsSocket;
        }

        public ISocket Socket => _socket;
        public override bool CanRead => !_disposed && _socket.Connected;
        public override bool CanSeek => false;
        public override bool CanWrite => !_disposed && _socket.Connected;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0)
                return 0;

            ThrowIfDisposed();

            if (!IsSocketConnected())
                throw new IOException("Socket is not connected");

            if (!HasDataAvailable())
                return 0;

            var bytesRead = await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);

            if (!IsSocketConnected())
                throw new IOException("Socket is not connected");

            return bytesRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override int Read(Span<byte> buffer)
        {
            return ReadAsync(buffer.ToArray(), 0, buffer.Length, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override int ReadByte()
        {
            var buffer = new byte[1];
            var bytesRead = Read(buffer, 0, 1);

            return bytesRead == 0 ? -1 : buffer[0];
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0)
                return;

            ThrowIfDisposed();

            if (!IsSocketConnected())
                throw new IOException("Socket is not connected");

            await _socket.SendAsync(buffer, SocketFlags.None, cancellationToken);

            if (!IsSocketConnected())
                throw new IOException("Socket is not connected");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            WriteAsync(buffer.ToArray(), 0, buffer.Length, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override void WriteByte(byte value)
        {
            Write([value], 0, 1);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override void Flush()
        {
            FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Seek is not supported on socket streams");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("SetLength is not supported on socket streams");
        }

        private bool IsSocketConnected()
        {
            try
            {
                if (_disposed || _socket == null)
                    return false;

                // On Linux, socket.Connected might not be reliable immediately after close
                // Use Poll to check the actual state
                return _socket.Connected && !_socket.Poll(TimeSpan.Zero, SelectMode.SelectError);
            }
            catch
            {
                return false;
            }
        }

        private bool HasDataAvailable()
        {
            try
            {
                if (_disposed || _socket == null)
                    return false;

                if (_socket.Available > 0)
                    return true;

                // Use Poll with zero timeout to check if data is ready to read
                // This is more reliable on Linux than just checking Available
                var dataReady = _socket.Poll(TimeSpan.Zero, SelectMode.SelectRead);

                // If Poll returns true but no data available, connection might be closing
                if (dataReady && _socket.Available == 0)
                    return IsSocketConnected();

                return dataReady;
            }
            catch
            {
                return false;
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    if (_ownsSocket)
                    {
                        _socket.Shutdown(SocketShutdown.Both);
                        _socket.Close(TimeSpan.Zero);
                    }
                }
                catch
                {
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                try
                {
                    if (_ownsSocket)
                    {
                        _socket.Shutdown(SocketShutdown.Both);
                        _socket.Close(TimeSpan.Zero);
                    }
                }
                catch
                {
                }

                _disposed = true;
            }

            await base.DisposeAsync();
        }
    }
}
