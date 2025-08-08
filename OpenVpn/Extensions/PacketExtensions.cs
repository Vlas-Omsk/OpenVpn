using System.Reflection;
using OpenVpn.Control.Packets;
using OpenVpn.Data.Packets;
using OpenVpn.Sessions.Packets;
using PinkSystem.Runtime;

namespace OpenVpn
{
    internal static class PacketExtensions
    {
        public static byte GetSessionPacketOpcode(this Type self)
        {
            var instance = (ISessionPacketHeader)ObjectAccessor.Create(self).Instance!;

            return instance.Opcode;
        }

        public static byte[] GetIdentifier(this IControlPacket self)
        {
            return GetControlPacketIdentifier(self.GetType());
        }

        public static byte[] GetControlPacketIdentifier(this Type self)
        {
            var attribute = self.GetCustomAttribute<ControlPacketAttribute>();

            if (attribute == null)
                throw new InvalidOperationException($"Packet must have {nameof(ControlPacketAttribute)}");

            return attribute.Identifier;
        }

        public static byte[] GetIdentifier(this IDataPacket self)
        {
            return GetDataPacketIdentifier(self.GetType());
        }

        public static byte[] GetDataPacketIdentifier(this Type self)
        {
            var attribute = self.GetCustomAttribute<DataPacketAttribute>();

            if (attribute == null)
                throw new InvalidOperationException($"Packet must have {nameof(DataPacketAttribute)}");

            return attribute.Identifier;
        }
    }
}
