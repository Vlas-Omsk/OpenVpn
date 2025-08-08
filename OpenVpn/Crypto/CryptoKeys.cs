using System.Buffers.Binary;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace OpenVpn.Crypto
{
    internal readonly struct CryptoKeys
    {
        private const string _masterSecretlabel = "OpenVPN master secret";
        private const int _masterSecretLength = 48;
        private const string _keyExpansionLabel = "OpenVPN key expansion";
        private const string _exportKeyDataLabel = "EXPORTER-OpenVPN-datakeys";

        public CryptoKeys(ReadOnlyMemory<byte> keys)
        {
            Client = new CryptoKey(keys.Slice(0, CryptoKey.KeyLength));
            Server = new CryptoKey(keys.Slice(CryptoKey.KeyLength, CryptoKey.KeyLength));
        }

        public CryptoKey Client { get; }
        public CryptoKey Server { get; }

        public static CryptoKeys DeriveFromTls(
            ITlsKeyingMaterialExporter keyingMaterialExporter
        )
        {
            var keys = keyingMaterialExporter.Export(
                _exportKeyDataLabel,
                CryptoKey.KeyLength * 2
            );

            return new(keys);
        }

        public static CryptoKeys DeriveFromKeySources(
            CryptoKeySource clientKeySource,
            ulong clientSessionId,
            CryptoKeySource serverKeySource,
            ulong serverSessionId
        )
        {
            var masterSecret = GeneratePrf(
                clientKeySource.PreMaster.Span,
                _masterSecretlabel,
                clientKeySource.Random1.Span,
                serverKeySource.Random1.Span,
                0,
                0,
                _masterSecretLength
            );
            var keys = GeneratePrf(
                masterSecret,
                _keyExpansionLabel,
                clientKeySource.Random2.Span,
                serverKeySource.Random2.Span,
                clientSessionId,
                serverSessionId,
                CryptoKey.KeyLength * 2
            );

            return new(keys);
        }

        // TODO: Use bouncy castle prf
        private static byte[] GeneratePrf(
            ReadOnlySpan<byte> secret,
            string label,
            ReadOnlySpan<byte> clientSeed,
            ReadOnlySpan<byte> serverSeed,
            ulong clientSessionId,
            ulong serverSessionId,
            int outputLength
        )
        {
            var labelBytes = Encoding.UTF8.GetBytes(label);
            var seedLength =
                labelBytes.Length +
                clientSeed.Length +
                serverSeed.Length +
                (clientSessionId == 0 ? 0 : sizeof(ulong)) +
                (serverSessionId == 0 ? 0 : sizeof(ulong));
            var seed = new byte[seedLength];

            var offset = 0;

            labelBytes.CopyTo(seed.AsSpan(offset));
            offset += labelBytes.Length;

            clientSeed.CopyTo(seed.AsSpan(offset));
            offset += clientSeed.Length;

            serverSeed.CopyTo(seed.AsSpan(offset));
            offset += serverSeed.Length;

            if (clientSessionId != 0)
                BinaryPrimitives.WriteUInt64BigEndian(seed.AsSpan(offset), clientSessionId);
            offset += sizeof(ulong);

            if (serverSessionId != 0)
                BinaryPrimitives.WriteUInt64BigEndian(seed.AsSpan(offset), serverSessionId);

            return GenerateTls10Prf(secret, seed, outputLength);
        }

        private static byte[] GenerateTls10Prf(
            ReadOnlySpan<byte> secret,
            ReadOnlySpan<byte> seed,
            int outputLength
        )
        {
            var halfLength = (secret.Length + 1) / 2;
            var s1 = new byte[halfLength];
            var s2 = new byte[halfLength];

            secret.Slice(0, halfLength).CopyTo(s1);
            secret.Slice(halfLength).CopyTo(s2);

            var md5Result = GeneratePHash(new MD5Digest(), s1, seed, outputLength);
            var sha1Result = GeneratePHash(new Sha1Digest(), s2, seed, outputLength);

            var result = new byte[outputLength];
            for (int i = 0; i < outputLength; i++)
            {
                result[i] = (byte)(md5Result[i] ^ sha1Result[i]);
            }

            return result;
        }

        private static byte[] GeneratePHash(
            IDigest digest,
            ReadOnlySpan<byte> secret,
            ReadOnlySpan<byte> seed,
            int outputLength
        )
        {
            var hmac = new HMac(digest);

            hmac.Init(new KeyParameter(secret));

            var result = new byte[outputLength];
            var a = seed.ToArray();
            var offset = 0;

            while (offset < outputLength)
            {
                // A(i) = HMAC_hash(secret, A(i-1))
                hmac.Reset();
                hmac.BlockUpdate(a);

                a = new byte[hmac.GetMacSize()];

                hmac.DoFinal(a);

                // HMAC_hash(secret, A(i) + seed)
                hmac.Reset();
                hmac.BlockUpdate(a);
                hmac.BlockUpdate(seed);

                var hash = new byte[hmac.GetMacSize()];

                hmac.DoFinal(hash, 0);

                var bytesToCopy = Math.Min(hash.Length, outputLength - offset);
                Array.Copy(hash, 0, result, offset, bytesToCopy);
                offset += bytesToCopy;
            }

            return result;
        }
    }
}
