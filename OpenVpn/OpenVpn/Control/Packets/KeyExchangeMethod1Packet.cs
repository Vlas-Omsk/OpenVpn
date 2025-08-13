using OpenVpn.IO;

namespace OpenVpn.Control.Packets
{
    [ControlPacket(new byte[0])]
    internal sealed class KeyExchangeMethod1Packet : IControlPacket
    {
        public void Serialize(OpenVpnMode mode, PacketWriter writer)
        {
            throw new NotImplementedException();
        }

        public bool TryDeserialize(OpenVpnMode mode, PacketReader reader, out int requiredSize)
        {
            throw new NotImplementedException();
        }
    }
}
