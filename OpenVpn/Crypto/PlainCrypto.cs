namespace OpenVpn.Crypto
{
    internal sealed class PlainCrypto : ICrypto
    {
        public int GetEncryptedSize(int length)
        {
            return length;
        }

        public int Encrypt(ReadOnlySpan<byte> additionalData, ReadOnlySpan<byte> input, Span<byte> output)
        {
            input.CopyTo(output);

            return input.Length;
        }

        public int GetDecryptedSize(int length)
        {
            return length;
        }

        public int Decrypt(ReadOnlySpan<byte> additionalData, ReadOnlySpan<byte> input, Span<byte> output)
        {
            input.CopyTo(output);

            return input.Length;
        }
    }
}
