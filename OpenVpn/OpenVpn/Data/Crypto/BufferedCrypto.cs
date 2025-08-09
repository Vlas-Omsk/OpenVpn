using System.Buffers.Binary;
using OpenVpn.Crypto;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace OpenVpn.Data.Crypto
{
    internal sealed class BufferedCrypto : IDataCrypto
    {
        private const int _packetIdSize = 4;
        private readonly IMac? _encryptMac;
        private readonly KeyParameter? _encryptAuthKey;
        private readonly IBufferedCipher _encryptCipher;
        private readonly KeyParameter _encryptKey;
        private readonly int _encryptAuthTagSize;
        private readonly byte[] _encryptPacketIdBuffer = new byte[_packetIdSize];
        private readonly IMac? _decryptMac;
        private readonly KeyParameter? _decryptAuthKey;
        private readonly IBufferedCipher _decryptCipher;
        private readonly KeyParameter _decryptKey;
        private readonly int _decryptAuthTagSize;
        private readonly byte[] _decryptAuthTagBuffer;
        private readonly int _ivSize;
        private readonly SecureRandom _random;

        public BufferedCrypto(
            CryptoKeys keys,
            Func<IBufferedCipher> cipherFactory,
            int keySize,
            int ivSize,
            Func<IMac>? macFactory,
            OpenVpnMode mode,
            SecureRandom random
        )
        {
            IMac? clientMac = null;
            KeyParameter? clientAuthKey = null;

            if (macFactory != null)
            {
                clientMac = macFactory.Invoke();
                clientAuthKey = new KeyParameter(keys.Client.HmacKey.Span.Slice(0, clientMac.GetMacSize()));
            }

            var clientCipher = cipherFactory.Invoke();
            var clientKey = new KeyParameter(keys.Client.CipherKey.Span.Slice(0, keySize));

            IMac? serverMac = null;
            KeyParameter? serverAuthKey = null;

            if (macFactory != null)
            {
                serverMac = macFactory.Invoke();
                serverAuthKey = new KeyParameter(keys.Server.HmacKey.Span.Slice(0, serverMac.GetMacSize()));
            }

            var serverCipher = cipherFactory.Invoke();
            var serverKey = new KeyParameter(keys.Server.CipherKey.Span.Slice(0, keySize));

            if (mode == OpenVpnMode.Server)
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
            else if (mode == OpenVpnMode.Client)
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
            else
            {
                throw new NotSupportedException("Mode not supported");
            }

            _encryptMac?.Init(_encryptAuthKey);
            _encryptAuthTagSize = _encryptMac?.GetMacSize() ?? 0;
            _encryptCipher.Init(
                forEncryption: true,
                new ParametersWithIV(_encryptKey, new byte[ivSize])
            );

            _decryptMac?.Init(_decryptAuthKey);
            _decryptAuthTagSize = _decryptMac?.GetMacSize() ?? 0;
            _decryptAuthTagBuffer = new byte[_decryptAuthTagSize];
            _decryptCipher.Init(
                forEncryption: false,
                new ParametersWithIV(_decryptKey, new byte[ivSize])
            );

            _ivSize = ivSize;
            _random = random;
        }

        public int GetEncryptedSize(int length)
        {
            return
                4 +
                _encryptAuthTagSize +
                _ivSize +
                _encryptCipher.GetOutputSize(length);
        }

        public int Encrypt(ReadOnlySpan<byte> header, ReadOnlySpan<byte> input, Span<byte> output, uint packetId)
        {
            var outputOffset = 0;

            var authTagSlice = output.Slice(outputOffset, _encryptAuthTagSize);
            outputOffset += _encryptAuthTagSize;

            var ivSlice = output.Slice(outputOffset, _ivSize);
            outputOffset += _ivSize;

            _random.NextBytes(ivSlice);

            var ciphertextOffset = 0;
            var ciphertextSlice = output.Slice(outputOffset);

            _encryptCipher.Init(forEncryption: true, new ParametersWithIV(_encryptKey, ivSlice));

            BinaryPrimitives.WriteUInt32BigEndian(_encryptPacketIdBuffer, packetId);
            ciphertextOffset += _encryptCipher.ProcessBytes(_encryptPacketIdBuffer, ciphertextSlice.Slice(ciphertextOffset));
            ciphertextOffset += _encryptCipher.DoFinal(input, ciphertextSlice.Slice(ciphertextOffset));

            ciphertextSlice = ciphertextSlice.Slice(0, ciphertextOffset);
            outputOffset += ciphertextOffset;

            if (_encryptMac != null)
            {
                _encryptMac.Init(_encryptAuthKey);
                _encryptMac.BlockUpdate(ivSlice);
                _encryptMac.BlockUpdate(ciphertextSlice);
                _encryptMac.DoFinal(authTagSlice);
            }

            return outputOffset;
        }

        public int GetDecryptedSize(int length)
        {
            return _decryptCipher.GetOutputSize(
                length -
                _decryptAuthTagSize -
                _ivSize
            );
        }

        public int Decrypt(ReadOnlySpan<byte> header, ReadOnlySpan<byte> input, Span<byte> output, out uint packetId)
        {
            var inputOffset = 0;

            var authTagSlice = input.Slice(inputOffset, _decryptAuthTagSize);
            inputOffset += _decryptAuthTagSize;

            var ivSlice = input.Slice(inputOffset, _ivSize);
            inputOffset += _ivSize;

            var ciphertextSlice = input.Slice(inputOffset);

            if (_decryptMac != null)
            {
                _decryptMac.Init(_decryptAuthKey);
                _decryptMac.BlockUpdate(ivSlice);
                _decryptMac.BlockUpdate(ciphertextSlice);
                _decryptMac.DoFinal(_decryptAuthTagBuffer);

                if (!authTagSlice.SequenceEqual(_decryptAuthTagBuffer))
                    throw new InvalidCipherTextException("mac check failed");
            }

            _decryptCipher.Init(forEncryption: false, new ParametersWithIV(_decryptKey, ivSlice));

            var outputOffset = _decryptCipher.DoFinal(ciphertextSlice, output);

            packetId = BinaryPrimitives.ReadUInt32BigEndian(output);
            output.MoveLeft(_packetIdSize);
            outputOffset -= _packetIdSize;

            return outputOffset;
        }

        public void Dispose()
        {
        }

        public static BufferedCrypto CreateCtr(
            CryptoKeys keys,
            Func<IBlockCipher> cipherFactory,
            int keySize,
            int ivSize,
            Func<IMac>? macFactory,
            OpenVpnMode mode,
            SecureRandom random
        )
        {
            return new BufferedCrypto(
                keys,
                () => new BufferedBlockCipher(
                    new SicBlockCipher(cipherFactory.Invoke())
                ),
                keySize,
                ivSize,
                macFactory,
                mode,
                random
            );
        }

        public static BufferedCrypto CreateCbc(
            CryptoKeys keys,
            Func<IBlockCipher> cipherFactory,
            int keySize,
            int ivSize,
            Func<IMac>? macFactory,
            OpenVpnMode mode,
            SecureRandom random
        )
        {
            return new BufferedCrypto(
                keys,
                () => new PaddedBufferedBlockCipher(
                    new CbcBlockCipher(cipherFactory.Invoke()),
                    new Pkcs7Padding()
                ),
                keySize,
                ivSize,
                macFactory,
                mode,
                random
            );
        }
    }
}
