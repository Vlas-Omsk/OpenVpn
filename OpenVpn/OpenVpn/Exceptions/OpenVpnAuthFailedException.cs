namespace OpenVpn.Exceptions
{
    public class OpenVpnAuthFailedException : OpenVpnProtocolException
    {
        public OpenVpnAuthFailedException(string? message) : base(message)
        {
        }
    }
}
