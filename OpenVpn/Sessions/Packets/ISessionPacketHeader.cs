using OpenVpn.IO;

namespace OpenVpn.Sessions.Packets
{
    internal interface ISessionPacketHeader
    {
        byte Opcode { get; }
        byte KeyId { get; }

        void Serialize(PacketWriter writer);
        bool TryDeserialize(PacketReader reader);
    }
}
