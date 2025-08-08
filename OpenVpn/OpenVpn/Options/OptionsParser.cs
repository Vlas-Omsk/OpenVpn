using System.Collections.Immutable;
using System.Text;

namespace OpenVpn.Options
{
    internal static class OptionsParser
    {
        public static string Stringify(IReadOnlyDictionary<string, string?> options, char separator, char keyValueSeparator)
        {
            return string.Join(separator, options.Select(x => $"{x.Key}{(x.Value == null ? "" : $"{keyValueSeparator}{x.Value}")}"));
        }

        public static IReadOnlyDictionary<string, string?> Parse(TextReader reader, char separator, char keyValueSeparator)
        {
            var options = ImmutableDictionary.CreateBuilder<string, string?>();

            Span<char> buffer = stackalloc char[1024];
            var currentPart = new StringBuilder();
            int charsRead;
            var inKey = true;
            string? currentKey = null;

            void ThrowIfKeyEmpty(string? key)
            {
                if (string.IsNullOrWhiteSpace(key) ||
                    string.IsNullOrEmpty(key))
                    throw new ArgumentException("Key cannot be null or empty");
            }

            while ((charsRead = reader.Read(buffer)) > 0)
            {
                var span = buffer.Slice(0, charsRead);

                for (int i = 0; i < span.Length; i++)
                {
                    char c = span[i];

                    if (c == separator)
                    {
                        var partStr = currentPart.ToString();

                        if (inKey)
                        {
                            ThrowIfKeyEmpty(partStr);

                            options.Add(partStr, null);
                        }
                        else
                        {
                            ThrowIfKeyEmpty(currentKey);

                            options.Add(currentKey!, partStr);
                        }

                        currentPart.Clear();
                        inKey = true;
                        currentKey = null;
                    }
                    else if (c == keyValueSeparator && inKey)
                    {
                        currentKey = currentPart.ToString();
                        currentPart.Clear();
                        inKey = false;
                    }
                    else
                    {
                        currentPart.Append(c);
                    }
                }
            }

            // Handle the last part
            if (currentPart.Length > 0)
            {
                var partStr = currentPart.ToString();

                if (inKey)
                {
                    ThrowIfKeyEmpty(partStr);

                    options.Add(partStr, null);
                }
                else
                {
                    ThrowIfKeyEmpty(currentKey);

                    options.Add(currentKey!, partStr);
                }
            }

            return options;
        }
    }
}
