namespace OpenVpn
{
    public class OpenVpnConnectionClosedException : Exception
    {
        public OpenVpnConnectionClosedException() : base("Connection closed")
        {
        }
    }
}
