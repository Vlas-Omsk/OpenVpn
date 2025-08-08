using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using PinkSystem;

namespace OpenVpn.Control.Crypto.Tls
{
    // TlsCredentials should implement TlsCredentialedSigner (https://github.com/bcgit/bc-csharp/blob/2383e934568aacb9638ed6f4cbf7ded6cb4f9711/crypto/src/tls/TlsUtilities.cs#L5244)
    internal sealed class TlsCredentials : Org.BouncyCastle.Tls.TlsCredentials, TlsCredentialedSigner, TlsSigner
    {
        private readonly AsymmetricKeyParameter _privateKey;
        private readonly string _signerName;

        private sealed class OpenVpnTlsStreamSigner : TlsStreamSigner
        {
            private readonly ISigner _signer;
            private readonly MemoryStream _buffer = new();

            public OpenVpnTlsStreamSigner(ISigner signer)
            {
                _signer = signer;
            }

            public Stream Stream => _buffer;

            public byte[] GetSignature()
            {
                var data = _buffer.ToReadOnlyMemory();

                _signer.BlockUpdate(data.Span);

                return _signer.GenerateSignature();
            }
        }

        public TlsCredentials(
            Certificate certificate,
            AsymmetricKeyParameter privateKey,
            SignatureAndHashAlgorithm signatureAndHashAlgorithm
        )
        {
            Certificate = certificate;
            _privateKey = privateKey;
            SignatureAndHashAlgorithm = signatureAndHashAlgorithm;

            _signerName = TlsUtils.GetSignatureAndHashAlgorithmName(SignatureAndHashAlgorithm);
        }

        public Certificate Certificate { get; }
        public SignatureAndHashAlgorithm SignatureAndHashAlgorithm { get; }

        public byte[] GenerateRawSignature(byte[] hash)
        {
            var signer = SignerUtilities.GetSigner(_signerName);

            signer.Init(true, _privateKey);
            signer.BlockUpdate(hash, 0, hash.Length);

            return signer.GenerateSignature();
        }

        public byte[] GenerateRawSignature(SignatureAndHashAlgorithm algorithm, byte[] hash)
        {
            var signerName = TlsUtils.GetSignatureAndHashAlgorithmName(algorithm);
            var signer = SignerUtilities.GetSigner(signerName);

            signer.Init(true, _privateKey);
            signer.BlockUpdate(hash, 0, hash.Length);

            return signer.GenerateSignature();
        }

        public TlsStreamSigner GetStreamSigner()
        {
            var signer = SignerUtilities.GetSigner(_signerName);

            signer.Init(true, _privateKey);

            return new OpenVpnTlsStreamSigner(signer);
        }

        public TlsStreamSigner GetStreamSigner(SignatureAndHashAlgorithm algorithm)
        {
            var signerName = TlsUtils.GetSignatureAndHashAlgorithmName(algorithm);
            var signer = SignerUtilities.GetSigner(signerName);

            signer.Init(true, _privateKey);

            return new OpenVpnTlsStreamSigner(signer);
        }
    }
}
