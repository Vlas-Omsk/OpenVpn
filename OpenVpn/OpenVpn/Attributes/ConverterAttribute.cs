namespace OpenVpn
{
    [AttributeUsage(AttributeTargets.Property)]
    internal class ConverterAttribute : Attribute
    {
        public ConverterAttribute(Type converterType, params object[] args)
        {
            ConverterType = converterType;
            Args = args;
        }

        public Type ConverterType { get; }
        public object[] Args { get; }
    }
}
