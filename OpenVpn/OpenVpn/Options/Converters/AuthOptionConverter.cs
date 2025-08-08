namespace OpenVpn.Options.Converters
{
    internal sealed class AuthOptionConverter : IOptionConverter
    {
        public object? Convert(string name, string? value, Type targetType)
        {
            if (value == null)
                return null;

            if (value == "[null-digest]")
                return null;

            return value;
        }
    }
}
