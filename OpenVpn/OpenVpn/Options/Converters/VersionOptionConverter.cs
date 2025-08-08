namespace OpenVpn.Options.Converters
{
    internal sealed class VersionOptionConverter : SingleValueOptionConverter
    {
        protected override object? ConvertOverride(string name, string? value, Type targetType)
        {
            if (value != null)
                throw new FormatException("Version value should be null");

            switch (name)
            {
                case "V4":
                    return 4;
                default:
                    throw new FormatException("Unknown version string");
            }
        }
    }
}
