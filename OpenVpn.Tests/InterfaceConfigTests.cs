using System.Net;

namespace OpenVpn.Tests
{
    public class InterfaceConfigTests
    {
        [Theory]
        [InlineData("192.168.1.100 255.255.255.0", "192.168.1.1", "192.168.1.100", 24, "192.168.1.1")]
        [InlineData("192.168.1.100 255.255.255.0", null, "192.168.1.100", 24, null)]
        [InlineData("192.168.1.100 255.255.0.0", "192.168.1.1", "192.168.1.100", 16, "192.168.1.1")]
        [InlineData("172.16.0.5 255.255.240.0", "172.16.0.1", "172.16.0.5", 20, "172.16.0.1")]
        [InlineData("192.168.100.10 255.255.255.252", "192.168.1.1", "192.168.100.10", 30, "192.168.1.1")]
        public void ParseIpv4_ValidFormat_WorkCorrectly(string ifconfig, string? gateway, string expectedAddress, int expectedMask, string? expectedGateway)
        {
            var result = InterfaceConfig.ParseIpv4(ifconfig, gateway);

            Assert.Equal(IPAddress.Parse(expectedAddress), result.Address);
            Assert.Equal(expectedMask, result.Mask);
            Assert.Equal(expectedGateway == null ? null : IPAddress.Parse(expectedGateway), result.Gateway);
        }

        [Fact]
        public void ParseIpv4_InvalidFormat_ThrowsFormatException()
        {
            var invalidConfig = "192.168.1.100";

            Assert.Throws<FormatException>(() => InterfaceConfig.ParseIpv4(invalidConfig, null));
        }

        [Fact]
        public void ParseIpv4_TooManyParts_ThrowsFormatException()
        {
            var invalidConfig = "192.168.1.100 255.255.255.0 extra";

            Assert.Throws<FormatException>(() => InterfaceConfig.ParseIpv4(invalidConfig, null));
        }

        [Fact]
        public void ParseIpv4_InvalidIPAddress_ThrowsFormatException()
        {
            var invalidConfig = "999.999.999.999 255.255.255.0";

            Assert.Throws<FormatException>(() => InterfaceConfig.ParseIpv4(invalidConfig, null));
        }

        [Fact]
        public void ParseIpv4_InvalidMask_ThrowsFormatException()
        {
            var invalidConfig = "192.168.1.100 255.255.255.129";

            Assert.Throws<FormatException>(() => InterfaceConfig.ParseIpv4(invalidConfig, null));
        }

        [Theory]
        [InlineData("255.255.255.255", 32)]
        [InlineData("255.255.255.0", 24)]
        [InlineData("255.255.0.0", 16)]
        [InlineData("255.0.0.0", 8)]
        [InlineData("0.0.0.0", 0)]
        public void ParseIpv4_ValidMasks_ReturnsCorrectBits(string mask, int expectedBits)
        {
            var ifconfig = $"192.168.1.100 {mask}";

            var result = InterfaceConfig.ParseIpv4(ifconfig, null);

            Assert.Equal(expectedBits, result.Mask);
        }

        [Theory]
        [InlineData("255.255.255.254", 31)]
        [InlineData("255.255.255.252", 30)]
        [InlineData("255.255.255.248", 29)]
        [InlineData("255.255.255.240", 28)]
        [InlineData("255.255.255.224", 27)]
        [InlineData("255.255.255.192", 26)]
        [InlineData("255.255.255.128", 25)]
        public void ParseIpv4_SubnetMasks_ReturnsCorrectBits(string mask, int expectedBits)
        {
            var ifconfig = $"10.0.0.1 {mask}";

            var result = InterfaceConfig.ParseIpv4(ifconfig, null);

            Assert.Equal(expectedBits, result.Mask);
        }

        [Fact]
        public void ParseIpv4_InvalidMaskFormat_ThrowsFormatException()
        {
            var invalidConfig = "192.168.1.100 255.255.255";

            Assert.Throws<FormatException>(() => InterfaceConfig.ParseIpv4(invalidConfig, null));
        }

        [Fact]
        public void ParseIpv4_InvalidMaskValue_ThrowsFormatException()
        {
            var invalidConfig = "192.168.1.100 255.255.255.256";

            Assert.Throws<FormatException>(() => InterfaceConfig.ParseIpv4(invalidConfig, null));
        }

