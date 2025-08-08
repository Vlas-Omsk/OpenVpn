namespace OpenVpn.Configuration
{
    public sealed class OpenVpnTlsCryptControlWrapper : IOpenVpnControlWrapper
    {
        public required byte[] StaticKey { get; init; }
    }
}
