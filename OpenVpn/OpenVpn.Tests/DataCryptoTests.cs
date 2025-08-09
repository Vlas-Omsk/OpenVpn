using OpenVpn.Crypto;
using OpenVpn.Data.Crypto;
using Org.BouncyCastle.Security;

namespace OpenVpn.Tests
{
    public class DataCryptoTests
    {
        [Theory]
        [InlineData("AES-128-GCM", null, false)]
        [InlineData("AES-128-CBC", null, false)]
        [InlineData("AES-128-CBC", "SHA1", false)]
        [InlineData("AES-128-CBC", "SHA256", false)]
        [InlineData("AES-128-CBC", "SHA384", false)]
        [InlineData("AES-128-CBC", "SHA512", false)]
        [InlineData("AES-128-CTR", null, false)]
        [InlineData("AES-128-CTR", "SHA1", false)]
        [InlineData("AES-128-CTR", "SHA256", false)]
        [InlineData("AES-128-CTR", "SHA384", false)]
        [InlineData("AES-128-CTR", "SHA512", false)]
        [InlineData("AES-192-GCM", null, false)]
        [InlineData("AES-192-CBC", null, false)]
        [InlineData("AES-192-CBC", "SHA1", false)]
        [InlineData("AES-192-CBC", "SHA256", false)]
        [InlineData("AES-192-CBC", "SHA384", false)]
        [InlineData("AES-192-CBC", "SHA512", false)]
        [InlineData("AES-192-CTR", null, false)]
        [InlineData("AES-192-CTR", "SHA1", false)]
        [InlineData("AES-192-CTR", "SHA256", false)]
        [InlineData("AES-192-CTR", "SHA384", false)]
        [InlineData("AES-192-CTR", "SHA512", false)]
        [InlineData("AES-256-GCM", null, false)]
        [InlineData("AES-256-CBC", null, false)]
        [InlineData("AES-256-CBC", "SHA1", false)]
        [InlineData("AES-256-CBC", "SHA256", false)]
        [InlineData("AES-256-CBC", "SHA384", false)]
        [InlineData("AES-256-CBC", "SHA512", false)]
        [InlineData("AES-256-CTR", null, false)]
        [InlineData("AES-256-CTR", "SHA1", false)]
        [InlineData("AES-256-CTR", "SHA256", false)]
        [InlineData("AES-256-CTR", "SHA384", false)]
        [InlineData("AES-256-CTR", "SHA512", false)]
        [InlineData("BF-CBC", null, false)]
        [InlineData("PLAIN", null, false)]
        [InlineData("NONE", null, false)]
        public void RoundTrip_EncryptThenDecrypt_PreservesData(string cipherName, string? macName, bool epochFormat)
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

            var clientCrypto = DataCrypto.Create(
                cipherName,
                macName,
                keys,
                OpenVpnMode.Client,
                epochFormat,
                random
            );
            var serverCrypto = DataCrypto.Create(
                cipherName,
                macName,
                keys,
                OpenVpnMode.Server,
                epochFormat,
                random
            );

            // Client -> Server
            for (var i = 1; i <= 32768; i *= 8)
            {
                var header = new byte[i];
                var data = new byte[i];
                var packetId = (uint)i;

                random.NextBytes(header);
                random.NextBytes(data);

                var expectedEncryptedLength = clientCrypto.GetEncryptedSize(i);
                var encryptedOutput = new byte[expectedEncryptedLength];
                var encryptedLength = clientCrypto.Encrypt(header, data, encryptedOutput, packetId);

                var expectedDecryptedLength = serverCrypto.GetDecryptedSize(encryptedLength);
                var decryptedOutput = new byte[expectedDecryptedLength];
                var decryptedLength = serverCrypto.Decrypt(header, encryptedOutput.AsSpan(0, encryptedLength), decryptedOutput, out var decryptedPacketId);

                Assert.Equal(packetId, decryptedPacketId);
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
                var packetId = (uint)i;

                random.NextBytes(additionalData);
                random.NextBytes(data);

                var expectedEncryptedLength = serverCrypto.GetEncryptedSize(i);
                var encryptedOutput = new byte[expectedEncryptedLength];
                var encryptedLength = serverCrypto.Encrypt(additionalData, data, encryptedOutput, packetId);

                var expectedDecryptedLength = clientCrypto.GetDecryptedSize(encryptedLength);
                var decryptedOutput = new byte[expectedDecryptedLength];
                var decryptedLength = clientCrypto.Decrypt(additionalData, encryptedOutput.AsSpan(0, encryptedLength), decryptedOutput, out var decryptedPacketId);

                Assert.Equal(packetId, decryptedPacketId);
                Assert.Equal(
                    data,
                    decryptedOutput.Take(decryptedLength)
                );
            }
        }
    }
}