using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Org.BouncyCastle.X509;

namespace OpenVpn.Control.Crypto.Tls
{
    internal class TlsUtils
    {
        public static Certificate CreateCertificateFromX509Certificate(
            BcTlsCrypto crypto,
            X509Certificate certificate,
            bool isTls13
        )
        {
            var certificateEntries = new[]
            {
                new CertificateEntry(
                    new BcTlsCertificate(crypto, certificate.CertificateStructure),
                    null
                )
            };

            var requestContext = (byte[]?)null;

            // Required for TLS 1.3 (https://github.com/bcgit/bc-csharp/blob/2383e934568aacb9638ed6f4cbf7ded6cb4f9711/crypto/src/tls/Certificate.cs#L118)
            if (isTls13)
                requestContext = [];

            return new Certificate(requestContext, certificateEntries);
        }

        public static SignatureAndHashAlgorithm SelectSignatureAndHashAlgorithmForKey(
            AsymmetricKeyParameter privateKey,
            IList<SignatureAndHashAlgorithm> supportedAlgorithms
        )
        {
            switch (privateKey)
            {
                case RsaPrivateCrtKeyParameters:
                case RsaKeyParameters _ when privateKey.IsPrivate:
                    return SelectBestHashAlgorithm(SignatureAlgorithm.rsa, supportedAlgorithms);
                case DsaPrivateKeyParameters:
                    return SelectBestHashAlgorithm(SignatureAlgorithm.dsa, supportedAlgorithms);
                case ECPrivateKeyParameters ecPrivateKey:
                    return SelectHashAlgorithmForEcKey(ecPrivateKey, supportedAlgorithms);
                case Ed25519PrivateKeyParameters:
                    return SelectBestHashAlgorithm(SignatureAlgorithm.ed25519, supportedAlgorithms);
                case Ed448PrivateKeyParameters:
                    return SelectBestHashAlgorithm(SignatureAlgorithm.ed448, supportedAlgorithms);
                default:
                    throw new NotSupportedException("Private key not supported");
            }
        }

        private static SignatureAndHashAlgorithm SelectHashAlgorithmForEcKey(
            ECPrivateKeyParameters ecPrivateKey,
            IList<SignatureAndHashAlgorithm> supportedAlgorithms
        )
        {
            var ecdsaAlgorithms = supportedAlgorithms.Where(x => x.Signature == SignatureAlgorithm.ecdsa);

            var result = ecPrivateKey.Parameters.N.BitLength switch
            {
                256 => ecdsaAlgorithms.FirstOrDefault(x => x.Hash == HashAlgorithm.sha256),
                384 => ecdsaAlgorithms.FirstOrDefault(x => x.Hash == HashAlgorithm.sha384),
                512 => ecdsaAlgorithms.FirstOrDefault(x => x.Hash == HashAlgorithm.sha512),
                _ => throw new NotSupportedException("Bit length not supported")
            };

            if (result == null)
                throw new NotSupportedException("Bit length not supported");

            return result;
        }

        private static SignatureAndHashAlgorithm SelectBestHashAlgorithm(
            short signatureAlgorithm,
            IList<SignatureAndHashAlgorithm> supportedAlgorithms
        )
        {
            var hashPreferences = new short[]
            {
                HashAlgorithm.sha512,
                HashAlgorithm.sha384,
                HashAlgorithm.sha256,
                HashAlgorithm.sha224,
                HashAlgorithm.sha1,
                HashAlgorithm.md5
            };

            foreach (var preferredHash in hashPreferences)
            {
                var algorithm = supportedAlgorithms.FirstOrDefault(x =>
                    x.Signature == signatureAlgorithm && x.Hash == preferredHash
                );

                if (algorithm != null)
                    return algorithm;
            }

            throw new NotSupportedException("Private key not supported");
        }

        public static string GetSignatureAndHashAlgorithmName(SignatureAndHashAlgorithm signatureAndHashAlgorithm)
        {
            return signatureAndHashAlgorithm.Signature switch
            {
                SignatureAlgorithm.rsa => signatureAndHashAlgorithm.Hash switch
                {
                    HashAlgorithm.sha1 => "SHA1withRSA",
                    HashAlgorithm.sha224 => "SHA224withRSA",
                    HashAlgorithm.sha256 => "SHA256withRSA",
                    HashAlgorithm.sha384 => "SHA384withRSA",
                    HashAlgorithm.sha512 => "SHA512withRSA",
                    _ => throw new NotSupportedException("SignatureAndHashAlgorithm not supported")
                },
                SignatureAlgorithm.dsa => signatureAndHashAlgorithm.Hash switch
                {
                    HashAlgorithm.sha1 => "SHA1withDSA",
                    HashAlgorithm.sha224 => "SHA224withDSA",
                    HashAlgorithm.sha256 => "SHA256withDSA",
                    _ => throw new NotSupportedException("SignatureAndHashAlgorithm not supported")
                },
                SignatureAlgorithm.ecdsa => signatureAndHashAlgorithm.Hash switch
                {
                    HashAlgorithm.sha1 => "SHA1withECDSA",
                    HashAlgorithm.sha224 => "SHA224withECDSA",
                    HashAlgorithm.sha256 => "SHA256withECDSA",
                    HashAlgorithm.sha384 => "SHA384withECDSA",
                    HashAlgorithm.sha512 => "SHA512withECDSA",
                    _ => throw new NotSupportedException("SignatureAndHashAlgorithm not supported")
                },
                _ => "SHA256withRSA"
            };
        }
    }
}
