namespace OpenVpn.Options.Converters
{
    internal interface IOptionConverter
    {
        object? Convert(string name, IReadOnlyList<string>? value, Type targetType);
    }
}
