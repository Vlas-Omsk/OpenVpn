using System.Buffers;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenVpn.Buffers;
using OpenVpn.Control.Crypto;
using OpenVpn.Control.Packets;
using OpenVpn.IO;
using OpenVpn.Queues;
using OpenVpn.Sessions;
using OpenVpn.Sessions.Packets;
using PinkSystem;
using PinkSystem.Runtime;

namespace OpenVpn.Control
{
    internal sealed class ControlChannel : IControlChannel
    {
        private static readonly ImmutableDictionary<byte[], Type> _packetIdentifiertTypeMap;
        private static readonly Type? _emptyIdentifierPacketType;
        private static readonly int _maxIdentifierLength;
        private const byte _keyId = 0;
        private readonly StrictOrderPacketsQueue<ControlPacket> _packetsQueue;
        private readonly ISessionChannel _controlChannel;
        private readonly OpenVpnMode _sendMode;
        private readonly OpenVpnMode _receiveMode;
        private readonly IControlCrypto _crypto;
        private readonly ILogger<SessionChannel> _logger;
        private readonly byte[] _sendBuffer = new byte[Buffers.Buffer.DefaultSize];
        private readonly MemoryStream _sendStreamBuffer = new();
        private readonly MemoryStream _sendPacketStreamBuffer = new();
        private readonly byte[] _receiveBuffer = new byte[Buffers.Buffer.DefaultSize];
        private readonly Pipe _receivePipe = new();
        private readonly ulong _sessionId = unchecked((ulong)Random.Shared.NextInt64());
        private ulong _remoteSessionId = 0;
        private uint _packetId = 0;
        private int _requestedPacketSize = _maxIdentifierLength;

        private sealed class ControlPacket
        {
            public required ISessionPacketHeader SessionHeader { get; init; }
            public required uint[] PacketIdArray { get; init; }
            public required ulong RemoteSessionId { get; init; }
            public required uint PacketId { get; init; }
            public required ReadOnlyMemory<byte> Data { get; init; }
        }

        public ControlChannel(
            int maximumQueueSize,
            ISessionChannel controlChannel,
            OpenVpnMode mode,
            IControlCrypto crypto,
            ILoggerFactory loggerFactory
        )
        {
            _packetsQueue = new(
                maximumQueueSize,
                firstId: 0,
                loggerFactory.CreateLogger<StrictOrderPacketsQueue<ControlPacket>>()
            );
            _controlChannel = controlChannel;
            _sendMode = mode;
            _receiveMode = mode switch
            {
                OpenVpnMode.Client => OpenVpnMode.Server,
                OpenVpnMode.Server => OpenVpnMode.Client,
                _ => throw new NotSupportedException()
            };
            _crypto = crypto;
            _logger = loggerFactory.CreateLogger<SessionChannel>();
        }

        static ControlChannel()
        {
            var packetIdentifiertTypeEnumerable = Assembly.GetExecutingAssembly().GetTypes()
                .Where(x => x.IsAssignableTo(typeof(IControlPacket)))
                .Select(x => (Attribute: x.GetCustomAttribute<ControlPacketAttribute>()!, Type: x))
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
            _maxIdentifierLength = _packetIdentifiertTypeMap.Max(x => x.Key.Length);
        }

        public ulong SessionId => _sessionId;
        public ulong RemoteSessionId => _remoteSessionId;

        public void Connect()
        {
            if (_sendMode == OpenVpnMode.Client)
            {
                _crypto.Connect();

                WriteHardResetClient();
            }
        }

        public void Write(IControlPacket packet)
        {
            try
            {
                using var packetWriter = new PacketWriter(_sendStreamBuffer, leaveOpen: true);

                var packetIdentifier = packet.GetIdentifier();

                packetWriter.WriteBytes(packetIdentifier);

                packet.Serialize(_sendMode, packetWriter);

                _crypto.WriteInput(
                    _sendStreamBuffer.ToReadOnlySpan()
                );
            }
            finally
            {
                _sendStreamBuffer.SetLength(0);
            }
        }

