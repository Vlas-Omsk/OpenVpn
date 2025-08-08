namespace OpenVpn
{
    public class OpenVpnUnexpectedPacketTypeException : Exception
    {
        public OpenVpnUnexpectedPacketTypeException(Type type) : base($"Received packet type unexpected {type.Name}")
        {
        }
    }
}
