using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace OpenVpn.Crypto
{
    internal sealed class CtrCrypto : ICrypto
    {
        private const int _authKeySize = 32;
        private const int _authTagSize = 32;
        private readonly int _ivSize;
        private readonly HMac _encryptMac;
        private readonly KeyParameter _encryptAuthKey;
        private readonly byte[] _encryptAuthTagBuffer = new byte[_authTagSize];
        private readonly BufferedBlockCipher _encryptCipher;
        private readonly KeyParameter _encryptKey;
        private readonly HMac _decryptMac;
        private readonly KeyParameter _decryptAuthKey;
        private readonly byte[] _decryptAuthTagBuffer = new byte[_authTagSize];
        private readonly byte[] _decryptExpectedAuthTagBuffer = new byte[_authTagSize];
        private readonly BufferedBlockCipher _decryptCipher;
        private readonly KeyParameter _decryptKey;

        public CtrCrypto(
            CryptoKeys keys,
            Func<IBlockCipher> cipherFactory,
            int keySize,
            int ivSize,
            OpenVpnMode mode
        )
        {
            _ivSize = ivSize;

            var clientMac = new HMac(new Sha256Digest());
            var clientAuthKey = new KeyParameter(keys.Client.HmacKey.Slice(0, _authKeySize).Span);
            var clientCipher = new BufferedBlockCipher(
                new SicBlockCipher(cipherFactory.Invoke())
            );
            var clientKey = new KeyParameter(keys.Client.CipherKey.Slice(0, keySize).Span);

            var serverMac = new HMac(new Sha256Digest());
            var serverAuthKey = new KeyParameter(keys.Server.HmacKey.Slice(0, _authKeySize).Span);
            var serverCipher = new BufferedBlockCipher(
                new SicBlockCipher(cipherFactory.Invoke())
            );
            var serverKey = new KeyParameter(keys.Server.CipherKey.Slice(0, keySize).Span);

            if (mode == OpenVpnMode.Server)
            {
                _encryptMac = clientMac;
                _encryptAuthKey = clientAuthKey;
                _encryptCipher = clientCipher;
                _encryptKey = clientKey;
                _decryptMac = serverMac;
                _decryptAuthKey = serverAuthKey;
                _decryptCipher = serverCipher;
                _decryptKey = serverKey;
            }
            else if (mode == OpenVpnMode.Client)
            {
                _encryptMac = serverMac;
                _encryptAuthKey = serverAuthKey;
                _encryptCipher = serverCipher;
                _encryptKey = serverKey;
                _decryptMac = clientMac;
                _decryptAuthKey = clientAuthKey;
                _decryptCipher = clientCipher;
                _decryptKey = clientKey;
            }
            else
            {
                throw new NotSupportedException("Mode not supported");
            }

            _encryptMac.Init(_encryptAuthKey);
            _encryptCipher.Init(forEncryption: true, new ParametersWithIV(_encryptKey, _encryptAuthTagBuffer.AsSpan(0, ivSize)));

            _decryptMac.Init(_decryptAuthKey);
            _decryptCipher.Init(forEncryption: false, new ParametersWithIV(_decryptKey, _decryptAuthTagBuffer.AsSpan(0, ivSize)));
        }

        public int GetEncryptedSize(int length)
        {
            return _authTagSize + _encryptCipher.GetOutputSize(length);
        }

        public int Encrypt(ReadOnlySpan<byte> additionalData, ReadOnlySpan<byte> input, Span<byte> output)
        {
            _encryptMac.Init(_encryptAuthKey);
            _encryptMac.BlockUpdate(additionalData);
            _encryptMac.BlockUpdate(input);
            _encryptMac.DoFinal(_encryptAuthTagBuffer);

            _encryptAuthTagBuffer.CopyTo(output);
            output = output.Slice(_authTagSize);

            var iv = _encryptAuthTagBuffer.AsSpan(0, _ivSize);

            _encryptCipher.Init(forEncryption: true, new ParametersWithIV(_encryptKey, iv));
            var length = _encryptCipher.DoFinal(input, output);

            return _authTagSize + length;
        }

        public int GetDecryptedSize(int length)
        {
            return _decryptCipher.GetOutputSize(length - _authTagSize);
        }

        public int Decrypt(ReadOnlySpan<byte> additionalData, ReadOnlySpan<byte> input, Span<byte> output)
        {
            input.Slice(0, _authTagSize).CopyTo(_decryptAuthTagBuffer);
            input = input.Slice(_authTagSize);

            var iv = _decryptAuthTagBuffer.AsSpan(0, _ivSize);

            _decryptCipher.Init(forEncryption: false, new ParametersWithIV(_decryptKey, iv));
            var length = _decryptCipher.DoFinal(input, output);

            _decryptMac.Init(_decryptAuthKey);
            _decryptMac.BlockUpdate(additionalData);
            _decryptMac.BlockUpdate(output.Slice(0, length));
            _decryptMac.DoFinal(_decryptExpectedAuthTagBuffer);

            if (!_decryptAuthTagBuffer.SequenceEqual(_decryptExpectedAuthTagBuffer))
                throw new InvalidCipherTextException("mac check in CTR failed");

            return length;
        }

        public void Dispose()
        {
        }
    }
}
