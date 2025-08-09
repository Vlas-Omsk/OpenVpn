using OpenVpn.Crypto;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace OpenVpn.TlsCrypt
{
    // Encryption: https://github.com/OpenVPN/openvpn/blob/db1fd1a80baa9e44df8ae82f0fd2b56c59195484/src/openvpn/tls_crypt.c#L136
    // Decryption: https://github.com/OpenVPN/openvpn/blob/db1fd1a80baa9e44df8ae82f0fd2b56c59195484/src/openvpn/tls_crypt.c#L209
    internal sealed class CtrCrypto
    {
        private readonly IMac _encryptMac;
        private readonly KeyParameter _encryptAuthKey;
        private readonly IBufferedCipher _encryptCipher;
        private readonly KeyParameter _encryptKey;
        private readonly int _encryptAuthTagSize;
        private readonly IMac _decryptMac;
        private readonly KeyParameter _decryptAuthKey;
        private readonly IBufferedCipher _decryptCipher;
        private readonly KeyParameter _decryptKey;
        private readonly int _decryptAuthTagSize;
        private readonly byte[] _decryptAuthTagBuffer;
        private readonly int _ivSize;

        public CtrCrypto(
            CryptoKeys keys,
            Func<IBlockCipher> cipherFactory,
            int keySize,
            int ivSize,
            Func<IMac> macFactory,
            OpenVpnMode mode,
            SecureRandom random
        )
        {
            var clientMac = macFactory.Invoke();
            var clientAuthKey = new KeyParameter(keys.Client.HmacKey.Span.Slice(0, clientMac.GetMacSize()));
            var clientCipher = new BufferedBlockCipher(
                new SicBlockCipher(cipherFactory.Invoke())
            );
            var clientKey = new KeyParameter(keys.Client.CipherKey.Span.Slice(0, keySize));

            var serverMac = macFactory.Invoke();
            var serverAuthKey = new KeyParameter(keys.Server.HmacKey.Span.Slice(0, serverMac.GetMacSize()));
            var serverCipher = new BufferedBlockCipher(
                new SicBlockCipher(cipherFactory.Invoke())
            );
            var serverKey = new KeyParameter(keys.Server.CipherKey.Span.Slice(0, keySize));

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
            _encryptAuthTagSize = _encryptMac.GetMacSize();
            _encryptCipher.Init(
                forEncryption: true,
                new ParametersWithIV(_encryptKey, new byte[ivSize])
            );

            _decryptMac.Init(_decryptAuthKey);
            _decryptAuthTagSize = _decryptMac.GetMacSize();
            _decryptAuthTagBuffer = new byte[_decryptAuthTagSize];
            _decryptCipher.Init(
                forEncryption: false,
                new ParametersWithIV(_decryptKey, _decryptAuthTagBuffer.AsSpan(0, ivSize))
            );

            _ivSize = ivSize;
        }

        public int GetEncryptedSize(int length)
        {
            return
                _encryptAuthTagSize +
                _encryptCipher.GetOutputSize(length);
        }

        public int Encrypt(ReadOnlySpan<byte> header, ReadOnlySpan<byte> input, Span<byte> output)
        {
            var offset = 0;

            var authTagSlice = output.Slice(offset, _encryptAuthTagSize);
            offset += _encryptAuthTagSize;

            _encryptMac.Init(_encryptAuthKey);
            _encryptMac.BlockUpdate(header);
            _encryptMac.BlockUpdate(input);
            _encryptMac.DoFinal(authTagSlice);

            var ciphertextSlice = output.Slice(offset);

            var ivSlice = authTagSlice.Slice(0, _ivSize);

            _encryptCipher.Init(forEncryption: true, new ParametersWithIV(_encryptKey, ivSlice));
            offset += _encryptCipher.DoFinal(input, ciphertextSlice);

            return offset;
        }

        public int GetDecryptedSize(int length)
        {
            return _decryptCipher.GetOutputSize(
                length -
                _decryptAuthTagSize
            );
        }

        public int Decrypt(ReadOnlySpan<byte> header, ReadOnlySpan<byte> input, Span<byte> output)
        {
            var offset = 0;

            var authTagSlice = input.Slice(offset, _decryptAuthTagSize);
            offset += _decryptAuthTagSize;

            var ciphertextSlice = input.Slice(offset);

            var ivSlice = authTagSlice.Slice(0, _ivSize);

            _decryptCipher.Init(forEncryption: false, new ParametersWithIV(_decryptKey, ivSlice));
            var length = _decryptCipher.DoFinal(ciphertextSlice, output);

            _decryptMac.Init(_decryptAuthKey);
            _decryptMac.BlockUpdate(header);
            _decryptMac.BlockUpdate(output.Slice(0, length));
            _decryptMac.DoFinal(_decryptAuthTagBuffer);

            if (!authTagSlice.SequenceEqual(_decryptAuthTagBuffer))
                throw new InvalidCipherTextException("mac check failed");

            return length;
        }

        public void Dispose()
        {
        }
    }
}
