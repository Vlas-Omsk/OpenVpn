namespace OpenVpn.Sessions.Packets
{
    internal sealed class ControlHardResetClientV2PacketHeader : ControlPacketHeader
    {
        public override byte Opcode { get; } = 0x07;
    }
}
