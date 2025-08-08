using OpenVpn.Crypto;
using Org.BouncyCastle.Security;

namespace OpenVpn.Tests
{
    public class CryptoTests
    {
        [Theory]
        [InlineData("AES-128-GCM", false)]
        [InlineData("AES-128-CBC", false)]
        [InlineData("AES-128-CTR", false)]
        [InlineData("AES-192-GCM", false)]
        [InlineData("AES-192-CBC", false)]
        [InlineData("AES-192-CTR", false)]
        [InlineData("AES-256-GCM", false)]
        [InlineData("AES-256-CBC", false)]
        [InlineData("AES-256-CTR", false)]
        [InlineData("BF-CBC", false)]
        [InlineData("PLAIN", false)]
        [InlineData("NONE", false)]
        public void RoundTrip_EncryptThenDecrypt_PreservesData(string cipherName, bool epochFormat)
        {
            var random = new SecureRandom();
            var clientKeySource = CryptoKeySource.Generate(random);
            var clientSessionId = (ulong)random.NextInt64();
            var serverKeySource = CryptoKeySource.Generate(random);
            var serverSessionId = (ulong)random.NextInt64();
            var keys = CryptoKeys.DeriveFromKeySources(
                clientKeySource,
                clientSessionId,
                serverKeySource,
                serverSessionId
            );

            var clientCrypto = Crypto.Crypto.Create(
                cipherName,
                keys,
                OpenVpnMode.Client,
                epochFormat,
                random
            );
            var serverCrypto = Crypto.Crypto.Create(
                cipherName,
                keys,
                OpenVpnMode.Server,
                epochFormat,
                random
            );

            // Client -> Server
            for (var i = 1; i <= 32768; i *= 8)
            {
                var additionalData = new byte[i];
                var data = new byte[i];

                random.NextBytes(additionalData);
                random.NextBytes(data);

                var expectedEncryptedLength = clientCrypto.GetEncryptedSize(i);
                var encryptedOutput = new byte[expectedEncryptedLength];
                var encryptedLength = clientCrypto.Encrypt(additionalData, data, encryptedOutput);

                var expectedDecryptedLength = serverCrypto.GetDecryptedSize(encryptedLength);
                var decryptedOutput = new byte[expectedDecryptedLength];
                var decryptedLength = serverCrypto.Decrypt(additionalData, encryptedOutput.AsSpan(0, encryptedLength), decryptedOutput);

                Assert.Equal(
                    data,
                    decryptedOutput.Take(decryptedLength)
                );
            }

            // Server -> Client
            for (var i = 1; i <= 32768; i *= 8)
            {
                var additionalData = new byte[i];
                var data = new byte[i];

                random.NextBytes(additionalData);
                random.NextBytes(data);

                var expectedEncryptedLength = serverCrypto.GetEncryptedSize(i);
                var encryptedOutput = new byte[expectedEncryptedLength];
                var encryptedLength = serverCrypto.Encrypt(additionalData, data, encryptedOutput);

                var expectedDecryptedLength = clientCrypto.GetDecryptedSize(encryptedLength);
                var decryptedOutput = new byte[expectedDecryptedLength];
                var decryptedLength = clientCrypto.Decrypt(additionalData, encryptedOutput.AsSpan(0, encryptedLength), decryptedOutput);

                Assert.Equal(
                    data,
                    decryptedOutput.Take(decryptedLength)
                );
            }
        }
    }
}