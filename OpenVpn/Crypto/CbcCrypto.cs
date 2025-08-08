using System.Buffers;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace OpenVpn.Crypto
{
    internal sealed class CbcCrypto : ICrypto
    {
        private readonly PaddedBufferedBlockCipher _encryptCipher;
        private readonly KeyParameter _encryptKey;
        private readonly byte[] _encryptIvBuffer;
        private readonly PaddedBufferedBlockCipher _decryptCipher;
        private readonly KeyParameter _decryptKey;
        private readonly byte[] _decryptIvBuffer;
        private readonly int _ivSize;
        private readonly SecureRandom _random;

        public CbcCrypto(
            CryptoKeys keys,
            Func<IBlockCipher> cipherFactory,
            int keySize,
            int ivSize,
            OpenVpnMode mode,
            SecureRandom random
        )
        {
            var clientCipher = new PaddedBufferedBlockCipher(
                new CbcBlockCipher(cipherFactory.Invoke()),
                new Pkcs7Padding()
            );
            var clientKey = new KeyParameter(keys.Client.CipherKey.Span.Slice(0, keySize));

            var serverCipher = new PaddedBufferedBlockCipher(
                new CbcBlockCipher(cipherFactory.Invoke()),
                new Pkcs7Padding()
            );
            var serverKey = new KeyParameter(keys.Server.CipherKey.Span.Slice(0, keySize));

            if (mode == OpenVpnMode.Server)
            {
                _encryptCipher = serverCipher;
                _encryptKey = serverKey;
                _decryptCipher = clientCipher;
                _decryptKey = clientKey;
            }
            else if (mode == OpenVpnMode.Client)
            {
                _encryptCipher = clientCipher;
                _encryptKey = clientKey;
                _decryptCipher = serverCipher;
                _decryptKey = serverKey;
            }
            else
            {
                throw new NotSupportedException("Mode not supported");
            }

            _encryptIvBuffer = new byte[ivSize];

            var encryptKeyWithIv = new ParametersWithIV(_encryptKey, _encryptIvBuffer);

            _encryptCipher.Init(forEncryption: true, encryptKeyWithIv);

            _decryptIvBuffer = new byte[ivSize];

            var keyWithIv = new ParametersWithIV(_decryptKey, _decryptIvBuffer);

            _decryptCipher.Init(forEncryption: false, keyWithIv);

            _ivSize = ivSize;
            _random = random;
        }

        public int GetEncryptedSize(int length)
        {
            return
                _ivSize +
                _encryptCipher.GetOutputSize(length);
        }

        public int Encrypt(ReadOnlySpan<byte> additionalData, ReadOnlySpan<byte> input, Span<byte> output)
        {
            var ivSlice = output.Slice(0, _ivSize);
            var payloadSlice = output.Slice(_ivSize);

            _random.NextBytes(_encryptIvBuffer);

            _encryptIvBuffer.CopyTo(ivSlice);

            var keyWithIv = new ParametersWithIV(_encryptKey, _encryptIvBuffer);

            _encryptCipher.Init(forEncryption: true, keyWithIv);

            var length = _encryptCipher.DoFinal(input, payloadSlice);

            return _ivSize + length;
        }

        public int GetDecryptedSize(int length)
        {
            return _decryptCipher.GetOutputSize(length);
        }

        public int Decrypt(ReadOnlySpan<byte> additionalData, ReadOnlySpan<byte> input, Span<byte> output)
        {
            var ivSlice = input.Slice(0, _ivSize);
            var payloadSlice = input.Slice(_ivSize);

            ivSlice.CopyTo(_decryptIvBuffer);

            var keyWithIv = new ParametersWithIV(_decryptKey, _decryptIvBuffer);

            _decryptCipher.Init(forEncryption: false, keyWithIv);

            var length = _decryptCipher.DoFinal(payloadSlice, output);

            return length;
        }
    }
}