        [Fact]
        public void ParseIpv4_NullOrEmptyMask_ThrowsFormatException()
        {
            var invalidConfig = "192.168.1.100 ";

            Assert.Throws<FormatException>(() => InterfaceConfig.ParseIpv4(invalidConfig, null));
        }

        [Fact]
        public void ParseIpv4_IncreasingMask_ThrowsFormatException()
        {
            var invalidConfig = "192.168.1.100 128.255.255.255";

            Assert.Throws<FormatException>(() => InterfaceConfig.ParseIpv4(invalidConfig, null));
        }

        [Theory]
        [InlineData("2001:db8::1/64 2001:db8::1", "2001:db8::1", 64, "2001:db8::1")]
        [InlineData("2001:db8::/32 2001:db8::1", "2001:db8::", 32, "2001:db8::1")]
        [InlineData("fd00::1/64 fd00::1", "fd00::1", 64, "fd00::1")]
        [InlineData("fe80::1/128 fe80::1", "fe80::1", 128, "fe80::1")]
        public void ParseIpv6_ValidFormat_WorkCorrectly(string ifconfig, string expectedAddress, int expectedMask, string expectedGateway)
        {
            var result = InterfaceConfig.ParseIpv6(ifconfig);

            Assert.Equal(IPAddress.Parse(expectedAddress), result.Address);
            Assert.Equal(expectedMask, result.Mask);
            Assert.Equal(IPAddress.Parse(expectedGateway), result.Gateway);
        }

        [Fact]
        public void ParseIpv6_InvalidFormat_ThrowsFormatException()
        {
            var invalidConfig = "2001:db8::1/64";

            Assert.Throws<FormatException>(() => InterfaceConfig.ParseIpv6(invalidConfig));
        }

        [Fact]
        public void ParseIpv6_TooManyParts_ThrowsFormatException()
        {
            var invalidConfig = "2001:db8::1/64 2001:db8::1 extra";

            Assert.Throws<FormatException>(() => InterfaceConfig.ParseIpv6(invalidConfig));
        }

        [Fact]
        public void ParseIpv6_InvalidIPAddress_ThrowsFormatException()
        {
            var invalidConfig = "invalid::address/64 2001:db8::1";

            Assert.Throws<FormatException>(() => InterfaceConfig.ParseIpv6(invalidConfig));
        }

        [Fact]
        public void ParseIpv6_InvalidMask_ThrowsFormatException()
        {
            var invalidConfig = "2001:db8::1/abc 2001:db8::1";

            Assert.Throws<FormatException>(() => InterfaceConfig.ParseIpv6(invalidConfig));
        }

        [Fact]
        public void ParseIpv6_InvalidGateway_ThrowsFormatException()
        {
            var invalidConfig = "2001:db8::1/64 invalid::gateway";

            Assert.Throws<FormatException>(() => InterfaceConfig.ParseIpv6(invalidConfig));
        }

        [Theory]
        [InlineData("2001:db8::1/128", 128)]
        [InlineData("2001:db8::1/64", 64)]
        [InlineData("2001:db8::1/48", 48)]
        [InlineData("2001:db8::1/32", 32)]
        [InlineData("2001:db8::1/0", 0)]
        public void ParseIpv6_ValidPrefixLengths_ReturnsCorrectMask(string addressWithPrefix, int expectedMask)
        {
            var ifconfig = $"{addressWithPrefix} 2001:db8::1";

            var result = InterfaceConfig.ParseIpv6(ifconfig);

            Assert.Equal(expectedMask, result.Mask);
        }

        [Theory]
        [InlineData("255.255.255.254", 31)]
        [InlineData("255.255.252.0", 22)]
        [InlineData("255.224.0.0", 11)]
        [InlineData("128.0.0.0", 1)]
        public void MaskToBits_EdgeCases_HandleCorrectly(string mask, int expectedMask)
        {
            var ifconfig = $"192.168.1.1 {mask}";
            var result = InterfaceConfig.ParseIpv4(ifconfig, null);

            Assert.Equal(expectedMask, result.Mask);
        }
    }
}