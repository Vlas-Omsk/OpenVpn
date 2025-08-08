using System.ComponentModel.DataAnnotations;
using OpenVpn.Options.Converters;

namespace OpenVpn.Options
{
    internal sealed class KeyExchangeOptions
    {
        [Required]
        [Name("dev-type")]
        public required OpenVpnDeviceType DeviceType { get; init; }
        [Converter(typeof(AuthOptionConverter))]
        [Name("auth")]
        public required string? Auth { get; init; }
        [Required]
        [Name("proto")]
        public required string Protocol { get; init; }
        [Required]
        [Name("link-mtu")]
        public required int LinkMtu { get; init; }
        [Required]
        [Name("tun-mtu")]
        public required int TunMtu { get; init; }
        [Converter(typeof(VersionOptionConverter))]
        [Required]
        [Name("V4")]
        public required int Version { get; init; }
        [Required]
        [Name("keysize")]
        public required int KeySize { get; init; }
        [Required]
        [Name("tls-server")]
        public required bool TlsServer { get; init; }
        [Required]
        [Name("key-method")]
        public required int KeyMethod { get; init; }
    }
}
