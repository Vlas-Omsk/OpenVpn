using System.Buffers;
using System.Collections.Immutable;
using System.Reflection;
using CommunityToolkit.HighPerformance;
using MoreLinq;
using OpenVpn.Crypto;
using OpenVpn.Data.Packets;
using OpenVpn.IO;
using OpenVpn.Sessions;
using OpenVpn.Sessions.Packets;
using PinkSystem;
using PinkSystem.Runtime;

namespace OpenVpn.Data
{
    // TODO: Add mtu controlling
    internal sealed class DataChannel : IDataChannel
    {
        private static readonly ImmutableDictionary<byte[], Type> _packetIdentifiertTypeMap;
        private static readonly Type? _emptyIdentifierPacketType;
        private const byte _keyId = 0;
        private readonly uint _peerId;
        private readonly ICrypto _crypto;
        private readonly ISessionChannel _dataChannel;
        private readonly MemoryStream _sendPacketStreamBuffer = new();
        private readonly MemoryStream _sendPacketHeaderStreamBuffer = new();
        private readonly MemoryStream _receivePacketHeaderStreamBuffer = new();

        public DataChannel(
            uint peerId,
            ICrypto crypto,
            ISessionChannel dataChannel
        )
        {
            _peerId = peerId;
            _crypto = crypto;
            _dataChannel = dataChannel;
        }

        static DataChannel()
        {
            var packetIdentifiertTypeEnumerable = Assembly.GetExecutingAssembly().GetTypes()
                .Where(x => x.IsAssignableTo(typeof(IDataPacket)))
                .Select(x => (Attribute: x.GetCustomAttribute<DataPacketAttribute>()!, Type: x))
                .Where(x => x.Attribute != null);

            foreach (var (outerPacketAttribute, outerPacketType) in packetIdentifiertTypeEnumerable)
            {
                foreach (var (innerPacketAttribute, innerPacketType) in packetIdentifiertTypeEnumerable)
                {
                    if (outerPacketType == innerPacketType)
                        continue;

                    if (outerPacketAttribute.Identifier.SequenceEqual(innerPacketAttribute.Identifier))
                        throw new Exception("Key duplicated exception");
                }
            }

            _packetIdentifiertTypeMap = packetIdentifiertTypeEnumerable
                .ToImmutableDictionary(x => x.Attribute!.Identifier, x => x.Type);
            _emptyIdentifierPacketType = packetIdentifiertTypeEnumerable
                .Where(x => x.Attribute!.Identifier.Length == 0)
                .Select(x => x.Type)
                .FirstOrDefault();
        }

        public void Write(IDataPacket packet)
        {
            try
            {
                _sendPacketStreamBuffer.Write(packet.GetIdentifier());

                var packetWriter = new PacketWriter(_sendPacketStreamBuffer);

                packet.Serialize(packetWriter);

                WriteEncrypted(_sendPacketStreamBuffer.ToReadOnlySpan());
            }
            finally
            {
                _sendPacketStreamBuffer.SetLength(0);
            }
        }

        private void WriteEncrypted(ReadOnlySpan<byte> input)
        {
            var header = new DataV2PacketHeader()
            {
                KeyId = _keyId,
                PeerId = _peerId,
            };

            var data = ArrayPool<byte>.Shared.Rent(
                _crypto.GetEncryptedSize(input.Length)
            );

            try
            {
                var dataLength = Encrypt(header, input, data);

                _dataChannel.Write(new()
                {
                    Header = header,
                    Data = data.AsMemory(0, dataLength)
                });
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(data);
            }
        }

        private int Encrypt(ISessionPacketHeader header, ReadOnlySpan<byte> input, Span<byte> output)
        {
            try
            {
                var headerWriter = new PacketWriter(_sendPacketHeaderStreamBuffer);

                header.Serialize(headerWriter);

                return _crypto.Encrypt(_sendPacketHeaderStreamBuffer.ToReadOnlySpan(), input, output);
            }
            finally
            {
                _sendPacketHeaderStreamBuffer.SetLength(0);
            }
        }

        public IDataPacket? Read()
        {
            var packet = _dataChannel.Read();

            if (packet == null)
                return null;

            if (packet.Header is DataV2PacketHeader)
            {
                var data = ArrayPool<byte>.Shared.Rent(
                    _crypto.GetDecryptedSize(packet.Data.Length)
                );

                try
                {
                    var dataLength = Decrypt(packet.Header, packet.Data.Span, data);

                    return Deserialize(data.AsMemory(0, dataLength));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(data);
                }
            }
            else
            {
                throw new OpenVpnUnexpectedPacketTypeException(packet.GetType());
            }
        }

        private int Decrypt(ISessionPacketHeader header, ReadOnlySpan<byte> input, Span<byte> output)
        {
            try
            {
                var headerWriter = new PacketWriter(_receivePacketHeaderStreamBuffer);

                header.Serialize(headerWriter);

                return _crypto.Decrypt(_receivePacketHeaderStreamBuffer.ToReadOnlySpan(), input, output);
            }
            finally
            {
                _receivePacketHeaderStreamBuffer.SetLength(0);
            }
        }

        private static IDataPacket Deserialize(ReadOnlyMemory<byte> data)
        {
            var packetType = IdentifyPacketType(
                data.Span,
                out var packetIdentifierLength
            );

            var packetReader = new PacketReader(
                data.Slice(packetIdentifierLength)
            );

            var packet = (IDataPacket)ObjectAccessor.Create(packetType).Instance!;

            packet.Deserialize(packetReader);

            return packet;
        }

        private static Type IdentifyPacketType(ReadOnlySpan<byte> bytes, out int identifierLength)
        {
            Type? packetType = null;
            byte[]? packetIdentifier = null;

            foreach (var (currentIdentifier, currentPacketType) in _packetIdentifiertTypeMap)
            {
                if (currentIdentifier.Length == 0)
                    continue;

                if (!currentIdentifier
                        .AsSpan()
                        .SequenceEqual(bytes.Slice(0, currentIdentifier.Length)))
                    continue;

                packetType = currentPacketType;
                packetIdentifier = currentIdentifier;
            }

            if (packetType == null)
            {
                if (_emptyIdentifierPacketType == null)
                    throw new OpenVpnProtocolException($"Received unknown packet identifier");

                packetType = _emptyIdentifierPacketType;
                packetIdentifier = [];
            }

            identifierLength = packetIdentifier!.Length;

            return packetType;
        }

        public Task Send(CancellationToken cancellationToken)
        {
            return _dataChannel.Send(cancellationToken);
        }

        public Task Receive(CancellationToken cancellationToken)
        {
            return _dataChannel.Receive(cancellationToken);
        }
    }
}
