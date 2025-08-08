using PacketDotNet;

namespace OpenVpn.Protocol.Packets
{
    public sealed class EthernetDataPacket : IOpenVpnProtocolPacket
    {
        public required EthernetPacket Packet { get; init; }
    }
}
