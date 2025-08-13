using System.Buffers.Binary;
using OpenVpn.Crypto;
using OpenVpn.Data.Crypto;
using Org.BouncyCastle.Security;

namespace OpenVpn.Tests
{
    /// <summary>
    /// Comprehensive data structure tests for IDataCrypto interface following Write -> Send -> Check output structure pattern
    /// and Receive -> Read -> Check input structure validation pattern.
    /// Tests actual implementations instead of mocks.
    /// </summary>
    public class IDataCryptoStructureTests
    {
        private readonly SecureRandom _random = new();

        [Theory]
        [InlineData("AES-128-GCM", null, false)]
        [InlineData("AES-256-GCM", null, false)]
        [InlineData("AES-128-CBC", "SHA256", false)]
        [InlineData("AES-256-CBC", "SHA256", false)]
        [InlineData("AES-128-CTR", "SHA256", false)]
        [InlineData("PLAIN", null, false)]
        [InlineData("NONE", null, false)]
        public void Write_Encrypt_CheckOutputStructure_VerifiesPacketStructure(string cipherName, string? macName, bool epochFormat)
        {
            // Arrange
            var (clientCrypto, serverCrypto) = CreateCryptoPair(cipherName, macName, epochFormat);
            var header = GenerateRandomBytes(16);
            var data = GenerateRandomBytes(64);
            var packetId = 0x12345678u;

            // Act - Write (Encrypt)
            var encryptedSize = clientCrypto.GetEncryptedSize(data.Length);
            var encryptedOutput = new byte[encryptedSize];
            var actualEncryptedLength = clientCrypto.Encrypt(header, data, encryptedOutput, packetId);

            // Check Output Structure
            Assert.True(actualEncryptedLength <= encryptedSize, "Encrypted length should not exceed predicted size");
            Assert.True(actualEncryptedLength > 0, "Encrypted output should not be empty");

            if (cipherName != "NONE" && cipherName != "PLAIN")
            {
                // For AEAD ciphers (GCM), packet ID is embedded in IV
                if (cipherName.Contains("GCM"))
                {
                    // GCM mode embeds packet ID in first 4 bytes of the encrypted data as part of IV
                    Assert.True(actualEncryptedLength >= 16, "GCM encrypted data should include authentication tag");
                }
                else if (macName != null)
                {
                    // CBC/CTR with MAC should have MAC size added
                    var macSize = GetExpectedMacSize(macName);
                    Assert.True(actualEncryptedLength >= data.Length + macSize, 
                        $"Encrypted data with MAC should include {macSize} byte MAC");
                }
                
                // Verify encrypted data is different from original
                Assert.NotEqual(data, encryptedOutput.AsSpan(0, Math.Min(data.Length, actualEncryptedLength)).ToArray());
            }
            else
            {
                // NONE/PLAIN modes should preserve data
                Assert.Equal(data.Length, actualEncryptedLength);
                if (cipherName == "NONE")
                {
                    Assert.Equal(data, encryptedOutput.AsSpan(0, actualEncryptedLength).ToArray());
                }
            }
        }

        [Theory]
        [InlineData("AES-128-GCM", null, false)]
        [InlineData("AES-256-GCM", null, false)]
        [InlineData("AES-128-CBC", "SHA256", false)]
        [InlineData("AES-256-CBC", "SHA256", false)]
        public void Receive_Decrypt_CheckInputStructure_ValidatesPacketFormat(string cipherName, string? macName, bool epochFormat)
        {
            // Arrange
            var (clientCrypto, serverCrypto) = CreateCryptoPair(cipherName, macName, epochFormat);
            var header = GenerateRandomBytes(16);
            var originalData = GenerateRandomBytes(64);
            var packetId = 0x87654321u;

            // First encrypt to get valid encrypted data
            var encryptedSize = clientCrypto.GetEncryptedSize(originalData.Length);
            var encryptedOutput = new byte[encryptedSize];
            var encryptedLength = clientCrypto.Encrypt(header, originalData, encryptedOutput, packetId);

            // Act - Receive (Decrypt)
            var decryptedSize = serverCrypto.GetDecryptedSize(encryptedLength);
            var decryptedOutput = new byte[decryptedSize];
            var actualDecryptedLength = serverCrypto.Decrypt(header, encryptedOutput.AsSpan(0, encryptedLength), decryptedOutput, out var decryptedPacketId);

            // Check Input Structure Validation
            Assert.Equal(packetId, decryptedPacketId);
            Assert.Equal(originalData.Length, actualDecryptedLength);
            Assert.Equal(originalData, decryptedOutput.AsSpan(0, actualDecryptedLength).ToArray());
        }

