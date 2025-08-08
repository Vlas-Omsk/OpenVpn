namespace OpenVpn.Options.Converters
{
    internal interface IOptionConverter
    {
        object? Convert(string name, string? value, Type targetType);
    }
}
