using System.Net;

namespace OpenVpn
{
    public sealed class InterfaceConfig
    {
        public required IPAddress Address { get; init; }
        public required int Mask { get; init; }
        public required IPAddress? Gateway { get; init; }

        public static InterfaceConfig ParseIpv4(string ifconfig, string? routeGateway)
        {
            var parts = ifconfig.Split(' ');

            if (parts.Length != 2)
                throw new FormatException("IfConfig should have 2 parts");

            var address = IPAddress.Parse(parts[0]);
            var mask = MaskToBits(parts[1]);
            var gateway = (IPAddress?)null;

            if (routeGateway != null)
                gateway = IPAddress.Parse(routeGateway);

            return new InterfaceConfig()
            {
                Address = address,
                Gateway = gateway,
                Mask = mask
            };
        }

        private static int MaskToBits(string mask)
        {
            if (string.IsNullOrWhiteSpace(mask))
                throw new FormatException("IPv4 mask cannot be null or empty");

            var parts = mask.Split('.');

            if (parts.Length != 4)
                throw new FormatException("IPv4 mask should have 4 parts");

            var bits = 0;
            var previous = 255;

            foreach (var part in parts)
            {
                if (!int.TryParse(part, out int byteValue) || byteValue < 0 || byteValue > 255)
                    throw new FormatException("IPv4 mask should only include numbers > 0 and < 256");

                if (byteValue != 255 && byteValue != 254 && byteValue != 252 && byteValue != 248 &&
                    byteValue != 240 && byteValue != 224 && byteValue != 192 && byteValue != 128 && byteValue != 0)
                    throw new FormatException("IPv4 mask should only include numbers in range [256, 254, 252, 248, 240, 224, 192, 128, 0]");

                if (byteValue > previous)
                    throw new FormatException("IPv4 mask should not increase");

                previous = byteValue;

                bits += CountBits(byteValue);
            }

            return bits;
        }

        private static int CountBits(int value)
        {
            var count = 0;

            while (value > 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }

        public static InterfaceConfig ParseIpv6(string ifconfig)
        {
            var parts = ifconfig.Split(' ');

            if (parts.Length != 2)
                throw new FormatException("IfConfig should have 2 parts");

            var addressParts = parts[0].Split('/');

            var address = IPAddress.Parse(addressParts[0]);
            var mask = int.Parse(addressParts[1]);
            var gateway = IPAddress.Parse(parts[1]);

            return new InterfaceConfig()
            {
                Address = address,
                Gateway = gateway,
                Mask = mask
            };
        }
    }
}
