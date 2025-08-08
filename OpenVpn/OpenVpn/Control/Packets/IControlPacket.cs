using OpenVpn.IO;

namespace OpenVpn.Control.Packets
{
    internal interface IControlPacket
    {
        void Serialize(OpenVpnMode mode, PacketWriter writer);
        bool TryDeserialize(OpenVpnMode mode, PacketReader reader, out int requestedSize);
    }
}
