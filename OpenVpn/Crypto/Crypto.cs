using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Security;

namespace OpenVpn.Crypto
{
    internal static class Crypto
    {
        public static ICrypto Create(
            string cipherName,
            CryptoKeys keys,
            OpenVpnMode mode,
            bool epochFormat,
            SecureRandom random
        )
        {
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
                    "GCM" => new GcmCrypto(
                        keys,
                        cipherFactory,
                        keySize,
                        ivSize: 12,
                        mode,
                        epochFormat
                    ),
                    "CBC" => new CbcCrypto(
                        keys,
                        cipherFactory,
                        keySize,
                        ivSize: 16,
                        mode,
                        random
                    ),
                    "CTR" => new CtrCrypto(
                        keys,
                        cipherFactory,
                        keySize,
                        ivSize: 16,
                        mode
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
                    "CBC" => new CbcCrypto(
                        keys,
                        cipherFactory,
                        keySize: 16,
                        ivSize: 8,
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
                throw new NotSupportedException("Engine not cupported");
            }
        }
    }
}
