using OpenVpn.Buffers;

namespace OpenVpn.Control.Crypto
{
    internal sealed class PlainCrypto : IControlCrypto
    {
        private readonly Pipe _sendPipe = new();
        private readonly Pipe _receivePipe = new();

        public void Connect()
        {
        }

        public void WriteInput(ReadOnlySpan<byte> data)
        {
            _sendPipe.Write(data);
        }

        public int ReadOutput(Span<byte> data)
        {
            return _sendPipe.Read(data);
        }

        public void WriteOutput(ReadOnlySpan<byte> data)
        {
            _receivePipe.Write(data);
        }

        public int ReadInput(Span<byte> data)
        {
            return _receivePipe.Read(data);
        }

        public void Dispose()
        {
        }
    }
}
