using System.Buffers;
using System.Buffers.Binary;
using CommunityToolkit.HighPerformance;
using OpenVpn.Buffers;
using OpenVpn.Crypto;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace OpenVpn.Data.Crypto
{
    // Encryption: https://github.com/OpenVPN/openvpn/blob/db1fd1a80baa9e44df8ae82f0fd2b56c59195484/src/openvpn/crypto.c#L66
    // Decryption: https://github.com/OpenVPN/openvpn/blob/db1fd1a80baa9e44df8ae82f0fd2b56c59195484/src/openvpn/crypto.c#L428
    internal sealed class AeadCrypto : IDataCrypto
    {
        private const int _macSize = 16;
        private const int _packetIdSize = 4;
        private readonly IAeadBlockCipher _encryptCipher;
        private readonly KeyParameter _encryptKey;
        private readonly byte[] _encryptIv;
        private readonly byte[] _encryptIvBuffer;
        private readonly byte[] _encryptMacBuffer = new byte[_macSize];
        private readonly IAeadBlockCipher _decryptCipher;
        private readonly KeyParameter _decryptKey;
        private readonly byte[] _decryptIv;
        private readonly byte[] _decryptIvBuffer;
        private readonly byte[] _decryptMacBuffer = new byte[_macSize];
        private readonly bool _epochFormat;

        public AeadCrypto(
            CryptoKeys keys,
            Func<IAeadBlockCipher> cipherFactory,
            int keySize,
            int ivSize,
            OpenVpnMode mode,
            bool epochFormat
        )
        {
            var clientCipher = cipherFactory.Invoke();
            var clientKey = new KeyParameter(keys.Client.CipherKey.Slice(0, keySize).Span);
            var clientIv = new byte[ivSize];

            keys.Client.HmacKey.Span.Slice(0, ivSize - 4).CopyTo(clientIv.AsSpan(4));

            var serverCipher = cipherFactory.Invoke();
            var serverKey = new KeyParameter(keys.Server.CipherKey.Slice(0, keySize).Span);
            var serverIv = new byte[ivSize];

            keys.Server.HmacKey.Span.Slice(0, ivSize - 4).CopyTo(serverIv.AsSpan(4));

            if (mode == OpenVpnMode.Server)
            {
                _encryptCipher = serverCipher;
                _encryptKey = serverKey;
                _encryptIv = serverIv;
                _decryptCipher = clientCipher;
                _decryptKey = clientKey;
                _decryptIv = clientIv;
            }
            else if (mode == OpenVpnMode.Client)
            {
                _encryptCipher = clientCipher;
                _encryptKey = clientKey;
                _encryptIv = clientIv;
                _decryptCipher = serverCipher;
                _decryptKey = serverKey;
                _decryptIv = serverIv;
            }
            else
            {
                throw new NotSupportedException("Mode not supported");
            }

            _encryptIvBuffer = new byte[ivSize];
            var encryptAead = new AeadParameters(_encryptKey, _macSize * 8, _encryptIvBuffer, null);
            _encryptCipher.Init(forEncryption: true, encryptAead);

            _decryptIvBuffer = new byte[ivSize];
            var decryptAead = new AeadParameters(_decryptKey, _macSize * 8, _decryptIvBuffer, null);
            _decryptCipher.Init(forEncryption: false, decryptAead);

            _epochFormat = epochFormat;
        }

        public int GetEncryptedSize(int length)
        {
            return
                _packetIdSize +
                _encryptCipher.GetOutputSize(length);
        }

        public int Encrypt(ReadOnlySpan<byte> header, ReadOnlySpan<byte> input, Span<byte> output, uint packetId)
        {
            var outputOffset = 0;

            var packetIdSlice = output.Slice(outputOffset, _packetIdSize);
            outputOffset += _packetIdSize;

            BinaryPrimitives.WriteUInt32BigEndian(packetIdSlice, packetId);
            DeriveIv(packetIdSlice, _encryptIv, _encryptIvBuffer);

            var ciphertextSlice = output.Slice(outputOffset);

            var additionalData = ExactSizedArrayPool<byte>.Shared.Rent(
                header.Length + packetIdSlice.Length
            );

            try
            {
                var additionalDataOffset = 0;

                header.CopyTo(additionalData.AsSpan(additionalDataOffset, header.Length));
                additionalDataOffset += header.Length;

                packetIdSlice.CopyTo(additionalData.AsSpan(additionalDataOffset, packetIdSlice.Length));

                var ciphertextOffset = 0;

                _encryptCipher.Init(
                    forEncryption: true,
                    new AeadParameters(
                        _encryptKey,
                        _macSize * 8,
                        _encryptIvBuffer,
                        additionalData
                    )
                );

                ciphertextOffset += _encryptCipher.ProcessBytes(input, ciphertextSlice.Slice(ciphertextOffset));
                ciphertextOffset += _encryptCipher.DoFinal(ciphertextSlice.Slice(ciphertextOffset));

                ciphertextSlice = ciphertextSlice.Slice(0, ciphertextOffset);
                outputOffset += ciphertextOffset;
            }
            finally
            {
                ExactSizedArrayPool<byte>.Shared.Return(additionalData);
            }

            if (_epochFormat)
                throw new NotSupportedException("Epoch format not supported");
            else
                MoveMacToStart(ciphertextSlice);

            return outputOffset;
        }

        private void MoveMacToStart(Span<byte> ciphertext)
        {
            var macSlice = ciphertext.Slice(ciphertext.Length - _macSize, _macSize);
            macSlice.CopyTo(_encryptMacBuffer);

            ciphertext.MoveRight(_macSize);

            _encryptMacBuffer.CopyTo(ciphertext);
        }

        public int GetDecryptedSize(int length)
        {
            return
                // Allocating additional length for mac moving
                (length - _packetIdSize) +
                _decryptCipher.GetOutputSize(length);
        }

        public int Decrypt(ReadOnlySpan<byte> header, ReadOnlySpan<byte> input, Span<byte> output, out uint packetId)
        {
            var inputOffset = 0;

            var packetIdSlice = input.Slice(inputOffset, _packetIdSize);
            inputOffset += packetIdSlice.Length;

            packetId = BinaryPrimitives.ReadUInt32BigEndian(packetIdSlice);
            DeriveIv(packetIdSlice, _decryptIv, _decryptIvBuffer);

            var inputCiphertextSlice = input.Slice(inputOffset);

            var outputOffset = 0;

            var outputCiphertextSlice = output.Slice(outputOffset, inputCiphertextSlice.Length);
            outputOffset += inputCiphertextSlice.Length;

            inputCiphertextSlice.CopyTo(outputCiphertextSlice);

            var outputPlaintextSlice = output.Slice(outputOffset);

            if (_epochFormat)
                throw new NotSupportedException("Epoch format not supported");
            else
                MoveMacToEnd(outputCiphertextSlice);

            var additionalData = ExactSizedArrayPool<byte>.Shared.Rent(
                header.Length + packetIdSlice.Length
            );

            try
            {
                var additionalDataOffset = 0;

                header.CopyTo(additionalData.AsSpan(additionalDataOffset, header.Length));
                additionalDataOffset += header.Length;

                packetIdSlice.CopyTo(additionalData.AsSpan(additionalDataOffset, packetIdSlice.Length));

                var plaintextOffset = 0;

                _decryptCipher.Init(
                    forEncryption: false,
                    new AeadParameters(
                        _decryptKey, 
                        _macSize * 8, 
                        _decryptIvBuffer, 
                        additionalData
                    )
                );

                plaintextOffset += _decryptCipher.ProcessBytes(outputCiphertextSlice, outputPlaintextSlice.Slice(plaintextOffset));
                plaintextOffset += _decryptCipher.DoFinal(outputPlaintextSlice.Slice(plaintextOffset));

                output.MoveLeft(outputCiphertextSlice.Length);

                return plaintextOffset;
            }
            finally
            {
                ExactSizedArrayPool<byte>.Shared.Return(additionalData);
            }
        }

        private void MoveMacToEnd(Span<byte> ciphertext)
        {
            var macSlice = ciphertext.Slice(0, _macSize);
            macSlice.CopyTo(_decryptMacBuffer);

            ciphertext.MoveLeft(_macSize);

            _decryptMacBuffer.CopyTo(ciphertext.Slice(ciphertext.Length - _macSize));
        }

        private static void DeriveIv(ReadOnlySpan<byte> packetId, ReadOnlySpan<byte> iv, Span<byte> derivedIv)
        {
            if (packetId.Length != _packetIdSize)
                throw new ArgumentException($"Packet id size should be {_packetIdSize}");

            if (iv.Length != derivedIv.Length)
                throw new ArgumentException($"Iv size should be equal to output size");

            for (var i = 0; i < packetId.Length; i++)
                derivedIv[i] = (byte)(packetId[i] ^ iv[i]);

            for (var i = packetId.Length; i < iv.Length; i++)
                derivedIv[i] = iv[i];
        }

        public static AeadCrypto CreateGcm(
            CryptoKeys keys,
            Func<IBlockCipher> cipherFactory,
            int keySize,
            int ivSize,
            OpenVpnMode mode,
            bool epochFormat
        )
        {
            return new AeadCrypto(
                keys,
                () => new GcmBlockCipher(cipherFactory.Invoke()),
                keySize,
                ivSize,
                mode,
                epochFormat
            );
        }
    }
}