        [Theory]
        [InlineData("AES-128-GCM", null)]
        [InlineData("AES-256-GCM", null)]
        [InlineData("AES-128-CBC", "SHA256")]
        public void Write_Send_CheckPacketIdEmbedding_VerifiesPacketIdStructure(string cipherName, string? macName)
        {
            // Arrange
            var (clientCrypto, serverCrypto) = CreateCryptoPair(cipherName, macName, false);
            var header = GenerateRandomBytes(16);
            var data = GenerateRandomBytes(32);
            
            // Test multiple packet IDs to verify structure
            var packetIds = new uint[] { 0x00000001, 0x12345678, 0xFFFFFFFF, 0x80000000 };

            foreach (var packetId in packetIds)
            {
                // Act - Write with specific packet ID
                var encryptedSize = clientCrypto.GetEncryptedSize(data.Length);
                var encryptedOutput = new byte[encryptedSize];
                var encryptedLength = clientCrypto.Encrypt(header, data, encryptedOutput, packetId);

                // Send to other side and check packet ID extraction
                var decryptedSize = serverCrypto.GetDecryptedSize(encryptedLength);
                var decryptedOutput = new byte[decryptedSize];
                var decryptedLength = serverCrypto.Decrypt(header, encryptedOutput.AsSpan(0, encryptedLength), decryptedOutput, out var extractedPacketId);

                // Check packet ID structure preservation
                Assert.Equal(packetId, extractedPacketId);
                Assert.Equal(data, decryptedOutput.AsSpan(0, decryptedLength).ToArray());
            }
        }

        [Theory]
        [InlineData("AES-128-CBC", "SHA256")]
        [InlineData("AES-256-CBC", "SHA512")]
        [InlineData("AES-128-CTR", "SHA1")]
        public void Write_Send_CheckIVGeneration_VerifiesIVStructure(string cipherName, string macName)
        {
            // Arrange
            var (clientCrypto, _) = CreateCryptoPair(cipherName, macName, false);
            var header = GenerateRandomBytes(16);
            var data = GenerateRandomBytes(64);
            var packetId = 0x12345678u;

            // Act - Generate multiple encryptions to check IV randomness
            var encryptedOutputs = new List<byte[]>();
            for (int i = 0; i < 5; i++)
            {
                var encryptedSize = clientCrypto.GetEncryptedSize(data.Length);
                var encryptedOutput = new byte[encryptedSize];
                var encryptedLength = clientCrypto.Encrypt(header, data, encryptedOutput, packetId);
                encryptedOutputs.Add(encryptedOutput.AsSpan(0, encryptedLength).ToArray());
            }

            // Check IV randomness - encrypted outputs should be different due to random IVs
            for (int i = 0; i < encryptedOutputs.Count - 1; i++)
            {
                for (int j = i + 1; j < encryptedOutputs.Count; j++)
                {
                    Assert.NotEqual(encryptedOutputs[i], encryptedOutputs[j]);
                }
            }
        }

        [Fact]
        public void Write_Send_CheckHeaderIntegrity_VerifiesHeaderIncluded()
        {
            // Arrange
            var (clientCrypto, serverCrypto) = CreateCryptoPair("AES-256-CBC", "SHA256", false);
            var header1 = GenerateRandomBytes(16);
            var header2 = GenerateRandomBytes(16);
            header2[0] = (byte)(header1[0] ^ 0xFF); // Ensure headers are different
            var data = GenerateRandomBytes(32);
            var packetId = 0x12345678u;

            // Act - Encrypt with first header
            var encryptedSize = clientCrypto.GetEncryptedSize(data.Length);
            var encryptedOutput = new byte[encryptedSize];
            var encryptedLength = clientCrypto.Encrypt(header1, data, encryptedOutput, packetId);

            // Try to decrypt with wrong header - should fail or give wrong result
            var decryptedSize = serverCrypto.GetDecryptedSize(encryptedLength);
            var decryptedOutput = new byte[decryptedSize];
            
            // This should either throw or produce incorrect results due to header mismatch
            var exception = Record.Exception(() => 
            {
                serverCrypto.Decrypt(header2, encryptedOutput.AsSpan(0, encryptedLength), decryptedOutput, out var packetId);
            });

            // Either an exception is thrown or the decryption produces different data
            Assert.True(exception != null, "Decryption with wrong header should fail");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(63)]
        [InlineData(64)]
        [InlineData(65)]
        [InlineData(1024)]
        [InlineData(8192)]
        public void Write_Send_CheckBoundaryValues_HandlesVariousDataSizes(int dataSize)
        {
            // Arrange
            var (clientCrypto, serverCrypto) = CreateCryptoPair("AES-256-CBC", "SHA256", false);
            var header = GenerateRandomBytes(16);
            var data = GenerateRandomBytes(dataSize);
            var packetId = 0x12345678u;

            // Act
            var encryptedSize = clientCrypto.GetEncryptedSize(dataSize);
            var encryptedOutput = new byte[encryptedSize];
            var encryptedLength = clientCrypto.Encrypt(header, data, encryptedOutput, packetId);

            var decryptedSize = serverCrypto.GetDecryptedSize(encryptedLength);
            var decryptedOutput = new byte[decryptedSize];
            var decryptedLength = serverCrypto.Decrypt(header, encryptedOutput.AsSpan(0, encryptedLength), decryptedOutput, out var decryptedPacketId);

            // Check boundary value handling
            Assert.Equal(packetId, decryptedPacketId);
            Assert.Equal(dataSize, decryptedLength);
            if (dataSize > 0)
            {
                Assert.Equal(data, decryptedOutput.AsSpan(0, decryptedLength).ToArray());
            }
        }

