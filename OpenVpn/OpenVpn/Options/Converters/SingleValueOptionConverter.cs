namespace OpenVpn.Options.Converters
{
    internal abstract class SingleValueOptionConverter : IOptionConverter
    {
        public object? Convert(string name, IReadOnlyList<string>? value, Type targetType)
        {
            if (value == null)
                return ConvertOverride(name, null, targetType);

            if (value.Count > 1)
                throw new FormatException($"{GetType()} converter only support single value");

            return ConvertOverride(name, value[0], targetType);
        }

        protected abstract object? ConvertOverride(string name, string? value, Type targetType);
    }
}
