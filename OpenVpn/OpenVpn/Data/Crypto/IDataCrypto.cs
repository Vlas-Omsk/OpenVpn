namespace OpenVpn.Data.Crypto
{
    internal interface IDataCrypto
    {
        int GetEncryptedSize(int length);
        int Encrypt(ReadOnlySpan<byte> header, ReadOnlySpan<byte> input, Span<byte> output, uint packetId);
        int GetDecryptedSize(int length);
        int Decrypt(ReadOnlySpan<byte> header, ReadOnlySpan<byte> input, Span<byte> output, out uint packetId);
    }
}
