namespace OpenVpn
{
    internal static class PlatformExtensions
    {
        public static string GetPeerInfoName(this OpenVpnPlatform self)
        {
            return self switch
            {
                OpenVpnPlatform.Linux => "linux",
                OpenVpnPlatform.Mac => "mac",
                OpenVpnPlatform.NetBSD => "netbsd",
                OpenVpnPlatform.FreeBSD => "freebsd",
                OpenVpnPlatform.Android => "android",
                OpenVpnPlatform.Windows => "win",
                OpenVpnPlatform.Unknown => "unknown",
                _ => throw new NotSupportedException()
            };
        }
    }
}
