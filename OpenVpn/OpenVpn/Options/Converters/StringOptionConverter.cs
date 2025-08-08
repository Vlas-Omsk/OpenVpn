namespace OpenVpn.Options.Converters
{
    internal sealed class StringOptionConverter : IOptionConverter
    {
        public object? Convert(string name, string? value, Type targetType)
        {
            return value;
        }
    }
}
