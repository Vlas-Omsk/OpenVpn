namespace OpenVpn.Options.Converters
{
    internal sealed class ParseOptionConverter<T> : SingleValueOptionConverter
        where T : IParsable<T>
    {
        protected override object? ConvertOverride(string name, string? value, Type targetType)
        {
            if (value == null)
            {
                if (targetType.IsValueType &&
                    Nullable.GetUnderlyingType(targetType) == null)
                    throw new FormatException($"Value cannot be null for type {targetType}");

                return null;
            }

            return T.Parse(value, provider: null);
        }
    }
}
