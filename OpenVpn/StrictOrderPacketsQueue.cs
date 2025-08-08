using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace OpenVpn
{
    internal sealed class StrictOrderPacketsQueue<T>
    {
        private readonly int _maximumSize;
        private readonly ILogger<StrictOrderPacketsQueue<T>> _logger;
        private readonly Dictionary<long, T?> _packets = new();
        private long _lastId;
        private long _firstId;

        public StrictOrderPacketsQueue(int maximumSize, int firstId, ILogger<StrictOrderPacketsQueue<T>> logger)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumSize, 0, nameof(maximumSize));
            ArgumentOutOfRangeException.ThrowIfLessThan(firstId, 0, nameof(firstId));

            _maximumSize = maximumSize;
            _lastId = firstId - 1;
            _firstId = firstId;
            _logger = logger;
        }

        public IEnumerable<uint> GetReceivedIds()
        {
            for (var i = _lastId - _maximumSize + 1; i <= _lastId; i++)
            {
                if (i < 0)
                    continue;

                if (i < _firstId)
                {
                    yield return (uint)i;
                    continue;
                }

                if (_packets.TryGetValue(i, out var packet))
                {
                    if (packet == null)
                        continue;

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
            if (_packets.TryGetValue(id, out var queuedPacket))
            {
                if (queuedPacket != null)
                {
                    _logger.LogWarning($"Packet {id} duplicated. Dropping");
                    return false;
                }
                else
                {
                    _packets[id] = queuedPacket;
                    return true;
                }
            }

            if (_packets.Count >= _maximumSize)
            {
                _logger.LogWarning($"Overcapacity when pushing packet {id}. Dropping");
                return false;
            }

            if (id <= _lastId)
            {
                _logger.LogWarning($"Packet {id} too old. Dropping");
                return false;
            }

            var difference = id - _lastId;
            var availableSlotsAmount = _maximumSize - _packets.Count;

            if (availableSlotsAmount < difference)
            {
                _logger.LogWarning($"Overcapacity when pushing packet {id}. Dropping");
                return false;
            }

            for (var i = _lastId + 1; i <= id; i++)
            {
                T? queuingPacket = i == id ?
                    packet :
                    default;

                _packets.Add(i, queuingPacket);
            }

            _lastId = id;
            return true;
        }

        public bool TryDequeue([NotNullWhen(true)] out T? packet)
        {
            if (!_packets.TryGetValue(_firstId, out packet))
                return false;

            if (packet == null)
                return false;

            _packets.Remove(_firstId);
            _firstId++;
            return true;
        }
    }
}
