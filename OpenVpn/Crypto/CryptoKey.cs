namespace OpenVpn.Crypto
{
    internal readonly struct CryptoKey
    {
        public const int KeyLength = _maxCipherKeyLength + _maxHmacKeyLength;
        private const int _maxCipherKeyLength = 64;
        private const int _maxHmacKeyLength = 64;

        public CryptoKey(ReadOnlyMemory<byte> key)
        {
            if (key.Length != KeyLength)
                throw new ArgumentException($"Key should be {KeyLength} length");

            CipherKey = key.Slice(0, _maxCipherKeyLength);
            HmacKey = key.Slice(_maxCipherKeyLength, _maxHmacKeyLength);
        }

        public ReadOnlyMemory<byte> CipherKey { get; }
        public ReadOnlyMemory<byte> HmacKey { get; }
    }
}
