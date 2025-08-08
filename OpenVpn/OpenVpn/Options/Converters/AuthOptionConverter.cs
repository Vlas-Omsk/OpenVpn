namespace OpenVpn.Options.Converters
{
    internal sealed class AuthOptionConverter : SingleValueOptionConverter
    {
        protected override object? ConvertOverride(string name, string? value, Type targetType)
        {
            if (value == null)
                return null;

            if (value == "[null-digest]")
                return null;

            return value;
        }
    }
}
