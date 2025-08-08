using System.Collections.Immutable;

namespace OpenVpn.Tests
{
    public class DictionaryExtensionsTests
    {
        [Fact]
        public void TryGetRequiredValue_EmptyDictionary_ReturnsFalse()
        {
            var dictionary = ImmutableDictionary<string, string?>.Empty;

            var result = dictionary.TryGetRequiredValue("anykey", out var value);

            Assert.False(result);
            Assert.Null(value);
        }

        [Theory]
        [InlineData("key1", "key1", "value", true)]
        [InlineData("key1", "key1", null, false)]
        [InlineData("key1", "key2", null, false)]
        [InlineData("key1", "key1", "", true)]
        [InlineData("key1", "key1", "   ", true)]
        public void TryGetRequiredValue_VariousValidValues_ReturnsTrue(string keyToAdd, string keyToGet, string? expectedValue, bool expectedResult)
        {
            var dictionary = new Dictionary<string, string?>()
            {
                { keyToAdd, expectedValue }
            };

            var actualResult = dictionary.TryGetRequiredValue(keyToGet, out var actualValue);

            Assert.True(actualResult == expectedResult);
            Assert.Equal(expectedValue, actualValue);
        }

        [Fact]
        public void TryGetRequiredValue_CaseSensitiveKeys_WorksCorrectly()
        {
            var dictionary = new Dictionary<string, string?>()
            {
                { "Key1", "value1" },
                { "key1", "value2" }
            };

            Assert.True(dictionary.TryGetRequiredValue("Key1", out var value1));
            Assert.Equal("value1", value1);

            Assert.True(dictionary.TryGetRequiredValue("key1", out var value2));
            Assert.Equal("value2", value2);

            Assert.False(dictionary.TryGetRequiredValue("KEY1", out var value3));
            Assert.Null(value3);
        }

        [Fact]
        public void TryGetRequiredValue_NullKey_ThrowsException()
        {
            var dictionary = ImmutableDictionary<string, string?>.Empty;

            Assert.Throws<ArgumentNullException>(() =>
                dictionary.TryGetRequiredValue(null!, out var value));
        }
    }
}