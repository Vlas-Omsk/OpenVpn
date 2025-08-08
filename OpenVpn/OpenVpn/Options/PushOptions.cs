using System.ComponentModel.DataAnnotations;
using OpenVpn.Options.Converters;

namespace OpenVpn.Options
{
    internal sealed class PushOptions
    {
        [Required]
        [Name("route-nopull")]
        public required bool RouteNoPull { get; init; }
        [Name("route-gateway")]
        public required string? RouteGateway { get; init; }
        [Required]
        [Name("cipher")]
        public required string Cipher { get; init; }
        [Required]
        [Name("tun-mtu")]
        public required int TunMtu { get; init; }
        [Name("ifconfig-ipv6")]
        public required string? IfConfigIpv6 { get; init; }
        [Required]
        [Name("ping")]
        public required int Ping { get; init; }
        [Name("tun-ipv6")]
        public required bool TunIpv6 { get; init; }
        [Converter(typeof(SplitOptionConverter), ' ')]
        [Name("protocol-flags")]
        public required string[]? ProtocolFlags { get; init; }
        [Converter(typeof(SplitOptionConverter), ' ')]
        [Name("redirect-gateway")]
        public required string[]? RedirectGateway { get; init; }
        [Name("peer-id")]
        public required uint? PeerId { get; init; }
        [Required]
        [Name("ping-restart")]
        public required int PingRestart { get; init; }
        [Required]
        [Name("topology")]
        public required OpenVpnTopology Topology { get; init; }
        [Name("ifconfig")]
        public required string? IfConfig { get; init; }
    }
}
