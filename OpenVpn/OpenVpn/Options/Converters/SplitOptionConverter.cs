
namespace OpenVpn.Options.Converters
{
    internal sealed class SplitOptionConverter : IOptionConverter
    {
        private readonly char _delimiter;

        public SplitOptionConverter(char delimiter)
        {
            _delimiter = delimiter;
        }

        public object? Convert(string name, string? value, Type targetType)
        {
            if (value == null)
                return null;

            return value.Split(_delimiter);
        }
    }
}
