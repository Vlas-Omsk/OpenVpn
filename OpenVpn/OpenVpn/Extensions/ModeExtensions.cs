namespace OpenVpn
{
    internal static class ModeExtensions
    {
        public static OpenVpnMode Invert(this OpenVpnMode self)
        {
            return self switch
            {
                OpenVpnMode.Client => OpenVpnMode.Server,
                OpenVpnMode.Server => OpenVpnMode.Client,
                _ => throw new NotSupportedException()
            };
        }
    }
}
