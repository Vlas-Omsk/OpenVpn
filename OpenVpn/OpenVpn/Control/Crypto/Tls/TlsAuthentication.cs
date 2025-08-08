using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Tls;

namespace OpenVpn.Control.Crypto.Tls
{
    internal sealed class TlsAuthentication : Org.BouncyCastle.Tls.TlsAuthentication
    {
        private readonly Certificate _certificate;
        private readonly AsymmetricKeyParameter _privateKey;
        private readonly IList<SignatureAndHashAlgorithm> _supportedSignatureAndHashAlgorithms;

        public TlsAuthentication(
            Certificate certificate,
            AsymmetricKeyParameter privateKey,
            IList<SignatureAndHashAlgorithm> supportedSignatureAndHashAlgorithms
        )
        {
            _certificate = certificate;
            _privateKey = privateKey;
            _supportedSignatureAndHashAlgorithms = supportedSignatureAndHashAlgorithms;
        }

        public Org.BouncyCastle.Tls.TlsCredentials GetClientCredentials(CertificateRequest certificateRequest)
        {
            var signatureAndHashAlgorithm = TlsUtils.SelectSignatureAndHashAlgorithmForKey(
                _privateKey,
                // Can be null before TLS 1.2
                certificateRequest.SupportedSignatureAlgorithms ?? _supportedSignatureAndHashAlgorithms
            );

            return new TlsCredentials(
                _certificate,
                _privateKey,
                signatureAndHashAlgorithm
            );
        }

        public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
        {
        }
    }
}
