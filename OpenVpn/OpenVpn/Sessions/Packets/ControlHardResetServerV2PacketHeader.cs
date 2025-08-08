namespace OpenVpn.Sessions.Packets
{
    internal sealed class ControlHardResetServerV2PacketHeader : ControlPacketHeader
    {
        public override byte Opcode { get; } = 0x08;
    }
}