        [Fact]
        public void Receive_Read_CheckErrorConditions_HandlesCorruptedData()
        {
            // Arrange
            var (clientCrypto, serverCrypto) = CreateCryptoPair("AES-256-GCM", null, false);
            var header = GenerateRandomBytes(16);
            var data = GenerateRandomBytes(64);
            var packetId = 0x12345678u;

            // Create valid encrypted data
            var encryptedSize = clientCrypto.GetEncryptedSize(data.Length);
            var encryptedOutput = new byte[encryptedSize];
            var encryptedLength = clientCrypto.Encrypt(header, data, encryptedOutput, packetId);

            // Corrupt the encrypted data
            var corruptedData = encryptedOutput.AsSpan(0, encryptedLength).ToArray();
            if (corruptedData.Length > 0)
            {
                corruptedData[corruptedData.Length / 2] ^= 0xFF; // Flip bits in the middle
            }

            // Act & Assert - Should handle corrupted data gracefully
            var decryptedSize = serverCrypto.GetDecryptedSize(encryptedLength);
            var decryptedOutput = new byte[decryptedSize];
            
            var exception = Record.Exception(() =>
            {
                serverCrypto.Decrypt(header, corruptedData, decryptedOutput, out var _);
            });

            // Should either throw an exception or fail authentication
            Assert.True(exception != null, "Corrupted data should cause decryption to fail");
        }

        [Theory]
        [InlineData("AES-128-GCM", null)]
        [InlineData("AES-256-GCM", null)]
        public void Write_Send_CheckAuthenticationTag_VerifiesGCMTagStructure(string cipherName, string? macName)
        {
            // Arrange
            var (clientCrypto, serverCrypto) = CreateCryptoPair(cipherName, macName, false);
            var header = GenerateRandomBytes(16);
            var data = GenerateRandomBytes(64);
            var packetId = 0x12345678u;

            // Act
            var encryptedSize = clientCrypto.GetEncryptedSize(data.Length);
            var encryptedOutput = new byte[encryptedSize];
            var encryptedLength = clientCrypto.Encrypt(header, data, encryptedOutput, packetId);

            // Check GCM authentication tag is present (16 bytes for GCM)
            Assert.True(encryptedLength >= data.Length + 16, "GCM encrypted data should include 16-byte authentication tag");

            // Verify authentication by successful decryption
            var decryptedSize = serverCrypto.GetDecryptedSize(encryptedLength);
            var decryptedOutput = new byte[decryptedSize];
            var decryptedLength = serverCrypto.Decrypt(header, encryptedOutput.AsSpan(0, encryptedLength), decryptedOutput, out var decryptedPacketId);

            Assert.Equal(packetId, decryptedPacketId);
            Assert.Equal(data, decryptedOutput.AsSpan(0, decryptedLength).ToArray());
        }

        private (IDataCrypto client, IDataCrypto server) CreateCryptoPair(string cipherName, string? macName, bool epochFormat)
        {
            var clientKeySource = CryptoKeySource.Generate(_random);
            var clientSessionId = (ulong)_random.NextInt64();
            var serverKeySource = CryptoKeySource.Generate(_random);
            var serverSessionId = (ulong)_random.NextInt64();
            var keys = CryptoKeys.DeriveFromKeySources(
                clientKeySource,
                clientSessionId,
                serverKeySource,
                serverSessionId
            );

            var clientCrypto = DataCrypto.Create(cipherName, macName, keys, OpenVpnMode.Client, epochFormat, _random);
            var serverCrypto = DataCrypto.Create(cipherName, macName, keys, OpenVpnMode.Server, epochFormat, _random);

            return (clientCrypto, serverCrypto);
        }

        private byte[] GenerateRandomBytes(int length)
        {
            var bytes = new byte[length];
            _random.NextBytes(bytes);
            return bytes;
        }

        private static int GetExpectedMacSize(string macName) => macName.ToUpper() switch
        {
            "SHA1" => 20,
            "SHA256" => 32,
            "SHA384" => 48,
            "SHA512" => 64,
            _ => throw new ArgumentException($"Unknown MAC: {macName}")
        };
    }
}