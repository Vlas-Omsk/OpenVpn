namespace OpenVpn.Buffers
{
    internal sealed class Pipe
    {
        private byte[] _buffer;
        private int _writePosition;
        private int _readPosition;
        private int _capacity;

        public Pipe(int initialCapacity = Buffer.DefaultSize)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(initialCapacity, 0, nameof(initialCapacity));

            _capacity = initialCapacity;
            _buffer = new byte[_capacity];
            _writePosition = 0;
            _readPosition = 0;
        }

        /// <summary>
        /// Gets the number of bytes available for reading
        /// </summary>
        public int Available => _writePosition - _readPosition;

        /// <summary>
        /// Gets a read-only span of available data without consuming it
        /// </summary>
        /// <returns>ReadOnlySpan of available data</returns>
        public ReadOnlySpan<byte> AvailableSpan => _buffer.AsSpan(_readPosition, _writePosition - _readPosition);

        /// <summary>
        /// Gets a read-only memory of available data without consuming it
        /// </summary>
        /// <returns>ReadOnlyMemory of available data</returns>
        public ReadOnlyMemory<byte> AvailableMemory => _buffer.AsMemory(_readPosition, _writePosition - _readPosition);

        /// <summary>
        /// Gets the current capacity of the internal buffer
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Writes data to the pipe
        /// </summary>
        /// <param name="data">Data to write</param>
        /// <returns>Number of bytes written</returns>
        public int Write(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty)
                return 0;

            CompactBufferIfNeeded(data.Length);
            EnsureCapacity(data.Length);

            data.CopyTo(_buffer.AsSpan(_writePosition));

            _writePosition += data.Length;

            return data.Length;
        }

        /// <summary>
        /// Reads data from the pipe without consuming it
        /// </summary>
        /// <param name="buffer">Destination buffer</param>
        /// <returns>Number of bytes read</returns>
        public int Read(Span<byte> buffer)
        {
            var readedBytes = Peek(buffer);

            Consume(readedBytes);

            return readedBytes;
        }

        /// <summary>
        /// Peeks at data in the pipe without advancing read position
        /// </summary>
        /// <param name="buffer">Destination buffer</param>
        /// <returns>Number of bytes peeked</returns>
        public int Peek(Span<byte> buffer, int skip = 0)
        {
            if (buffer.IsEmpty)
                return 0;

            var availableBytes = _writePosition - (_readPosition + skip);

            if (availableBytes == 0)
                return 0;

            var bytesToRead = Math.Min(buffer.Length, availableBytes);

            _buffer.AsSpan(_readPosition + skip, bytesToRead).CopyTo(buffer);

            return bytesToRead;
        }

        /// <summary>
        /// Marks the specified number of bytes as consumed, advancing the read position, but not clearing
        /// </summary>
        /// <param name="count">Number of bytes to consume</param>
        public void Consume(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            var availableBytes = _writePosition - _readPosition;

            if (count > availableBytes)
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot consume more bytes than available");

            _readPosition += count;

            // Reset positions if buffer is empty
            if (_readPosition == _writePosition)
            {
                _readPosition = 0;
                _writePosition = 0;
            }
        }

        /// <summary>
        /// Clears all data from the pipe
        /// </summary>
        public void Clear()
        {
            _readPosition = 0;
            _writePosition = 0;
        }

        /// <summary>
        /// Compacts the buffer by moving unread data to the beginning if beneficial
        /// </summary>
        private void CompactBufferIfNeeded(int additionalBytes)
        {
            // Only compact if read position is significant and we need space
            if (_readPosition <= _capacity / 4 || _writePosition + additionalBytes <= _capacity)
                return;

            var availableBytes = _writePosition - _readPosition;

            if (availableBytes > 0)
                System.Buffer.BlockCopy(_buffer, _readPosition, _buffer, 0, availableBytes);

            _writePosition = availableBytes;
            _readPosition = 0;
        }

        /// <summary>
        /// Ensures the buffer has enough capacity
        /// </summary>
        private void EnsureCapacity(int additionalBytes)
        {
            var requiredCapacity = _writePosition + additionalBytes;

            if (requiredCapacity <= _capacity)
                return;

            // Grow by 1.5x or required size, whichever is larger
            var newCapacity = Math.Max(_capacity + _capacity / 2, requiredCapacity);

            var newBuffer = new byte[newCapacity];

            if (_writePosition > 0)
                System.Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _writePosition);

            _buffer = newBuffer;
            _capacity = newCapacity;
        }
    }
}
