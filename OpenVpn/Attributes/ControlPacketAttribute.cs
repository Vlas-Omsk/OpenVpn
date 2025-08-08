using System.Text;

namespace OpenVpn
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class ControlPacketAttribute : Attribute
    {
        public ControlPacketAttribute(string identifier)
        {
            Identifier = Encoding.UTF8.GetBytes(identifier);
        }

        public ControlPacketAttribute(byte[] identifier)
        {
            Identifier = identifier;
        }

        public byte[] Identifier { get; }
    }
}
