using OpenVpn.IO;

namespace OpenVpn.Data.Packets
{
    // Ping identifier is constant (https://github.com/OpenVPN/openvpn/blob/7b1b283478ec008fad163c8a54659a1ed97ed727/src/openvpn/ping.c#L42)
    [DataPacket([
        0x2a, 0x18, 0x7b, 0xf3, 0x64, 0x1e, 0xb4, 0xcb,
        0x07, 0xed, 0x2d, 0x0a, 0x98, 0x1f, 0xc7, 0x48
    ])]
    internal sealed class PingPacket : IDataPacket
    {
        public void Serialize(PacketWriter writer)
        {
        }

        public void Deserialize(PacketReader reader)
        {
        }
    }
}
