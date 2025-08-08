using Org.BouncyCastle.Security;

namespace OpenVpn.Crypto
{
    internal readonly struct CryptoKeySource
    {
        public const int KeySize = 112;
        public const int PreMasterSize = 48;
        public const int RandomSize = 32;
        private readonly Memory<byte>? _key;
        private readonly ReadOnlyMemory<byte>? _readOnlyKey;

        public CryptoKeySource(
            Memory<byte> key
        ) : this((ReadOnlyMemory<byte>)key)
        {
            _key = key;
        }

        public CryptoKeySource(
            ReadOnlyMemory<byte> key
        )
        {
            if (key.Length != PreMasterSize + RandomSize + RandomSize &&
                key.Length != RandomSize + RandomSize)
                throw new ArgumentOutOfRangeException(nameof(key), $"Bytes length must equal to {PreMasterSize + RandomSize + RandomSize} or {RandomSize + RandomSize}");

            _readOnlyKey = key;

            var offset = 0;

            if (key.Length == PreMasterSize + RandomSize + RandomSize)
            {
                PreMaster = key.Slice(offset, PreMasterSize);
                offset += PreMasterSize;
            }

            Random1 = key.Slice(offset, RandomSize);
            offset += RandomSize;

            Random2 = key.Slice(offset, RandomSize);
            offset += RandomSize;
        }

        public CryptoKeySource(
            ReadOnlyMemory<byte> preMaster,
            ReadOnlyMemory<byte> random1,
            ReadOnlyMemory<byte> random2
        )
        {
            if (preMaster.Length != 0)
                ArgumentOutOfRangeException.ThrowIfNotEqual(preMaster.Length, PreMasterSize, nameof(preMaster));

            ArgumentOutOfRangeException.ThrowIfNotEqual(random1.Length, RandomSize, nameof(random1));
            ArgumentOutOfRangeException.ThrowIfNotEqual(random2.Length, RandomSize, nameof(random2));

            PreMaster = preMaster;
            Random1 = random1;
            Random2 = random2;
        }

        public ReadOnlyMemory<byte> PreMaster { get; }
        public ReadOnlyMemory<byte> Random1 { get; }
        public ReadOnlyMemory<byte> Random2 { get; }

        public CryptoKeySource Clone()
        {
            if (_readOnlyKey.HasValue)
            {
                return new CryptoKeySource(_readOnlyKey.Value.ToArray());
            }
            else
            {
                var key = new byte[PreMaster.Length + Random1.Length + Random2.Length];

                PreMaster.Span.CopyTo(key.AsSpan(0, PreMaster.Length));
                Random1.Span.CopyTo(key.AsSpan(PreMaster.Length, Random1.Length));
                Random2.Span.CopyTo(key.AsSpan(PreMaster.Length + Random1.Length, Random2.Length));

                return new CryptoKeySource(key);
            }
        }

        public bool TryClear()
        {
            if (_key.HasValue)
            {
                _key.Value.Span.Clear();
                return true;
            }

            return false;
        }

        public static CryptoKeySource Generate(SecureRandom random)
        {
            var key = new byte[KeySize];

            random.NextBytes(key);

            return new CryptoKeySource(key);
        }
    }
}
