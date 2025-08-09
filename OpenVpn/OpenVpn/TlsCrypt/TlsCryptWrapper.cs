using System.Buffers;
using CommunityToolkit.HighPerformance;
using Microsoft.Extensions.Logging;
using OpenVpn.Crypto;
using OpenVpn.IO;
using OpenVpn.Queues;
using OpenVpn.Sessions;
using OpenVpn.Sessions.Packets;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Security;
using PinkSystem;

namespace OpenVpn.TlsCrypt
{
    internal sealed class TlsCryptWrapper : ISessionChannel
    {
        private const int _packetIdSize = sizeof(uint);
        private const int _timeSize = sizeof(uint);
        private const int _headerSize = _packetIdSize + _timeSize;
        private readonly PacketsQueue<TlsCryptPacket> _packetsQueue;
        private readonly ISessionChannel _channel;
        private readonly CtrCrypto _crypto;
        private readonly MemoryStream _sendStreamBuffer = new();
        private byte[] _receiveBuffer = new byte[Buffers.Buffer.DefaultSize];
        private uint _packetId = 1;

        private sealed class TlsCryptPacket
        {
            public required ISessionPacketHeader SessionHeader { get; init; }
            public required uint PacketId { get; init; }
            public required uint Time { get; init; }
            public required ReadOnlyMemory<byte> HeaderAndData { get; init; }
        }

        public TlsCryptWrapper(
            int maximumQueueSize,
            ISessionChannel channel,
            CryptoKeys keys,
            OpenVpnMode mode,
            SecureRandom random,
            ILoggerFactory loggerFactory
        )
        {
            _packetsQueue = new(
                maximumQueueSize,
                loggerFactory.CreateLogger<PacketsQueue<TlsCryptPacket>>()
            );
            _channel = channel;
            _crypto = new CtrCrypto(
                keys,
                () => new AesEngine(),
                keySize: 32,
                ivSize: 16,
                () => new HMac(new Sha256Digest()),
                mode,
                random
            );
        }

        public void Write(SessionPacket packet)
        {
            var packetId = _packetId++;

            var output = ArrayPool<byte>.Shared.Rent(
                _headerSize +
                _crypto.GetEncryptedSize(packet.Data.Length)
            );

            var header = output.AsMemory(0, _headerSize);
            var encrypted = output.AsSpan(_headerSize);

            try
            {
                using (var packetWriter = new PacketWriter(new MemoryStream(output)))
                {
                    packetWriter.WriteUInt(packetId);
                    packetWriter.WriteUInt((uint)UnixTimestamp.Now.SecondsLong);
                }

                var encryptedLength = Wrap(packet.Header, header.Span, packet.Data.Span, encrypted);

                _channel.Write(new()
                {
                    Header = packet.Header,
                    Data = output.AsMemory(0, _headerSize + encryptedLength)
                });
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(output);
            }
        }

        private int Wrap(ISessionPacketHeader sessionHeader, ReadOnlySpan<byte> header, ReadOnlySpan<byte> input, Span<byte> output)
        {
            try
            {
                var headerWriter = new PacketWriter(_sendStreamBuffer);

                sessionHeader.Serialize(headerWriter);

                headerWriter.WriteBytes(header);

                return _crypto.Encrypt(_sendStreamBuffer.ToReadOnlySpan(), input, output);
            }
            finally
            {
                _sendStreamBuffer.SetLength(0);
            }
        }

        public SessionPacket? Read()
        {
            var packet = ReadPacket();

            if (packet == null)
                return null;

            var header = packet.HeaderAndData.Span.Slice(0, _headerSize);
            var data = packet.HeaderAndData.Span.Slice(_headerSize);

            var decryptedLength = _crypto.GetDecryptedSize(
                data.Length
            );

            if (_receiveBuffer.Length < decryptedLength)
                _receiveBuffer = new byte[decryptedLength * 2];

            decryptedLength = Unwrap(packet.SessionHeader, header, data, _receiveBuffer);

            return new()
            {
                Header = packet.SessionHeader,
                Data = _receiveBuffer.AsMemory(0, decryptedLength)
            };
        }

        private TlsCryptPacket? ReadPacket()
        {
            var packet = _channel.Read();

            if (packet == null)
                return null;

            var packetReader = new PacketReader(packet.Data);

            var packetId = packetReader.ReadUInt();
            var time = packetReader.ReadUInt();

            var tlsCryptPacket = new TlsCryptPacket()
            {
                PacketId = packetId,
                Time = time,
                // Packet can be overwritten after dequeuing so cloning needed data
                HeaderAndData = packet.Data.ToArray(),
                SessionHeader = packet.Header,
            };

            _packetsQueue.TryEnqueue(packetId, tlsCryptPacket);

            if (!_packetsQueue.TryDequeue(out var tlsCryptDequeuedPacket))
                return null;

            return tlsCryptDequeuedPacket;
        }

        private int Unwrap(ISessionPacketHeader sessionHeader, ReadOnlySpan<byte> header, ReadOnlySpan<byte> input, Span<byte> output)
        {
            try
            {
                var headerWriter = new PacketWriter(_sendStreamBuffer);

                sessionHeader.Serialize(headerWriter);

                headerWriter.WriteBytes(header);

                return _crypto.Decrypt(_sendStreamBuffer.ToReadOnlySpan(), input, output);
            }
            finally
            {
                _sendStreamBuffer.SetLength(0);
            }
        }

        public Task Send(CancellationToken cancellationToken)
        {
            return _channel.Send(cancellationToken);
        }

        public Task Receive(CancellationToken cancellationToken)
        {
            return _channel.Receive(cancellationToken);
        }

        public void Dispose()
        {
            _channel.Dispose();
        }
    }
}
