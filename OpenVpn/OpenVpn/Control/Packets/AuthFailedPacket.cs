using OpenVpn.IO;
using OpenVpn.Options;

namespace OpenVpn.Control.Packets
{
    [ControlPacket("AUTH_FAILED,")]
    internal sealed class AuthFailedPacket : IControlPacket
    {
        private string _reason = null!;

        public required string Reason
        {
            get => _reason;
            init => _reason = value;
        }

        public void Serialize(OpenVpnMode mode, PacketWriter writer)
        {
            writer.WriteString(Reason);
        }

        public bool TryDeserialize(OpenVpnMode mode, PacketReader reader, out int requiredSize)
        {
            var endPosition = reader.AvailableSpan.IndexOf((byte)0);

            if (endPosition == -1)
            {
                requiredSize = reader.Available + Buffers.Buffer.DefaultSize;
                return false;
            }

            using var reasonReader = reader.ReadString(endPosition + 1);

            _reason = reasonReader.ReadToEnd();

            requiredSize = 0;
            return true;
        }
    }
}
