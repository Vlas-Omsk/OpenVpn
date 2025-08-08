using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace OpenVpn
{
    internal sealed class PacketsQueue<T>
    {
        private readonly int _maximumSize;
        private readonly ILogger<PacketsQueue<T>> _logger;
        private readonly Dictionary<long, bool> _ids = new();
        private readonly PriorityQueue<T, long> _packets = new();
        private long _lastId = 0;

        public PacketsQueue(int maximumSize, ILogger<PacketsQueue<T>> logger)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumSize, 0, nameof(maximumSize));

            _maximumSize = maximumSize;
            _logger = logger;
        }

        public IEnumerable<uint> GetReceivedIds()
        {
            for (var i = _lastId - _maximumSize + 1; i <= _lastId; i++)
            {
                if (i < 0)
                    continue;

                if (_ids.TryGetValue(i, out _))
                {
                    yield return (uint)i;
                    continue;
                }
                else
                {
                    throw new Exception("Packet already dequed");
                }
            }
        }

        public bool TryEnqueue(uint id, T packet)
        {
            if (_ids.TryGetValue(id, out _))
            {
                _logger.LogWarning($"Packet {id} duplicated. Dropping");
                return false;
            }

            if (_packets.Count >= _maximumSize)
            {
                _logger.LogWarning($"Overcapacity when pushing packet {id}. Dropping");
                return false;
            }

            if (id < _lastId)
            {
                _logger.LogWarning($"Packet {id} too old. Dropping");
                return false;
            }

            _packets.Enqueue(packet, id);
            _ids.Add(id, true);

            return true;
        }

        public bool TryDequeue([NotNullWhen(true)] out T? packet)
        {
            if (!_packets.TryDequeue(out packet, out var id))
                return false;

            if (packet == null)
                return false;

            _ids.Remove(id);
            _lastId = id;

            return true;
        }
    }
}
