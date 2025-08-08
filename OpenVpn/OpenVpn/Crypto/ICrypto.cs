namespace OpenVpn.Crypto
{
    internal interface ICrypto
    {
        int GetEncryptedSize(int length);
        int Encrypt(ReadOnlySpan<byte> additionalData, ReadOnlySpan<byte> input, Span<byte> output);
        int GetDecryptedSize(int length);
        int Decrypt(ReadOnlySpan<byte> additionalData, ReadOnlySpan<byte> input, Span<byte> output);
    }
}
