using OpenVpn.Crypto;
using OpenVpn.IO;
using OpenVpn.Options;

namespace OpenVpn.Control.Packets
{
    [ControlPacket([0x00, 0x00, 0x00, 0x00, 0x02])]
    internal sealed class KeyExchangeMethod2Packet : IControlPacket
    {
        private const char _optionsSeparator = ',';
        private const char _optionsKeyValueSeparator = ' ';
        private const char _peerInfoSeparator = '\n';
        private const char _peerInfoKeyValueSeparator = '=';
        private CryptoKeySource _keySource;
        private IReadOnlyDictionary<string, string?> _options = null!;
        private string _username = null!;
        private string _password = null!;
        private IReadOnlyDictionary<string, string?> _peerInfo = null!;

        public required CryptoKeySource KeySource
        {
            get => _keySource;
            init => _keySource = value;
        }

        public required IReadOnlyDictionary<string, string?> Options
        {
            get => _options;
            init => _options = value;
        }

        public required string Username
        {
            get => _username;
            init => _username = value;
        }

        public required string Password
        {
            get => _password;
            init => _password = value;
        }

        public required IReadOnlyDictionary<string, string?> PeerInfo
        {
            get => _peerInfo;
            init => _peerInfo = value;
        }

        public void Serialize(OpenVpnMode mode, PacketWriter writer)
        {
            if (mode == OpenVpnMode.Client)
            {
                if (KeySource.PreMaster.Length == 0)
                    throw new InvalidOperationException("Pre master should be initialized for client mode");

                writer.WriteBytes(KeySource.PreMaster.Span);
            }

            writer.WriteBytes(KeySource.Random1.Span);
            writer.WriteBytes(KeySource.Random2.Span);

            var options = OptionsParser.Stringify(Options, _optionsSeparator, _optionsKeyValueSeparator);

            writer.WriteInt(writer.GetStringSize(options), bytesAmount: 2);
            writer.WriteString(options);

            var username = Username ?? string.Empty;

            writer.WriteInt(writer.GetStringSize(username), bytesAmount: 2);
            writer.WriteString(username);

            var password = Password ?? string.Empty;

            writer.WriteInt(writer.GetStringSize(password), bytesAmount: 2);
            writer.WriteString(password);

            var peerInfo = OptionsParser.Stringify(PeerInfo, _peerInfoSeparator, _peerInfoKeyValueSeparator);

            writer.WriteInt(writer.GetStringSize(peerInfo), bytesAmount: 2);
            writer.WriteString(peerInfo);
        }

        public bool TryDeserialize(OpenVpnMode mode, PacketReader reader, out int requestedSize)
        {
            var keySize = mode switch
            {
                OpenVpnMode.Client => CryptoKeySource.PreMasterSize + CryptoKeySource.RandomSize + CryptoKeySource.RandomSize,
                OpenVpnMode.Server => CryptoKeySource.RandomSize + CryptoKeySource.RandomSize,
                _ => throw new NotSupportedException()
            };

            requestedSize = keySize + /* options length bytes */ 2;

            if (reader.Available < requestedSize)
                return false;

            var key = reader.ReadMemory(keySize);

            _keySource = new(key);

            var optionsLength = reader.ReadInt(bytesAmount: 2);
            requestedSize += optionsLength + /* username length bytes */ 2;

            if (reader.Available < optionsLength)
                return false;

            using var optionsReader = reader.ReadString(optionsLength);

            _options = OptionsParser.Parse(
                optionsReader,
                _optionsSeparator,
                _optionsKeyValueSeparator
            );

            if (reader.Available < /* username length bytes */ 2)
                return false;

            var usernameLength = reader.ReadInt(bytesAmount: 2);
            requestedSize += usernameLength + /* password length bytes */ 2;

            if (reader.Available < usernameLength)
                return false;

            using var usernameReader = reader.ReadString(usernameLength);

            _username = usernameReader.ReadToEnd();

            if (reader.Available < /* password length bytes */ 2)
                return false;

            var passwordLength = reader.ReadInt(bytesAmount: 2);
            requestedSize += passwordLength + /* peerInfo length bytes */ 2;

            if (reader.Available < passwordLength)
                return false;

            using var passwordReader = reader.ReadString(passwordLength);

            _password = passwordReader.ReadToEnd();

            if (reader.Available < /* peerInfo length bytes */ 2)
                return false;

            var peerInfoLength = reader.ReadInt(bytesAmount: 2);
            requestedSize += peerInfoLength;

            if (reader.Available < peerInfoLength)
                return false;

            using var peerInfoReader = reader.ReadString(peerInfoLength);

            _peerInfo = OptionsParser.Parse(
                peerInfoReader,
                _peerInfoSeparator,
                _peerInfoKeyValueSeparator
            );

            return true;
        }
    }
}
