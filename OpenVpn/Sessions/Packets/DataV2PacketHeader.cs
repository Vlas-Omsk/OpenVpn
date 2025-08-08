using OpenVpn.IO;

namespace OpenVpn.Sessions.Packets
{
    internal sealed class DataV2PacketHeader : SessionPacketHeader
    {
        private uint _peerId;

        public override byte Opcode { get; } = 0x09;
        public required uint PeerId
        {
            get => _peerId;
            init => _peerId = value;
        }

        public override void Serialize(PacketWriter writer)
        {
            base.Serialize(writer);

            writer.WriteUInt(PeerId, bytesAmount: 3);
        }

        public override bool TryDeserialize(PacketReader reader)
        {
            if (!base.TryDeserialize(reader))
                return false;

            _peerId = reader.ReadUInt(bytesAmount: 3);

            return true;
        }
    }
}
