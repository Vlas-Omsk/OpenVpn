using System.Net;
using OpenVpn.Options.Converters;
using PinkSystem.Runtime;

namespace OpenVpn.Tests
{
    public class BoolOptionConverterTests
    {
        [Theory]
        [InlineData(null, typeof(bool), true)]
        [InlineData(null, typeof(bool?), null)]
        [InlineData("true", typeof(bool), true)]
        [InlineData("True", typeof(bool), true)]
        [InlineData("TRUE", typeof(bool), true)]
        [InlineData("false", typeof(bool), false)]
        [InlineData("False", typeof(bool), false)]
        [InlineData("FALSE", typeof(bool), false)]
        public void Convert_ValidString_ConvertsCorrectly(string? str, Type targetType, bool? expectedValue)
        {
            var converter = new BoolOptionConverter();

            var result = converter.Convert("test", str, targetType);

            Assert.Equal(expectedValue, result);
        }

        [Fact]
        public void Convert_InvalidString_ThrowsFormatException()
        {
            var converter = new BoolOptionConverter();

            Assert.Throws<FormatException>(() => converter.Convert("test", "invalid", typeof(bool)));
        }

        [Fact]
        public void Convert_EmptyString_ThrowsFormatException()
        {
            var converter = new BoolOptionConverter();

            Assert.Throws<FormatException>(() => converter.Convert("test", "", typeof(bool)));
        }
    }

    public class StringOptionConverterTests
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("test-string", "test-string")]
        [InlineData("", "")]
        [InlineData("Hello 世界 🌍", "Hello 世界 🌍")]
        public void Convert_ValidString_ConvertsCorrectly(string? str, string? expectedValue)
        {
            var converter = new StringOptionConverter();

            var result = converter.Convert("test", str, typeof(string));

            Assert.Equal(expectedValue, result);
        }
    }

    public class SplitOptionConverterTests
    {
        [Theory]
        [InlineData(' ', null, null)]
        [InlineData(' ', "single", new[] { "single" })]
        [InlineData(' ', "value1 value2 value3", new[] { "value1", "value2", "value3" })]
        [InlineData(' ', "", new[] { "" })]
        [InlineData(' ', "value1 value2 value3 ", new[] { "value1", "value2", "value3", "" })]
        [InlineData(' ', "value1 value2  value3", new[] { "value1", "value2", "", "value3" })]
        [InlineData(' ', "a b c", new[] { "a", "b", "c" })]
        [InlineData(',', "a,b,c", new[] { "a", "b", "c" })]
        [InlineData(';', "x;y;z", new[] { "x", "y", "z" })]
        [InlineData('|', "first|second", new[] { "first", "second" })]
        [InlineData(' ', "redirect-gateway def1 bypass-dhcp", new[] { "redirect-gateway", "def1", "bypass-dhcp" })]
        public void Convert_ValidString_ConvertsCorrectly(char delimiter, string? str, string[]? expectedValue)
        {
            var converter = new SplitOptionConverter(delimiter);

            var result = converter.Convert("test", str, typeof(string[]));

            Assert.Equal(expectedValue, result);
        }
    }

    public class ParseOptionConverterTests
    {
        [Theory]
        [InlineData("42", typeof(int?), 42)]
        [InlineData("42", typeof(int), 42)]
        [InlineData(null, typeof(int?), null)]
        [InlineData("9223372036854775807", typeof(long?), 9223372036854775807)]
        [InlineData("4294967295", typeof(uint?), 4294967295)]
        [InlineData("18446744073709551615", typeof(ulong?), 18446744073709551615)]
        public void Convert_ValidString_ConvertsCorrectly(string? str, Type targetType, object? expectedValue)
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType);

            if (underlyingType == null)
                underlyingType = targetType;

            var converter = ObjectAccessor.Create(typeof(ParseOptionConverter<>).MakeGenericType(underlyingType));

            var result = converter.CallMethod(nameof(ParseOptionConverter<int>.Convert), "test", str, targetType);

            Assert.Equal(expectedValue, result);
        }

        [Fact]
        public void Convert_InvalidString_ThrowsFormatException()
        {
            var converter = new ParseOptionConverter<int>();

            Assert.Throws<FormatException>(() => converter.Convert("test", "not-a-number", typeof(int)));
        }

        [Fact]
        public void Convert_NullValue_WhenTypeAcceptNull_ReturnsNull()
        {
            var converter = new ParseOptionConverter<int>();

            var result = converter.Convert("test", null, typeof(int?));

            Assert.Null(result);
        }

        [Fact]
        public void Convert_NullValue_WhenTypeReference_ReturnsNull()
        {
            var converter = new ParseOptionConverter<IPAddress>();

            var result = converter.Convert("test", null, typeof(IPAddress));

            Assert.Null(result);
        }

        [Fact]
        public void Convert_NullValue_WhenTypeNotAcceptNull_ThrowsFormatException()
        {
            var converter = new ParseOptionConverter<int>();

            Assert.Throws<FormatException>(() => converter.Convert("test", null, typeof(int)));
        }

        [Fact]
        public void Convert_EmptyString_ThrowsFormatException()
        {
            var converter = new ParseOptionConverter<int>();

            Assert.Throws<FormatException>(() => converter.Convert("test", "", typeof(int)));
        }

        [Fact]
        public void Convert_OverflowValue_ThrowsOverflowException()
        {
            var converter = new ParseOptionConverter<int>();

            Assert.Throws<OverflowException>(() => converter.Convert("test", "9999999999999999999", typeof(int)));
        }
    }

    public class EnumOptionConverterTests
    {
        public enum TestEnum
        {
            Value1,
            Value2,
            SomeValue
        }

        [Theory]
        [InlineData("Value2", typeof(TestEnum?), TestEnum.Value2)]
        [InlineData("Value2", typeof(TestEnum), TestEnum.Value2)]
        [InlineData("value2", typeof(TestEnum?), TestEnum.Value2)]
        [InlineData(null, typeof(TestEnum?), null)]
        [InlineData("1", typeof(TestEnum?), TestEnum.Value2)]
        public void Convert_ValidString_ConvertsCorrectly(string? str, Type targetType, TestEnum? expectedValue)
        {
            var converter = new EnumOptionConverter();

            var result = converter.Convert("test", str, targetType);

            Assert.Equal(expectedValue, result);
        }

        [Fact]
        public void Convert_NullValue_WhenTypeNotAcceptNull_ThrowsFormatException()
        {
            var converter = new EnumOptionConverter();

            Assert.Throws<FormatException>(() => converter.Convert("test", null, typeof(TestEnum)));
        }

        [Fact]
        public void Convert_InvalidString_ThrowsArgumentException()
        {
            var converter = new EnumOptionConverter();

            Assert.Throws<ArgumentException>(() => converter.Convert("test", "InvalidValue", typeof(TestEnum)));
        }

        [Fact]
        public void Convert_EmptyString_ThrowsArgumentException()
        {
            var converter = new EnumOptionConverter();

            Assert.Throws<ArgumentException>(() => converter.Convert("test", "", typeof(TestEnum)));
        }
    }
}