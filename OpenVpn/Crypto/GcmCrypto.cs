using System.Buffers;
using System.Buffers.Binary;
using CommunityToolkit.HighPerformance;
using OpenVpn.Buffers;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace OpenVpn.Crypto
{
    internal sealed class GcmCrypto : ICrypto
    {
        private const int _macSize = 16;
        private const int _packetIdSize = 4;
        private readonly GcmBlockCipher _encryptCipher;
        private readonly KeyParameter _encryptKey;
        private readonly ReadOnlyMemory<byte> _encryptIv;
        private readonly byte[] _encryptIvBuffer;
        private readonly byte[] _encryptMacBuffer = new byte[_macSize];
        private readonly GcmBlockCipher _decryptCipher;
        private readonly KeyParameter _decryptKey;
        private readonly ReadOnlyMemory<byte> _decryptIv;
        private readonly byte[] _decryptIvBuffer;
        private readonly byte[] _decryptMacBuffer = new byte[_macSize];
        private readonly bool _epochFormat;
        private uint _packetId = 0;

        public GcmCrypto(
            CryptoKeys keys,
            Func<IBlockCipher> cipherFactory,
            int keySize,
            int ivSize,
            OpenVpnMode mode,
            bool epochFormat
        )
        {
            var clientCipher = new GcmBlockCipher(cipherFactory.Invoke());
            var clientKey = new KeyParameter(keys.Client.CipherKey.Slice(0, keySize).Span);
            var clientIv = new byte[ivSize];

            keys.Client.HmacKey.Span.Slice(0, ivSize - 4).CopyTo(clientIv.AsSpan(4));

            var serverCipher = new GcmBlockCipher(cipherFactory.Invoke());
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

            var aead = new AeadParameters(_decryptKey, _macSize * 8, _decryptIvBuffer, null);

            _decryptCipher.Init(forEncryption: false, aead);

            _epochFormat = epochFormat;
        }

        public int GetEncryptedSize(int length)
        {
            return
                _packetIdSize +
                _encryptCipher.GetOutputSize(length);
        }

        public int Encrypt(ReadOnlySpan<byte> additionalData, ReadOnlySpan<byte> input, Span<byte> output)
        {
            var packetId = ++_packetId;

            var packetIdSlice = output.Slice(0, _packetIdSize);
            BinaryPrimitives.WriteUInt32BigEndian(packetIdSlice, packetId);

            DeriveIv(packetIdSlice, _encryptIv.Span, _encryptIvBuffer);

            output = output.Slice(_packetIdSize);

            var mergedAdditionalData = ExactSizedArrayPool<byte>.Shared.Rent(additionalData.Length + packetIdSlice.Length);

            try
            {
                additionalData.CopyTo(mergedAdditionalData);
                packetIdSlice.CopyTo(mergedAdditionalData.AsSpan(additionalData.Length));

                var length = EncryptData(input, _encryptIvBuffer, mergedAdditionalData, output);
                output = output.Slice(0, length);
            }
            finally
            {
                ExactSizedArrayPool<byte>.Shared.Return(mergedAdditionalData);
            }

            if (_epochFormat)
                throw new NotSupportedException("Epoch format not supported");
            else
                MoveMacToStart(output);

            return _packetIdSize + output.Length;
        }

        private int EncryptData(ReadOnlySpan<byte> input, byte[] iv, byte[] ad, Span<byte> output)
        {
            var aead = new AeadParameters(_encryptKey, _macSize * 8, iv, ad);

            _encryptCipher.Init(forEncryption: true, aead);

            var length = _encryptCipher.ProcessBytes(input, output);
            output = output.Slice(length);

            length += _encryptCipher.DoFinal(output);

            return length;
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
            // Allocating additional length for mac moving
            return length - _packetIdSize + _decryptCipher.GetOutputSize(length);
        }

        public int Decrypt(ReadOnlySpan<byte> additionalData, ReadOnlySpan<byte> input, Span<byte> output)
        {
            var packetIdSlice = input.Slice(0, _packetIdSize);

            DeriveIv(packetIdSlice, _decryptIv.Span, _decryptIvBuffer);

            input.Slice(_packetIdSize).CopyTo(output);

            var inputSilce = output.Slice(0, input.Length - _packetIdSize);
            var outputSlice = output.Slice(inputSilce.Length);

            if (_epochFormat)
                throw new NotSupportedException("Epoch format not supported");
            else
                MoveMacToEnd(inputSilce);

            var mergedAdditionalData = ExactSizedArrayPool<byte>.Shared.Rent(additionalData.Length + packetIdSlice.Length);

            try
            {
                additionalData.CopyTo(mergedAdditionalData);
                packetIdSlice.CopyTo(mergedAdditionalData.AsSpan(additionalData.Length));

                var length = DecryptData(inputSilce, _decryptIvBuffer, mergedAdditionalData, outputSlice);

                output.MoveLeft(inputSilce.Length);

                return length;
            }
            finally
            {
                ExactSizedArrayPool<byte>.Shared.Return(mergedAdditionalData);
            }
        }

        private int DecryptData(ReadOnlySpan<byte> input, byte[] iv, byte[] ad, Span<byte> output)
        {
            var aead = new AeadParameters(_decryptKey, _macSize * 8, iv, ad);

            _decryptCipher.Init(forEncryption: false, aead);

            var length = _decryptCipher.ProcessBytes(input, output);
            output = output.Slice(length);

            length += _decryptCipher.DoFinal(output);

            return length;
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
    }
}
