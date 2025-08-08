using System.Text.RegularExpressions;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.X509;

namespace OpenVpn
{
    public static class PemUtils
    {
        private static readonly Regex _staticKeyRegex =
            new("-----BEGIN OpenVPN Static key V1-----([0-9a-fA-F\n\r]*?)-----END OpenVPN Static key V1-----", RegexOptions.Compiled);

        public static X509Certificate ParseCertificate(string pem)
        {
            using var reader = new StringReader(pem);
            var pemReader = new PemReader(reader);

            var pemObject = pemReader.ReadObject();

            return pemObject switch
            {
                X509Certificate cert => cert,
                _ => throw new ArgumentException("Invalid certificate PEM format", nameof(pem))
            };
        }

        public static AsymmetricKeyParameter ParsePrivateKey(string pem)
        {
            using var reader = new StringReader(pem);
            var pemReader = new PemReader(reader);

            var pemObject = pemReader.ReadObject();

            return pemObject switch
            {
                AsymmetricCipherKeyPair keyPair => keyPair.Private,
                AsymmetricKeyParameter privateKey => privateKey,
                _ => throw new ArgumentException("Invalid private key PEM format", nameof(pem))
            };
        }

        public static byte[] ParseStaticKey(string pem)
        {
            var match = _staticKeyRegex.Match(pem);

            if (!match.Success)
                throw new ArgumentException("Invalid OpenVPN static key PEM format");

            var base64 = match.Groups[1].Value.Replace("\n", "").Replace("\r", "").Trim();
            var key = HexStringToByteArray(base64);

            if (key.Length != 256)
                throw new ArgumentException($"Invalid OpenVPN static key length: {key.Length}");

            return key;
        }

        private static byte[] HexStringToByteArray(string hex)
        {
            var bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
