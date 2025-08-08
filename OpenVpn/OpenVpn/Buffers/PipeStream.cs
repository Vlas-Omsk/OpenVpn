namespace OpenVpn.Buffers
{
    internal sealed class PipeStream : Stream
    {
        private readonly Pipe _pipe;

        public PipeStream(Pipe pipe)
        {
            _pipe = pipe;
        }

        public override long Position { get; set; } = 0;
        public override long Length => _pipe.Available;
        public override bool CanWrite { get; } = true;
        public override bool CanRead { get; } = true;
        public override bool CanSeek { get; } = false;

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _pipe.Read(new Span<byte>(buffer, offset, count));
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _pipe.Write(new ReadOnlySpan<byte>(buffer, offset, count));
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}
