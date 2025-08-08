namespace OpenVpn
{
    public class OpenVpnProtocolException : Exception
    {
        public OpenVpnProtocolException()
        {
        }

        public OpenVpnProtocolException(string? message) : base(message)
        {
        }

        public OpenVpnProtocolException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
