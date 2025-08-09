using OpenVpn.Crypto;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Security;

namespace OpenVpn.Data.Crypto
{
    internal static class DataCrypto
    {
        public static IDataCrypto Create(
            string cipherName,
            string? macName,
            CryptoKeys keys,
            OpenVpnMode mode,
            bool epochFormat,
            SecureRandom random
        )
        {
            macName = macName?.ToUpper();

            Func<IMac>? macFactory;

            if (macName == null)
            {
                macFactory = null;
            }
            else if (macName == "SHA512")
            {
                macFactory = () => new HMac(new Sha512Digest());
            }
            else if (macName == "SHA384")
            {
                macFactory = () => new HMac(new Sha384Digest());
            }
            else if (macName == "SHA256")
            {
                macFactory = () => new HMac(new Sha256Digest());
            }
            else if (macName == "SHA1")
            {
                macFactory = () => new HMac(new Sha1Digest());
            }
            else
            {
                throw new NotSupportedException("Mac not supported");
            }

            cipherName = cipherName.ToUpper();

            var parts = cipherName.Split('-');

            Func<IBlockCipher> cipherFactory;

            if (parts[0] == "AES")
            {
                cipherFactory = () => new AesEngine();

                if (parts.Length != 3)
                    throw new FormatException("Invalid aes format");

                var keySize = int.Parse(parts[1]) / 8;

                return parts[2] switch
                {
                    "GCM" => AeadCrypto.CreateGcm(
                        keys,
                        cipherFactory,
                        keySize,
                        ivSize: 12,
                        mode,
                        epochFormat
                    ),
                    "CBC" => BufferedCrypto.CreateCbc(
                        keys,
                        cipherFactory,
                        keySize,
                        ivSize: 16,
                        macFactory,
                        mode,
                        random
                    ),
                    "CTR" => BufferedCrypto.CreateCtr(
                        keys,
                        cipherFactory,
                        keySize,
                        ivSize: 16,
                        macFactory,
                        mode,
                        random
                    ),
                    _ => throw new NotSupportedException()
                };
            }
            else if (parts[0] == "BF")
            {
                cipherFactory = () => new BlowfishEngine();

                if (parts.Length != 2)
                    throw new FormatException("Invalid blowfish format");

                return parts[1] switch
                {
                    "CBC" => BufferedCrypto.CreateCbc(
                        keys,
                        cipherFactory,
                        keySize: 16,
                        ivSize: 8,
                        macFactory,
                        mode,
                        random
                    ),
                    _ => throw new NotSupportedException()
                };
            }
            else if (
                parts[0] == "PLAIN" ||
                parts[0] == "NONE"
            )
            {
                return new PlainCrypto();
            }
            else
            {
                throw new NotSupportedException("Cipher not supported");
            }
        }
    }
}