        public IControlPacket? Read()
        {
            if (_receivePipe.Available < _requestedPacketSize)
                return null;

            var packetReader = new PacketReader(
                _receivePipe.AvailableMemory
            );

            var packetType = IdentifyPacketType(packetReader);

            var packet = (IControlPacket)ObjectAccessor.Create(packetType).Instance!;

            if (!packet.TryDeserialize(_receiveMode, packetReader, out var requestedSize))
            {
                _requestedPacketSize = _maxIdentifierLength + requestedSize;
                return null;
            }

            _receivePipe.Consume(packetReader.Position);

            _requestedPacketSize = _maxIdentifierLength;

            return packet;
        }

        private static Type IdentifyPacketType(PacketReader packetReader)
        {
            var firstBytes = packetReader.ReadSpan(_maxIdentifierLength);

            Type? packetType = null;
            byte[]? packetIdentifier = null;

            foreach (var (currentIdentifier, currentPacketType) in _packetIdentifiertTypeMap)
            {
                if (currentIdentifier.Length == 0)
                    continue;

                if (!currentIdentifier
                        .AsSpan()
                        .SequenceEqual(firstBytes.Slice(0, currentIdentifier.Length)))
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

            packetReader.Position = packetIdentifier!.Length;

            return packetType;
        }

        public Task Send(CancellationToken cancellationToken)
        {
            var readedLength = 0;

            do
            {
                readedLength = _crypto.ReadOutput(_sendBuffer);

                if (readedLength > 0)
                    WriteData(_sendBuffer.AsMemory(0, readedLength));
            }
            while (readedLength > 0);

            return _controlChannel.Send(cancellationToken);
        }

        private void WritePacket(ISessionPacketHeader header, ReadOnlyMemory<byte> data, bool withPacketId = true)
        {
            try
            {
                using var packetWriter = new PacketWriter(_sendPacketStreamBuffer, leaveOpen: true);

                var receivedPacketIds = _packetsQueue.GetReceivedIds().Reverse().ToArray();

                packetWriter.WriteInt(receivedPacketIds.Length, bytesAmount: 1);

                foreach (var item in receivedPacketIds)
                    packetWriter.WriteUInt(item);

                if (receivedPacketIds.Length > 0)
                {
                    if (_remoteSessionId == 0)
                        throw new OpenVpnProtocolException("Remote session id should be not 0");

                    packetWriter.WriteULong(_remoteSessionId);
                }

                if (withPacketId)
                {
                    var packetId = _packetId++;

                    packetWriter.WriteUInt(packetId);
                }

                packetWriter.WriteBytes(data.Span);

                _controlChannel.Write(new SessionPacket()
                {
                    Header = header,
                    Data = _sendPacketStreamBuffer.ToReadOnlyMemory(),
                });
            }
            finally
            {
                _sendPacketStreamBuffer.SetLength(0);
            }
        }

        private void WriteAck()
        {
            WritePacket(
                new AckV1PacketHeader()
                {
                    KeyId = _keyId,
                    SessionId = _sessionId,
                },
                ReadOnlyMemory<byte>.Empty,
                withPacketId: false
            );
        }

        private void WriteHardResetClient()
        {
            WritePacket(
                new ControlHardResetClientV2PacketHeader()
                {
                    KeyId = _keyId,
                    SessionId = _sessionId,
                },
                ReadOnlyMemory<byte>.Empty
            );
        }

        private void WriteHardResetServer()
        {
            WritePacket(
                new ControlHardResetServerV2PacketHeader()
                {
                    KeyId = _keyId,
                    SessionId = _sessionId,
                },
                ReadOnlyMemory<byte>.Empty
            );
        }

        private void WriteData(ReadOnlyMemory<byte> data)
        {
            WritePacket(
                new ControlV1PacketHeader()
                {
                    KeyId = _keyId,
                    SessionId = _sessionId,
                },
                data
            );
        }

        public async Task Receive(CancellationToken cancellationToken)
        {
            await _controlChannel.Receive(cancellationToken);

            while (true)
            {
                var packet = ReadPacket();

                if (packet == null)
                    return;

                HandlePacket(packet);
            }
        }

        private ControlPacket? ReadPacket()
        {
            var packet = _controlChannel.Read();

            if (packet == null)
                return null;

            if (packet.Header is not ControlPacketHeader controlPacketHeader)
                throw new OpenVpnUnexpectedPacketTypeException(packet.GetType());

            if (_remoteSessionId != 0 &&
                controlPacketHeader.SessionId != _remoteSessionId)
            {
                _logger.LogWarning($"Received packet from different remote session id. Current remote session id: {_remoteSessionId}, Received remote session id: {controlPacketHeader.SessionId}. Dropping");
                return null;
            }

            var packetReader = new PacketReader(packet.Data);

            var packetIdArrayLength = packetReader.ReadByte();
            var packetIdArray = new uint[packetIdArrayLength];

            for (var i = 0; i < packetIdArrayLength; i++)
                packetIdArray[i] = packetReader.ReadUInt();

            var remoteSessionId = 0ul;

            if (packetIdArrayLength > 0)
            {
                // TODO: Check for dropped packets

                remoteSessionId = packetReader.ReadULong();

                if (remoteSessionId != _sessionId)
                {
                    _logger.LogWarning($"Received packet for different session id. Current session id: {_sessionId}, Received session id: {remoteSessionId}. Dropping");
                    return null;
                }
            }

            var packetId = packetReader.ReadUInt();

            var controlPacket = new ControlPacket()
            {
                PacketIdArray = packetIdArray,
                RemoteSessionId = remoteSessionId,
                PacketId = packetId,
                // Packet can be overwritten after dequeuing so cloning needed data
                Data = packetReader.AvailableMemory.ToArray(),
                SessionHeader = packet.Header
            };

            if (_packetsQueue.TryEnqueue(packetId, controlPacket))
            {
                if (packet.Header
                    is not ControlHardResetClientV2PacketHeader
                    and not ControlHardResetServerV2PacketHeader)
                    WriteAck();
            }

            if (!_packetsQueue.TryDequeue(out var dequeuedControlPacket))
            {
                return null;
            }

            return dequeuedControlPacket;
        }

        private void HandlePacket(ControlPacket packet)
        {
            if (packet.SessionHeader is ControlHardResetClientV2PacketHeader controlHardResetClientV2PacketHeader)
            {
                if (_sendMode == OpenVpnMode.Client)
                    throw new OpenVpnUnexpectedPacketTypeException(packet.SessionHeader.GetType());

                _remoteSessionId = controlHardResetClientV2PacketHeader.SessionId;

                WriteHardResetServer();
            }
            else if (packet.SessionHeader is ControlHardResetServerV2PacketHeader controlHardResetServerV2PacketHeader)
            {
                if (_sendMode == OpenVpnMode.Server)
                    throw new OpenVpnUnexpectedPacketTypeException(packet.SessionHeader.GetType());

                _remoteSessionId = controlHardResetServerV2PacketHeader.SessionId;

                WriteAck();
            }
            else if (packet.SessionHeader is AckV1PacketHeader)
            {
            }
            else if (packet.SessionHeader is ControlV1PacketHeader)
            {
                _crypto.WriteOutput(packet.Data.Span);

                var readedLength = 0;

                do
                {
                    readedLength = _crypto.ReadInput(_receiveBuffer);

                    if (readedLength > 0)
                        _receivePipe.Write(_receiveBuffer.AsSpan(0, readedLength));
                }
                while (readedLength > 0);
            }
            else
            {
                throw new OpenVpnUnexpectedPacketTypeException(packet.SessionHeader.GetType());
            }
        }

        public void Dispose()
        {
            _controlChannel.Dispose();
        }
    }
}
