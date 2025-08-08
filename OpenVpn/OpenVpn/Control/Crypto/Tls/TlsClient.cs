using OpenVpn.Crypto;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Org.BouncyCastle.X509;

namespace OpenVpn.Control.Crypto.Tls
{
    // Clone of OpenVPN 2.6.13
    internal class TlsClient : AbstractTlsClient
    {
        private readonly X509Certificate _certificate;
        private readonly AsymmetricKeyParameter _privateKey;
        private readonly BcTlsCrypto _crypto;

        private sealed class KeyingMaterialExporter : ITlsKeyingMaterialExporter
        {
            private readonly TlsContext _context;

            public KeyingMaterialExporter(TlsContext context)
            {
                _context = context;
            }

            public byte[] Export(string label, int length)
            {
                return _context.ExportKeyingMaterial(label, null, length);
            }
        }

        public TlsClient(
            X509Certificate certificate,
            AsymmetricKeyParameter privateKey,
            SecureRandom random
        ) : base(new BcTlsCrypto(random))
        {
            _certificate = certificate;
            _privateKey = privateKey;
            _crypto = (BcTlsCrypto)Crypto;
        }

        public bool HandshakeCompleted { get; private set; } = false;
        public CryptoKeys? Keys { get; private set; }

        protected override int[] GetSupportedCipherSuites()
        {
            return [
                CipherSuite.TLS_AES_256_GCM_SHA384,
                CipherSuite.TLS_CHACHA20_POLY1305_SHA256,
                CipherSuite.TLS_AES_128_GCM_SHA256,
                CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384,
                CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
                CipherSuite.TLS_DHE_RSA_WITH_AES_256_GCM_SHA384,
                CipherSuite.TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256,
                CipherSuite.TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
                CipherSuite.TLS_DHE_RSA_WITH_CHACHA20_POLY1305_SHA256,
                CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
                CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
                CipherSuite.TLS_DHE_RSA_WITH_AES_128_GCM_SHA256,
                CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA384,
                CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384,
                CipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA256,
                CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256,
                CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256,
                CipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA256,
                CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA,
                CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA,
                CipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA,
                CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA,
                CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA,
                CipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA
            ];
        }

        public override IDictionary<int, byte[]> GetClientExtensions()
        {
            var extensions = new Dictionary<int, byte[]>();

            extensions[65281] = [0];

            TlsExtensionsUtilities.AddSupportedPointFormatsExtension(extensions, [
                ECPointFormat.uncompressed,
                ECPointFormat.ansiX962_compressed_prime,
                ECPointFormat.ansiX962_compressed_char2
            ]);
            TlsExtensionsUtilities.AddSupportedGroupsExtension(extensions, GetSupportedGroups([]));
            TlsExtensionsUtilities.AddEncryptThenMacExtension(extensions);
            TlsExtensionsUtilities.AddExtendedMasterSecretExtension(extensions);
            TlsExtensionsUtilities.AddSignatureAlgorithmsExtension(extensions, GetSupportedSignatureAlgorithms());
            TlsExtensionsUtilities.AddPskKeyExchangeModesExtension(extensions, GetPskKeyExchangeModes());

            return extensions;
        }

        public override IList<int> GetEarlyKeyShareGroups()
        {
            return [0x001d];
        }

        public override short[] GetPskKeyExchangeModes()
        {
            return [
                PskKeyExchangeMode.psk_dhe_ke
            ];
        }

        protected override IList<SignatureAndHashAlgorithm> GetSupportedSignatureAlgorithms()
        {
            var supportedSignatureAlgorithms = new List<SignatureAndHashAlgorithm>();
            var wantedSignatureAlgorithms = new List<SignatureAndHashAlgorithm>()
            {
                new SignatureAndHashAlgorithm(4, 3), // ecdsa_secp256r1_sha256
                new SignatureAndHashAlgorithm(5, 3), // ecdsa_secp384r1_sha384
                new SignatureAndHashAlgorithm(6, 3), // ecdsa_secp521r1_sha512
                SignatureAndHashAlgorithm.ed25519,
                SignatureAndHashAlgorithm.ed448,
                SignatureAndHashAlgorithm.ecdsa_brainpoolP256r1tls13_sha256,
                SignatureAndHashAlgorithm.ecdsa_brainpoolP384r1tls13_sha384,
                SignatureAndHashAlgorithm.ecdsa_brainpoolP512r1tls13_sha512,
                SignatureAndHashAlgorithm.rsa_pss_pss_sha256,
                SignatureAndHashAlgorithm.rsa_pss_pss_sha384,
                SignatureAndHashAlgorithm.rsa_pss_pss_sha512,
                SignatureAndHashAlgorithm.rsa_pss_rsae_sha256,
                SignatureAndHashAlgorithm.rsa_pss_rsae_sha384,
                new SignatureAndHashAlgorithm(8, 6), // rsa_pss_rsae_sha512
                new SignatureAndHashAlgorithm(4, 1), // rsa_pkcs1_sha256
                new SignatureAndHashAlgorithm(5, 1), // rsa_pkcs1_sha384
                new SignatureAndHashAlgorithm(6, 1), // rsa_pkcs1_sha512
                new SignatureAndHashAlgorithm(3, 3), // SHA224 ECDSA
                new SignatureAndHashAlgorithm(3, 1), // SHA224 RSA
                new SignatureAndHashAlgorithm(3, 2), // SHA224 DSA
                new SignatureAndHashAlgorithm(4, 2), // SHA256 DSA
                new SignatureAndHashAlgorithm(5, 2), // SHA384 DSA
                new SignatureAndHashAlgorithm(6, 2), // SHA512 DSA
            };

            foreach (var wantedSignatureAlgorithm in wantedSignatureAlgorithms)
            {
                TlsUtilities.AddIfSupported(supportedSignatureAlgorithms, Crypto, wantedSignatureAlgorithm);
            }

            return supportedSignatureAlgorithms;
        }

        protected override IList<int> GetSupportedGroups(IList<int> namedGroupRoles)
        {
            var supportedGroups = new List<int>();
            var wantedGroups = new int[]
            {
                0x001d, // x25519
                0x0017, // secp256r1
                0x001e, // x448
                0x0019, // secp521r1
                0x0018, // secp384r1
                0x0100, // ffdhe2048
                0x0101, // ffdhe3072
                0x0102, // ffdhe4096
                0x0103, // ffdhe6144
                0x0104, // ffdhe8192
            };

            TlsUtilities.AddIfSupported(supportedGroups, Crypto, wantedGroups);

            return supportedGroups;
        }

        protected override ProtocolVersion[] GetSupportedVersions()
        {
            return [ProtocolVersion.TLSv13, ProtocolVersion.TLSv12];
        }

        public override ProtocolVersion[] GetProtocolVersions()
        {
            return [ProtocolVersion.TLSv13, ProtocolVersion.TLSv12];
        }

        public override Org.BouncyCastle.Tls.TlsAuthentication GetAuthentication()
        {
            var certificate = TlsUtils.CreateCertificateFromX509Certificate(
                _crypto,
                _certificate,
                TlsUtilities.IsTlsV13(m_context)
            );

            return new TlsAuthentication(
                certificate,
                _privateKey,
                GetSupportedSignatureAlgorithms()
            );
        }

        public override void NotifyHandshakeComplete()
        {
            Keys = CryptoKeys.DeriveFromTls(new KeyingMaterialExporter(m_context));

            HandshakeCompleted = true;
        }
    }
}
