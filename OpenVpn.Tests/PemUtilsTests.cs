using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;

namespace OpenVpn.Tests
{
    public class PemUtilsTests
    {
        private const string ValidCertificatePem = """
            -----BEGIN CERTIFICATE-----
            MIIB1zCCAXygAwIBAgIQTexhyWb/eL62lIpXEJDwcDAKBggqhkjOPQQDAjAeMRww
            GgYDVQQDDBNjbl9uY0kySU9EbzZqY3pYVVBsMB4XDTI1MDgwNTE4MjEzM1oXDTM1
            MDgwMzE4MjEzM1owDzENMAsGA1UEAwwEdGVzdDBZMBMGByqGSM49AgEGCCqGSM49
            AwEHA0IABCKhxiffS8RKfvJ2LiWCGRJQNyjbKolpwZ+Vxmoys/ozy3A9jxScq3/G
            NHdKMWwQqa5qHxuEFOZM0EGrr03uFECjgaowgacwCQYDVR0TBAIwADAdBgNVHQ4E
            FgQUv3AZ7r5HcO24EEnIxXia0w3kpCcwWQYDVR0jBFIwUIAUPcAGVQNuX9eevkKI
            82QFdbtvX3ChIqQgMB4xHDAaBgNVBAMME2NuX25jSTJJT0RvNmpjelhVUGyCFFw4
            MJoiEXfd5iLKE7k8yEBEDl89MBMGA1UdJQQMMAoGCCsGAQUFBwMCMAsGA1UdDwQE
            AwIHgDAKBggqhkjOPQQDAgNJADBGAiEAxWV1luN3xQkVbVLP1C0FZuwbZio2ZMs2
            nI0pqB0kD/ICIQD80bZ5PIwetmzlFfWy4kUk1ghwSA361fttL8cJ9W36Lg==
            -----END CERTIFICATE-----
            """;

        private const string ValidPrivateKeyPem = """
            -----BEGIN PRIVATE KEY-----
            MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQge5+smInRPshGxYo/
            hTmdaa/M1iUzz4sycG8DgyzDW66hRANCAAQiocYn30vESn7ydi4lghkSUDco2yqJ
            acGflcZqMrP6M8twPY8UnKt/xjR3SjFsEKmuah8bhBTmTNBBq69N7hRA
            -----END PRIVATE KEY-----
            """;

        private const string InvalidPem = """
            -----BEGIN INVALID-----
            InvalidContent
            -----END INVALID-----
            """;

        [Fact]
        public void ParseCertificate_ValidPem_ReturnsX509Certificate()
        {
            var certificate = PemUtils.ParseCertificate(ValidCertificatePem);

            Assert.NotNull(certificate);
            Assert.IsType<X509Certificate>(certificate);
            Assert.Equal("test", certificate.SubjectDN.ToString().Split('=').Last());
            Assert.Equal("cn_ncI2IODo6jczXUPl", certificate.IssuerDN.ToString().Split('=').Last());
        }

        [Fact]
        public void ParseCertificate_InvalidPem_ThrowsException()
        {
            Assert.ThrowsAny<Exception>(() => PemUtils.ParseCertificate(InvalidPem));
        }

        [Fact]
        public void ParseCertificate_EmptyString_ThrowsException()
        {
            Assert.ThrowsAny<Exception>(() => PemUtils.ParseCertificate(string.Empty));
        }

        [Fact]
        public void ParseCertificate_NullString_ThrowsException()
        {
            Assert.ThrowsAny<Exception>(() => PemUtils.ParseCertificate(null!));
        }

        [Fact]
        public void ParsePrivateKey_ValidPem_ReturnsAsymmetricKeyParameter()
        {
            var privateKey = PemUtils.ParsePrivateKey(ValidPrivateKeyPem);

            Assert.NotNull(privateKey);
            Assert.IsAssignableFrom<AsymmetricKeyParameter>(privateKey);
            Assert.True(privateKey.IsPrivate);
        }

        [Fact]
        public void ParsePrivateKey_InvalidPem_ThrowsException()
        {
            Assert.ThrowsAny<Exception>(() => PemUtils.ParsePrivateKey(InvalidPem));
        }

        [Fact]
        public void ParsePrivateKey_EmptyString_ThrowsException()
        {
            Assert.ThrowsAny<Exception>(() => PemUtils.ParsePrivateKey(string.Empty));
        }

        [Fact]
        public void ParsePrivateKey_NullString_ThrowsException()
        {
            Assert.ThrowsAny<Exception>(() => PemUtils.ParsePrivateKey(null!));
        }

        [Fact]
        public void ParsePrivateKey_CertificatePem_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(() => PemUtils.ParsePrivateKey(ValidCertificatePem));
            Assert.Contains("Invalid private key PEM format", exception.Message);
            Assert.Equal("pem", exception.ParamName);
        }

        [Fact]
        public void ParseCertificate_PrivateKeyPem_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(() => PemUtils.ParseCertificate(ValidPrivateKeyPem));
            Assert.Contains("Invalid certificate PEM format", exception.Message);
            Assert.Equal("pem", exception.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\n\r")]
        [InlineData("not a pem")]
        public void ParseCertificate_InvalidFormats_ThrowsException(string invalidPem)
        {
            Assert.ThrowsAny<Exception>(() => PemUtils.ParseCertificate(invalidPem));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\n\r")]
        [InlineData("not a pem")]
        public void ParsePrivateKey_InvalidFormats_ThrowsException(string invalidPem)
        {
            Assert.ThrowsAny<Exception>(() => PemUtils.ParsePrivateKey(invalidPem));
        }
    }
}