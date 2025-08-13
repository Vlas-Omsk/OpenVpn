using System.ComponentModel.DataAnnotations;
using OpenVpn.Options;
using OpenVpn.Options.Converters;

namespace OpenVpn.Tests
{
    public class OptionsSerializerTests
    {
        internal class TestOptions
        {
            [Name("string-option")]
            public string? StringOption { get; set; }
            [Name("int-option")]
            public int IntOption { get; set; }
            [Name("bool-option")]
            public bool BoolOption { get; set; }
            [Name("enum-option")]
            public TestEnum EnumOption { get; set; }
            [Required]
            [Name("required-option")]
            public string? RequiredOption { get; set; }
            [Name("nullable-int")]
            public int? NullableInt { get; set; }
            [Converter(typeof(SplitOptionConverter), ' ')]
            [Name("split-option")]
            public string[]? SplitOption { get; set; }
        }

        internal enum TestEnum
        {
            Value1,
            Value2,
            Value3
        }

        [Fact]
        public void Serialize_EmptyDictionary_ReturnsObjectWithDefaults()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>();

            var result = serializer.Serialize<TestOptions>(options);

            Assert.NotNull(result);
            Assert.Null(result.StringOption);
            Assert.Equal(0, result.IntOption);
            Assert.False(result.BoolOption);
            Assert.Equal(TestEnum.Value1, result.EnumOption);
        }

        [Fact]
        public void Serialize_StringOption_SetsCorrectly()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "string-option", new[] { "test-value" } }
            };

            var result = serializer.Serialize<TestOptions>(options);

            Assert.Equal("test-value", result.StringOption);
        }

        [Fact]
        public void Serialize_IntOption_SetsCorrectly()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "int-option", new[] { "42" } }
            };

            var result = serializer.Serialize<TestOptions>(options);

            Assert.Equal(42, result.IntOption);
        }

        [Fact]
        public void Serialize_BoolOption_SetsCorrectly()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "bool-option", null }
            };

            var result = serializer.Serialize<TestOptions>(options);

            Assert.True(result.BoolOption);
        }

        [Fact]
        public void Serialize_EnumOption_SetsCorrectly()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "enum-option", new[] { "Value2" } }
            };

            var result = serializer.Serialize<TestOptions>(options);

            Assert.Equal(TestEnum.Value2, result.EnumOption);
        }

        [Fact]
        public void Serialize_RequiredOption_WithValue_SetsCorrectly()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "required-option", new[] { "required-value" } }
            };

            var result = serializer.Serialize<TestOptions>(options);

            Assert.Equal("required-value", result.RequiredOption);
        }

        [Fact]
        public void Serialize_RequiredOption_WithNullValue_ThrowsFormatException()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "required-option", null }
            };

            var exception = Assert.Throws<FormatException>(() => serializer.Serialize<TestOptions>(options));
            Assert.Contains("required-option", exception.Message);
        }

        [Fact]
        public void Serialize_NullableInt_WithValue_SetsCorrectly()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "nullable-int", new[] { "123" } }
            };

            var result = serializer.Serialize<TestOptions>(options);

            Assert.Equal(123, result.NullableInt);
        }

        [Fact]
        public void Serialize_NullableInt_WithNull_RemainsNull()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>();

            var result = serializer.Serialize<TestOptions>(options);

            Assert.Null(result.NullableInt);
        }

        [Fact]
        public void Serialize_SplitOption_SplitsCorrectly()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "split-option", new[] { "value1 value2 value3" } }
            };

            var result = serializer.Serialize<TestOptions>(options);

            Assert.NotNull(result.SplitOption);
            Assert.Equal(new[] { "value1", "value2", "value3" }, result.SplitOption);
        }

        [Fact]
        public void Serialize_MultipleOptions_SetsAllCorrectly()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "string-option", new[] { "test" } },
                { "int-option", new[] { "100" } },
                { "bool-option", null },
                { "enum-option", new[] { "Value3" } },
                { "required-option", new[] { "required" } }
            };

            var result = serializer.Serialize<TestOptions>(options);

            Assert.Equal("test", result.StringOption);
            Assert.Equal(100, result.IntOption);
            Assert.True(result.BoolOption);
            Assert.Equal(TestEnum.Value3, result.EnumOption);
            Assert.Equal("required", result.RequiredOption);
        }

        [Fact]
        public void Serialize_UnknownOptions_StoresInUnknownOptions()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "string-option", new[] { "test" } },
                { "unknown-option1", new[] { "value1" } },
                { "unknown-option2", null }
            };

            var result = serializer.Serialize<TestOptions>(options);

            Assert.Equal("test", result.StringOption);
            Assert.Contains("unknown-option1", serializer.UnknownOptions.Keys);
            Assert.Contains("unknown-option2", serializer.UnknownOptions.Keys);
            Assert.Equal("value1", serializer.UnknownOptions["unknown-option1"]?[0]);
            Assert.Null(serializer.UnknownOptions["unknown-option2"]);
        }

        [Fact]
        public void Serialize_InvalidIntValue_ThrowsFormatException()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "int-option", new[] { "not-a-number" } }
            };

            Assert.Throws<FormatException>(() => serializer.Serialize<TestOptions>(options));
        }

        [Fact]
        public void Serialize_InvalidEnumValue_ThrowsArgumentException()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "enum-option", new[] { "InvalidValue" } }
            };

            Assert.Throws<ArgumentException>(() => serializer.Serialize<TestOptions>(options));
        }

        [Fact]
        public void Serialize_RealPushOptions_WorksCorrectly()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "route-nopull", null },
                { "route-gateway", new[] { "10.8.0.1" } },
                { "cipher", new[] { "AES-256-GCM" } },
                { "tun-mtu", new[] { "1500" } },
                { "ping", new[] { "10" } },
                { "ping-restart", new[] { "120" } },
                { "topology", new[] { "subnet" } },
                { "peer-id", new[] { "1" } }
            };

            var result = serializer.Serialize<PushOptions>(options);

            Assert.True(result.RouteNoPull);
            Assert.Equal("10.8.0.1", result.RouteGateway);
            Assert.Equal("AES-256-GCM", result.Cipher);
            Assert.Equal(1500, result.TunMtu);
            Assert.Equal(10, result.Ping);
            Assert.Equal(120, result.PingRestart);
            Assert.Equal(OpenVpnTopology.Subnet, result.Topology);
            Assert.Equal(1u, result.PeerId);
        }

        [Fact]
        public void Serialize_EmptyValues_HandleCorrectly()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>
            {
                { "string-option", new[] { "" } },
                { "int-option", new[] { "0" } }
            };

            var result = serializer.Serialize<TestOptions>(options);

            Assert.Equal("", result.StringOption);
            Assert.Equal(0, result.IntOption);
        }

        internal class InvalidOptions
        {
            public string? PropertyWithoutName { get; set; }
        }

        [Fact]
        public void Serialize_PropertyWithoutName_ThrowsNotSupportedException()
        {
            var serializer = new OptionsSerializer();
            var options = new Dictionary<string, IReadOnlyList<string>?>();

            Assert.Throws<NotSupportedException>(() => serializer.Serialize<InvalidOptions>(options));
        }
    }
}