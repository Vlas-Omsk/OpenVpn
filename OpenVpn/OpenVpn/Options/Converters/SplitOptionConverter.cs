namespace OpenVpn.Options.Converters
{
    internal sealed class SplitOptionConverter : SingleValueOptionConverter
    {
        private readonly char _delimiter;

        public SplitOptionConverter(char delimiter)
        {
            _delimiter = delimiter;
        }

        protected override object? ConvertOverride(string name, string? value, Type targetType)
        {
            if (value == null)
                return null;

            return value.Split(_delimiter);
        }
    }
}
