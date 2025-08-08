using PacketDotNet;

namespace OpenVpn.Protocol.Packets
{
    public sealed class IpDataPacket : IOpenVpnProtocolPacket
    {
        public required IPPacket Packet { get; init; }
    }
}
