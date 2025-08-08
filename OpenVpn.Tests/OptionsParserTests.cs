using System.Collections.Immutable;
using OpenVpn.Options;

namespace OpenVpn.Tests
{
    public class OptionsParserTests
    {
        [Fact]
        public void Stringify_EmptyDictionary_ReturnsEmptyString()
        {
            var options = ImmutableDictionary<string, string?>.Empty;

            var result = OptionsParser.Stringify(options, ',', '=');

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Stringify_SingleKeyNoValue_ReturnsKeyOnly()
        {
            var options = new Dictionary<string, string?>()
            {
                { "verbose", null }
            };

            var result = OptionsParser.Stringify(options, ',', '=');

            Assert.Equal("verbose", result);
        }

        [Fact]
        public void Stringify_SingleKeyWithValue_ReturnsKeyValuePair()
        {
            var options = new Dictionary<string, string?>()
            {
                { "port", "1194" }
            };

            var result = OptionsParser.Stringify(options, ',', '=');

            Assert.Equal("port=1194", result);
        }

        [Theory]
        [InlineData(',', '=')]
        [InlineData(';', ':')]
        [InlineData('\n', ' ')]
        public void Stringify_MultipleOptions_ReturnsCorrectFormat(char separator, char keyValueSeparator)
        {
            var options = new Dictionary<string, string?>()
            {
                { "verbose", null },
                { "port", "1194" },
                { "proto", "udp" },
                { "comp-lzo", null }
            };

            var result = OptionsParser.Stringify(options, separator, keyValueSeparator);

            Assert.Contains("verbose", result);
            Assert.Contains($"port{keyValueSeparator}1194", result);
            Assert.Contains($"proto{keyValueSeparator}udp", result);
            Assert.Contains("comp-lzo", result);

            var separatorCount = result.Count(c => c == separator);
            Assert.Equal(3, separatorCount);
        }

        [Fact]
        public void Parse_EmptyString_ReturnsEmptyDictionary()
        {
            using var reader = new StringReader("");

            var result = OptionsParser.Parse(reader, ',', '=');

            Assert.Empty(result);
        }

        [Fact]
        public void Parse_SingleKeyNoValue_ReturnsCorrectDictionary()
        {
            using var reader = new StringReader("verbose");

            var result = OptionsParser.Parse(reader, ',', '=');

            Assert.Single(result);
            Assert.True(result.ContainsKey("verbose"));
            Assert.Null(result["verbose"]);
        }

        [Fact]
        public void Parse_SingleKeyWithValue_ReturnsCorrectDictionary()
        {
            using var reader = new StringReader("port=1194");

            var result = OptionsParser.Parse(reader, ',', '=');

            Assert.Single(result);
            Assert.True(result.ContainsKey("port"));
            Assert.Equal("1194", result["port"]);
        }

        [Fact]
        public void Parse_MultipleOptions_ReturnsCorrectDictionary()
        {
            using var reader = new StringReader("verbose,port=1194,proto=udp,comp-lzo");

            var result = OptionsParser.Parse(reader, ',', '=');

            Assert.Equal(4, result.Count);
            Assert.True(result.ContainsKey("verbose"));
            Assert.Null(result["verbose"]);
            Assert.Equal("1194", result["port"]);
            Assert.Equal("udp", result["proto"]);
            Assert.True(result.ContainsKey("comp-lzo"));
            Assert.Null(result["comp-lzo"]);
        }

        [Fact]
        public void Parse_CustomSeparators_ParsesCorrectly()
        {
            using var reader = new StringReader("key1:value1;key2;key3:value3");

            var result = OptionsParser.Parse(reader, ';', ':');

            Assert.Equal(3, result.Count);
            Assert.Equal("value1", result["key1"]);
            Assert.Null(result["key2"]);
            Assert.Equal("value3", result["key3"]);
        }

        [Fact]
        public void Parse_EmptyKeys_Throws()
        {
            using var reader = new StringReader(",,key1=value1,,");

            Assert.Throws<ArgumentException>(() => OptionsParser.Parse(reader, ',', '='));
        }

        [Fact]
        public void Parse_EmptyValues_TreatsAsEmpty()
        {
            using var reader = new StringReader("key1=,key2=value2");

            var result = OptionsParser.Parse(reader, ',', '=');

            Assert.Equal(2, result.Count);
            Assert.Equal("", result["key1"]);
            Assert.Equal("value2", result["key2"]);
        }

        [Fact]
        public void Parse_SpecialCharactersInValues_HandlesCorrectly()
        {
            using var reader = new StringReader("path=/etc/openvpn,path2=\\etc\\openvpn,desc=VPN Server");

            var result = OptionsParser.Parse(reader, ',', '=');

            Assert.Equal(3, result.Count);
            Assert.Equal("/etc/openvpn", result["path"]);
            Assert.Equal("\\etc\\openvpn", result["path2"]);
            Assert.Equal("VPN Server", result["desc"]);
        }

        [Fact]
        public void StringifyThenParse_RoundTrip_PreservesData()
        {
            var originalOptions = new Dictionary<string, string?>()
            {
                { "verbose", null },
                { "port", "1194" },
                { "proto", "udp" },
                { "dev", "tun0" }
            };

            var stringified = OptionsParser.Stringify(originalOptions, ',', '=');
            using var reader = new StringReader(stringified);
            var parsed = OptionsParser.Parse(reader, ',', '=');

            Assert.Equal(originalOptions.Count, parsed.Count);
            foreach (var kvp in originalOptions)
            {
                Assert.True(parsed.ContainsKey(kvp.Key));
                Assert.Equal(kvp.Value, parsed[kvp.Key]);
            }
        }

        [Theory]
        [InlineData("key=value", '=', ':', "key", "value")]
        [InlineData("key:value", ':', '=', "key", "value")]
        [InlineData("key", '=', ':', "key", null)]
        public void Parse_DifferentSeparatorCombinations_WorksCorrectly(
            string input, char keyValueSep, char itemSep, string expectedKey, string? expectedValue
        )
        {
            using var reader = new StringReader(input);

            var result = OptionsParser.Parse(reader, itemSep, keyValueSep);

            Assert.Single(result);
            Assert.True(result.ContainsKey(expectedKey));
            Assert.Equal(expectedValue, result[expectedKey]);
        }

        [Fact]
        public void Parse_LargeInput_WorksCorrectly()
        {
            var options = new List<string>();
            for (int i = 0; i < 1000; i++)
            {
                options.Add($"key{i}=value{i}");
            }
            var input = string.Join(",", options);
            using var reader = new StringReader(input);

            var result = OptionsParser.Parse(reader, ',', '=');

            Assert.Equal(1000, result.Count);
            Assert.Equal("value500", result["key500"]);
            Assert.Equal("value999", result["key999"]);
        }
    }
}