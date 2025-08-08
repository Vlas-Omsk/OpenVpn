using OpenVpn.IO;

namespace OpenVpn.Sessions.Packets
{
    internal abstract class ControlPacketHeader : SessionPacketHeader
    {
        private ulong _sessionId;

        public required ulong SessionId
        {
            get => _sessionId;
            init => _sessionId = value;
        }

        public override void Serialize(PacketWriter writer)
        {
            base.Serialize(writer);

            writer.WriteULong(SessionId);
        }

        public override bool TryDeserialize(PacketReader reader)
        {
            if (!base.TryDeserialize(reader))
                return false;

            _sessionId = reader.ReadULong();

            return true;
        }
    }
}
