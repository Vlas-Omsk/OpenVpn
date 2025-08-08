namespace OpenVpn.Options.Converters
{
    internal sealed class StringOptionConverter : SingleValueOptionConverter
    {
        protected override object? ConvertOverride(string name, string? value, Type targetType)
        {
            return value;
        }
    }
}
