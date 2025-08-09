using System.Buffers.Binary;

namespace OpenVpn.Data.Crypto
{
    internal sealed class PlainCrypto : IDataCrypto
    {
        private const int _packetIdSize = 4;

        public int GetEncryptedSize(int length)
        {
            return _packetIdSize + length;
        }

        public int Encrypt(ReadOnlySpan<byte> header, ReadOnlySpan<byte> input, Span<byte> output, uint packetId)
        {
            var outputOffset = 0;

            BinaryPrimitives.WriteUInt32BigEndian(output.Slice(outputOffset, _packetIdSize), packetId);
            outputOffset += _packetIdSize;

            input.CopyTo(output.Slice(outputOffset));
            outputOffset += input.Length;

            return outputOffset;
        }

        public int GetDecryptedSize(int length)
        {
            return length - _packetIdSize;
        }

        public int Decrypt(ReadOnlySpan<byte> header, ReadOnlySpan<byte> input, Span<byte> output, out uint packetId)
        {
            var inputOffset = 0;

            packetId = BinaryPrimitives.ReadUInt32BigEndian(input.Slice(inputOffset, _packetIdSize));
            inputOffset += _packetIdSize;

            input.Slice(inputOffset).CopyTo(output);

            return input.Length - inputOffset;
        }
    }
}
