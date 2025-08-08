using System.Diagnostics.CodeAnalysis;

namespace OpenVpn.Queues
{
    internal interface IPacketsQueue<T>
    {
        IEnumerable<uint> GetReceivedIds();
        bool TryEnqueue(uint id, T packet);
        bool TryDequeue([NotNullWhen(true)] out T? packet);
    }
}
