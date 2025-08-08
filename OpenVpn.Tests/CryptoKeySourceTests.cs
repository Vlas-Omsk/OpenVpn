using OpenVpn.Crypto;
using Org.BouncyCastle.Security;

namespace OpenVpn.Tests
{
    public class CryptoKeySourceTests
    {
        [Fact]
        public void Generate_CreatesKeyWithCorrectSize()
        {
            var random = new SecureRandom();

            var keySource = CryptoKeySource.Generate(random);

            Assert.Equal(CryptoKeySource.PreMasterSize, keySource.PreMaster.Length);
            Assert.Equal(CryptoKeySource.RandomSize, keySource.Random1.Length);
            Assert.Equal(CryptoKeySource.RandomSize, keySource.Random2.Length);
        }

        [Fact]
        public void Constructor_WithFullKey_ParsesCorrectly()
        {
            var fullKey = new byte[CryptoKeySource.PreMasterSize + CryptoKeySource.RandomSize + CryptoKeySource.RandomSize];
            var random = new SecureRandom();
            random.NextBytes(fullKey);

            var keySource = new CryptoKeySource(fullKey);

            Assert.Equal(CryptoKeySource.PreMasterSize, keySource.PreMaster.Length);
            Assert.Equal(CryptoKeySource.RandomSize, keySource.Random1.Length);
            Assert.Equal(CryptoKeySource.RandomSize, keySource.Random2.Length);

            Assert.True(fullKey.AsSpan(0, CryptoKeySource.PreMasterSize).SequenceEqual(keySource.PreMaster.Span));
            Assert.True(fullKey.AsSpan(CryptoKeySource.PreMasterSize, CryptoKeySource.RandomSize).SequenceEqual(keySource.Random1.Span));
            Assert.True(fullKey.AsSpan(CryptoKeySource.PreMasterSize + CryptoKeySource.RandomSize, CryptoKeySource.RandomSize).SequenceEqual(keySource.Random2.Span));
        }

        [Fact]
        public void Constructor_WithRandomsOnly_ParsesCorrectly()
        {
            var key = new byte[CryptoKeySource.RandomSize + CryptoKeySource.RandomSize];
            var random = new SecureRandom();
            random.NextBytes(key);

            var keySource = new CryptoKeySource(key);

            Assert.Equal(0, keySource.PreMaster.Length);
            Assert.Equal(CryptoKeySource.RandomSize, keySource.Random1.Length);
            Assert.Equal(CryptoKeySource.RandomSize, keySource.Random2.Length);

            Assert.True(key.AsSpan(0, CryptoKeySource.RandomSize).SequenceEqual(keySource.Random1.Span));
            Assert.True(key.AsSpan(CryptoKeySource.RandomSize, CryptoKeySource.RandomSize).SequenceEqual(keySource.Random2.Span));
        }

        [Fact]
        public void Constructor_WithSeparateComponents_CreatesCorrectly()
        {
            var preMaster = new byte[CryptoKeySource.PreMasterSize];
            var random1 = new byte[CryptoKeySource.RandomSize];
            var random2 = new byte[CryptoKeySource.RandomSize];

            var random = new SecureRandom();
            random.NextBytes(preMaster);
            random.NextBytes(random1);
            random.NextBytes(random2);

            var keySource = new CryptoKeySource(preMaster, random1, random2);

            Assert.True(preMaster.AsSpan().SequenceEqual(keySource.PreMaster.Span));
            Assert.True(random1.AsSpan().SequenceEqual(keySource.Random1.Span));
            Assert.True(random2.AsSpan().SequenceEqual(keySource.Random2.Span));
        }

        [Fact]
        public void Constructor_WithEmptyPreMaster_Works()
        {
            var random1 = new byte[CryptoKeySource.RandomSize];
            var random2 = new byte[CryptoKeySource.RandomSize];

            var random = new SecureRandom();
            random.NextBytes(random1);
            random.NextBytes(random2);

            var keySource = new CryptoKeySource(ReadOnlyMemory<byte>.Empty, random1, random2);

            Assert.Equal(0, keySource.PreMaster.Length);
            Assert.True(random1.AsSpan().SequenceEqual(keySource.Random1.Span));
            Assert.True(random2.AsSpan().SequenceEqual(keySource.Random2.Span));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(50)]
        [InlineData(100)]
        public void Constructor_WithInvalidKeySize_ThrowsArgumentOutOfRangeException(int invalidSize)
        {
            var invalidKey = new byte[invalidSize];

            Assert.Throws<ArgumentOutOfRangeException>(() => new CryptoKeySource(invalidKey));
        }

