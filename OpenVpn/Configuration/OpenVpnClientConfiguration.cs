using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenVpn.Configuration
{
    public sealed class OpenVpnClientConfiguration
    {
        public Version Version { get; init; } = Assembly.GetExecutingAssembly().GetName().Version!;
        public OpenVpnPlatform Platform { get; init; } = DetectPlatform();
        public string Name { get; init; } = $"Vlas-Omsk/OpenVpn_{Assembly.GetExecutingAssembly().GetName().Version!}";
        public required EndPoint Remote { get; init; }
        public required ProtocolType Protocol { get; init; }
        public IOpenVpnControlCrypto? ControlCrypto { get; init; }
        public IOpenVpnControlWrapper? ControlWrapper { get; init; }
        public IEnumerable<string> DataCiphers { get; init; } = ["AES-256-GCM", "AES-128-GCM"];

        public static OpenVpnPlatform DetectPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return OpenVpnPlatform.Linux;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return OpenVpnPlatform.Mac;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                return OpenVpnPlatform.FreeBSD;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return OpenVpnPlatform.Windows;

            return OpenVpnPlatform.Unknown;
        }
    }
}
