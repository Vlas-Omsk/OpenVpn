using OpenVpn.Sessions.Packets;

namespace OpenVpn.Sessions
{
    internal sealed class SessionChannelDemuxer : IDisposable
    {
        private readonly ISessionChannel _channel;
        private readonly Dictionary<byte, Channel> _demuxingChannels = new();

        private sealed class Channel : ISessionChannel
        {
            private readonly ISessionChannel _channel;
            private readonly Dictionary<byte, Channel> _demuxingChannels;
            private readonly HashSet<byte> _acceptingOpcodes;
            private readonly Queue<SessionPacket> _queuedPackets = new();

            public Channel(
                ISessionChannel channel,
                Dictionary<byte, Channel> demuxingChannels,
                HashSet<byte> acceptingOpcodes
            )
            {
                _channel = channel;
                _demuxingChannels = demuxingChannels;
                _acceptingOpcodes = acceptingOpcodes;
            }

            public void Write(SessionPacket packet)
            {
                _channel.Write(packet);
            }

            public SessionPacket? Read()
            {
                if (_queuedPackets.TryDequeue(out var dequeuedPacket))
                    return dequeuedPacket;

                var packet = _channel.Read();

                if (packet == null)
                    return null;

                if (_acceptingOpcodes.Contains(packet.Header.Opcode))
                    return packet;

                if (_demuxingChannels.TryGetValue(packet.Header.Opcode, out var channel))
                    channel.Enqueue(packet);

                return null;
            }

            public async Task Send(CancellationToken cancellationToken)
            {
                await _channel.Send(cancellationToken);
            }

            public async Task Receive(CancellationToken cancellationToken)
            {
                await _channel.Receive(cancellationToken);
            }

            private void Enqueue(SessionPacket packet)
            {
                // Packet can be overwritten so cloning it
                var packetClone = packet.Clone();

                _queuedPackets.Enqueue(packetClone);
            }

            public void Dispose()
            {
            }
        }

        public SessionChannelDemuxer(ISessionChannel channel)
        {
            _channel = channel;
        }

        public ISessionChannel RegisterFor(IEnumerable<byte> opcodes)
        {
            var opcodesSet = opcodes.ToHashSet();

            foreach (var opcode in opcodesSet)
                if (_demuxingChannels.ContainsKey(opcode))
                    throw new ArgumentException($"Channel for opcode {opcode} already registered");

            var channel = new Channel(
                _channel,
                _demuxingChannels,
                opcodesSet
            );

            foreach (var opcode in opcodesSet)
                _demuxingChannels.Add(opcode, channel);

            return channel;
        }

        public void Dispose()
        {
            _channel.Dispose();
        }
    }
}