        [Theory]
        [InlineData(30)]
        [InlineData(60)]
        public void Constructor_WithInvalidPreMasterSize_ThrowsArgumentOutOfRangeException(int preMasterSize)
        {
            var preMaster = new byte[preMasterSize];
            var random1 = new byte[CryptoKeySource.RandomSize];
            var random2 = new byte[CryptoKeySource.RandomSize];

            Assert.Throws<ArgumentOutOfRangeException>(() => new CryptoKeySource(preMaster, random1, random2));
        }

        [Theory]
        [InlineData(30)]
        [InlineData(40)]
        public void Constructor_WithInvalidRandom1Size_ThrowsArgumentOutOfRangeException(int random1Size)
        {
            var preMaster = new byte[CryptoKeySource.PreMasterSize];
            var random1 = new byte[random1Size];
            var random2 = new byte[CryptoKeySource.RandomSize];

            Assert.Throws<ArgumentOutOfRangeException>(() => new CryptoKeySource(preMaster, random1, random2));
        }

        [Theory]
        [InlineData(30)]
        [InlineData(40)]
        public void Constructor_WithInvalidRandom2Size_ThrowsArgumentOutOfRangeException(int random2Size)
        {
            var preMaster = new byte[CryptoKeySource.PreMasterSize];
            var random1 = new byte[CryptoKeySource.RandomSize];
            var random2 = new byte[random2Size];

            Assert.Throws<ArgumentOutOfRangeException>(() => new CryptoKeySource(preMaster, random1, random2));
        }

        [Fact]
        public void Clone_CreatesEqualCopy()
        {
            var original = CryptoKeySource.Generate(new SecureRandom());

            var cloned = original.Clone();

            Assert.True(original.PreMaster.Span.SequenceEqual(cloned.PreMaster.Span));
            Assert.True(original.Random1.Span.SequenceEqual(cloned.Random1.Span));
            Assert.True(original.Random2.Span.SequenceEqual(cloned.Random2.Span));
        }

        [Fact]
        public void Clone_CreatesIndependentCopy()
        {
            var original = CryptoKeySource.Generate(new SecureRandom());
            var cloned = original.Clone();

            original.TryClear();

            Assert.False(cloned.PreMaster.Span.ToArray().All(b => b == 0));
            Assert.False(cloned.Random1.Span.ToArray().All(b => b == 0));
            Assert.False(cloned.Random2.Span.ToArray().All(b => b == 0));
        }

        [Fact]
        public void TryClear_WithMutableKey_ClearsMemory()
        {
            var key = new byte[CryptoKeySource.KeySize];
            new SecureRandom().NextBytes(key);
            var keySource = new CryptoKeySource(key);

            var result = keySource.TryClear();

            Assert.True(result);
            Assert.True(key.All(b => b == 0));
        }

        [Fact]
        public void TryClear_WithReadOnlyKey_ReturnsFalse()
        {
            var key = new byte[CryptoKeySource.KeySize];
            new SecureRandom().NextBytes(key);
            var keySource = new CryptoKeySource((ReadOnlyMemory<byte>)key);

            var result = keySource.TryClear();

            Assert.False(result);
        }

        [Fact]
        public void Generate_ProducesDifferentKeys()
        {
            var random = new SecureRandom();

            var key1 = CryptoKeySource.Generate(random);
            var key2 = CryptoKeySource.Generate(random);

            Assert.False(
                key1.PreMaster.Span.SequenceEqual(key2.PreMaster.Span) ||
                key1.Random1.Span.SequenceEqual(key2.Random1.Span) ||
                key1.Random2.Span.SequenceEqual(key2.Random2.Span)
            );
        }
    }
}