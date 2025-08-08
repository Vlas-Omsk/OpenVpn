using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using CommunityToolkit.HighPerformance;
using OpenVpn.Buffers;
using OpenVpn.IO;
using OpenVpn.Sessions.Packets;
using PinkSystem;
using PinkSystem.Runtime;

namespace OpenVpn.Sessions
{
    internal sealed class SessionChannel : ISessionChannel
    {
        private static readonly ImmutableDictionary<byte, Type> _packetHeaderOpcodeTypeMap;
        private readonly Stream _stream;
        private readonly byte[] _sendBuffer = new byte[Buffers.Buffer.DefaultSize];
        private readonly Pipe _sendPipe = new();
        private readonly MemoryStream _sendStreamBuffer = new();
        private readonly byte[] _receiveBuffer = new byte[Buffers.Buffer.DefaultSize];
        private readonly Pipe _receivePipe = new();

        public SessionChannel(Stream stream)
        {
            _stream = stream;
        }

        static SessionChannel()
        {
            _packetHeaderOpcodeTypeMap = Assembly.GetExecutingAssembly().GetTypes()
                .Where(x => !x.IsAbstract && x.IsAssignableTo(typeof(ISessionPacketHeader)))
                .Select(x => (Opcode: x.GetSessionPacketOpcode(), Type: x))
                .ToImmutableDictionary(x => x.Opcode, x => x.Type);
        }

        public void Write(SessionPacket packet)
        {
            try
            {
                _sendStreamBuffer.Position = 2;

                var packetWriter = new PacketWriter(_sendStreamBuffer);

                packet.Header.Serialize(packetWriter);

                _sendStreamBuffer.Write(packet.Data.Span);

                _sendStreamBuffer.Position = 0;

                packetWriter.WriteLong(_sendStreamBuffer.Length - 2, bytesAmount: 2);

                _sendPipe.Write(
                    _sendStreamBuffer.ToReadOnlySpan()
                );
            }
            finally
            {
                _sendStreamBuffer.SetLength(0);
            }
        }

        public SessionPacket? Read()
        {
            var packetLength = EnsurePacketBytesReceived();

            if (packetLength == 0)
                return null;

            var packetReader = new PacketReader(
                _receivePipe.AvailableMemory.Slice(0, packetLength)
            );

            try
            {
                (var opcode, _) = SessionPacketHeader.SplitOpcodeKeyId(
                    packetReader.ReadByte()
                );

                packetReader.Position = 0;

                if (!_packetHeaderOpcodeTypeMap.TryGetValue(opcode, out var packetHeaderType))
                    throw new OpenVpnProtocolException($"Received unknown packet {opcode}");

                var packetHeader = (ISessionPacketHeader)ObjectAccessor.Create(packetHeaderType).Instance!;

                if (!packetHeader.TryDeserialize(packetReader))
                    throw new OpenVpnProtocolException("Packet wrongly mapped to opcode");

                return new()
                {
                    Header = packetHeader,
                    Data = packetReader.AvailableMemory
                };
            }
            finally
            {
                _receivePipe.Consume(packetLength);
            }
        }

        private int EnsurePacketBytesReceived()
        {
            var packetLengthSlice = _receiveBuffer.AsMemory(0, 2);

            var readedLength = _receivePipe.Peek(packetLengthSlice.Span);

            if (readedLength < 2)
                return 0;

            var packetLength = BinaryPrimitives.ReadUInt16BigEndian(packetLengthSlice.Span);

            if (_receivePipe.Available < packetLength)
                return 0;

            _receivePipe.Consume(2);

            return packetLength;
        }

        public async Task Send(CancellationToken cancellationToken)
        {
            var readedLength = 0;

            do
            {
                readedLength = _sendPipe.Read(
                    _sendBuffer
                );

                if (readedLength > 0)
                {
                    await _stream.WriteAsync(
                        _sendBuffer.AsMemory(0, readedLength),
                        cancellationToken
                    );
                }
            }
            while (readedLength > 0);
        }

        public async Task Receive(CancellationToken cancellationToken)
        {
            var readedLength = 0;

            do
            {
                readedLength = await _stream.ReadAsync(
                    _receiveBuffer,
                    cancellationToken
                );

                if (readedLength > 0)
                {
                    _receivePipe.Write(
                        _receiveBuffer.AsSpan(0, readedLength)
                    );
                }
            }
            while (readedLength > 0);
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
