using OpenVpn.IO;
using OpenVpn.Options;

namespace OpenVpn.Control.Packets
{
    [ControlPacket("PUSH_REPLY,")]
    internal sealed class PushReplyPacket : IControlPacket
    {
        private const char _optionsSeparator = ',';
        private const char _optionsKeyValueSeparator = ' ';
        private IReadOnlyDictionary<string, string?> _options = null!;

        public IReadOnlyDictionary<string, string?> Options
        {
            get => _options;
            init => _options = value;
        }

        public void Serialize(OpenVpnMode mode, PacketWriter writer)
        {
            var options = OptionsParser.Stringify(Options, _optionsSeparator, _optionsKeyValueSeparator);

            writer.WriteString(options);
        }

        public bool TryDeserialize(OpenVpnMode mode, PacketReader reader, out int requiredSize)
        {
            var endPosition = reader.AvailableSpan.IndexOf((byte)0);

            if (endPosition == -1)
            {
                requiredSize = reader.Available + Buffers.Buffer.DefaultSize;
                return false;
            }

            using var optionsReader = reader.ReadString(endPosition + 1);

            _options = OptionsParser.Parse(
                optionsReader,
                _optionsSeparator,
                _optionsKeyValueSeparator
            );

            requiredSize = 0;
            return true;
        }
    }
}
