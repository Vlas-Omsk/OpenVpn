using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;

namespace OpenVpn.Configuration
{
    public sealed class OpenVpnTlsControlCrypto : IOpenVpnControlCrypto
    {
        public required X509Certificate Certificate { get; init; }
        public required AsymmetricKeyParameter PrivateKey { get; init; }
        public bool UseKeyMaterialExporters { get; init; } = true;
    }
}
