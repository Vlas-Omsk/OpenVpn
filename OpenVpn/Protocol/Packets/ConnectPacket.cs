namespace OpenVpn.Protocol.Packets
{
    public sealed class ConnectPacket : IOpenVpnProtocolPacket
    {
        public required OpenVpnDeviceType DeviceType { get; init; }
        public required InterfaceConfig? InterfaceConfigIpv4 { get; init; }
        public required InterfaceConfig? InterfaceConfigIpv6 { get; init; }
    }
}
