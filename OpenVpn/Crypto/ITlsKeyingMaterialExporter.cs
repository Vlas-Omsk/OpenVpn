namespace OpenVpn.Crypto
{
    internal interface ITlsKeyingMaterialExporter
    {
        byte[] Export(string label, int length);
    }
}
