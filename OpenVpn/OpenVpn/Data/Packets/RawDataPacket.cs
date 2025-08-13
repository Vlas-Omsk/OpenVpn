using OpenVpn.IO;

namespace OpenVpn.Data.Packets
{
    [DataPacket(new byte[0])]
    internal sealed class RawDataPacket : IDataPacket
    {
        private ReadOnlyMemory<byte> _data = null!;

        public ReadOnlyMemory<byte> Data
        {
            get => _data;
            init => _data = value;
        }

        public void Serialize(PacketWriter writer)
        {
            writer.WriteBytes(_data.Span);
        }

        public void Deserialize(PacketReader reader)
        {
            _data = reader.AvailableMemory;
        }
    }
}
