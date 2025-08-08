using System.Text;

namespace OpenVpn
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class DataPacketAttribute : Attribute
    {
        public DataPacketAttribute(string identifier)
        {
            Identifier = Encoding.UTF8.GetBytes(identifier);
        }

        public DataPacketAttribute(byte[] identifier)
        {
            Identifier = identifier;
        }

        public byte[] Identifier { get; }
    }
}
