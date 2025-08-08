using System.Diagnostics.CodeAnalysis;

namespace OpenVpn
{
    internal static class DictionaryExtensions
    {
        public static bool TryGetRequiredValue(this IReadOnlyDictionary<string, string?> self, string key, [NotNullWhen(true)] out string? value)
        {
            if (!self.TryGetValue(key, out value) ||
                value == null)
                return false;

            return true;
        }
    }
}
