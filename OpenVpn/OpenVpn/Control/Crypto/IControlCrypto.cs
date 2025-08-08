namespace OpenVpn.Control.Crypto
{
    internal interface IControlCrypto : IDisposable
    {
        void Connect();
        void WriteInput(ReadOnlySpan<byte> data);
        int ReadOutput(Span<byte> data);
        void WriteOutput(ReadOnlySpan<byte> data);
        int ReadInput(Span<byte> data);
    }
}
