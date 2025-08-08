namespace OpenVpn.Sessions.Packets
{
    internal sealed class AckV1PacketHeader : ControlPacketHeader
    {
        public override byte Opcode { get; } = 0x05;
    }
}
