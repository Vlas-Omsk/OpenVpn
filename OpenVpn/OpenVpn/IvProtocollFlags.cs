namespace OpenVpn
{
    // https://github.com/OpenVPN/openvpn/blob/d1f2afc26bc8cc1837c2c12981e7eb6afdd4fcf6/src/openvpn/ssl.h#L72
    [Flags]
    internal enum IvProtocollFlags : uint
    {
        None = 0,
        DataV2 = 1 << 1,
        RequestPush = 1 << 2,
        TlsKeyMaterialExport = 1 << 3,
        AuthPending = 1 << 4,
        NcpP2p = 1 << 5,
        DnsOption = 1 << 6,
        ExitNotify = 1 << 7,
        AuthFailTemp = 1 << 8,
        DynamicTlsCrypt = 1 << 9,
        DataEpoch = 1 << 10,
        DnsOptionV2 = 1 << 11,
    }
}
