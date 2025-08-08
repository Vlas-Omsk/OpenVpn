using OpenVpn.IO;

namespace OpenVpn.Data.Packets
{
    internal interface IDataPacket
    {
        void Serialize(PacketWriter writer);
        void Deserialize(PacketReader reader);
    }
}
