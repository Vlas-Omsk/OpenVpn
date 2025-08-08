namespace OpenVpn
{
    [AttributeUsage(AttributeTargets.Property)]
    internal class NameAttribute : Attribute
    {
        public NameAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
