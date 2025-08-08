namespace OpenVpn.Options.Converters
{
    internal sealed class BoolOptionConverter : IOptionConverter
    {
        public object? Convert(string name, string? value, Type targetType)
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
