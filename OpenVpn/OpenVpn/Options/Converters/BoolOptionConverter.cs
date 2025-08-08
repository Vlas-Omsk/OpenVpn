namespace OpenVpn.Options.Converters
{
    internal sealed class BoolOptionConverter : SingleValueOptionConverter
    {
        protected override object? ConvertOverride(string name, string? value, Type targetType)
        {
            if (value == null)
            {
                if (Nullable.GetUnderlyingType(targetType) != null)
                    return null;

                return true;
            }

            return bool.Parse(value);
        }
    }
}
