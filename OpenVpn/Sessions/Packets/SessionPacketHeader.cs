using BitsKit.Primitives;
using OpenVpn.IO;

namespace OpenVpn.Sessions.Packets
{
    internal abstract class SessionPacketHeader : ISessionPacketHeader
    {
        private byte _keyId;

        public abstract byte Opcode { get; }

        public required byte KeyId
        {
            get => _keyId;
            init => _keyId = value;
        }

        public virtual void Serialize(PacketWriter writer)
        {
            writer.WriteByte(CombineOpcodeKeyId(Opcode, KeyId));
        }

        public virtual bool TryDeserialize(PacketReader reader)
        {
            (var opcode, _keyId) = SplitOpcodeKeyId(reader.ReadByte());

            if (opcode != Opcode)
                return false;

            return true;
        }

        public static (byte Opcode, byte KeyId) SplitOpcodeKeyId(byte value)
        {
            return (
                BitPrimitives.ReadUInt8MSB(value, 0, 5),
                BitPrimitives.ReadUInt8MSB(value, 5, 3)
            );
        }

        public static byte CombineOpcodeKeyId(byte opcode, byte keyId)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(opcode, 31, nameof(opcode));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(keyId, 7, nameof(keyId));

            var value = new byte();

            BitPrimitives.WriteUInt8MSB(ref value, 0, opcode, 5);
            BitPrimitives.WriteUInt8MSB(ref value, 5, keyId, 3);

            return value;
        }
    }
}
