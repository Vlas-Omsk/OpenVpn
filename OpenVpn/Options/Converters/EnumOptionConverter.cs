namespace OpenVpn.Options.Converters
{
    internal sealed class EnumOptionConverter : IOptionConverter
    {
        public object? Convert(string name, string? value, Type targetType)
        {
            if (value == null)
            {
                if (Nullable.GetUnderlyingType(targetType) == null)
                    throw new FormatException($"Value cannot be null for type {targetType}");

                return null;
            }

            var underlyingType = Nullable.GetUnderlyingType(targetType);

            if (underlyingType != null)
                targetType = underlyingType;

            return Enum.Parse(targetType, value, ignoreCase: true);
        }
    }
}
