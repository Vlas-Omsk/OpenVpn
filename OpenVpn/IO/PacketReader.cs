using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance;

namespace OpenVpn.IO
{
    internal sealed class PacketReader
    {
        public PacketReader(ReadOnlyMemory<byte> bytes)
        {
            Memory = bytes;
        }

        public int Length => Memory.Length;
        public int Position { get; set; }
        public int Available => Length - Position;
        public ReadOnlyMemory<byte> Memory { get; }
        public ReadOnlyMemory<byte> AvailableMemory => Memory.Slice(Position, Available);
        public ReadOnlySpan<byte> AvailableSpan => Memory.Span.Slice(Position, Available);
        public Encoding Encoding { get; } = Encoding.UTF8;

        public ReadOnlySpan<byte> ReadSpan(int amount)
        {
            var result = PeekSpan(amount);

            Consume(amount);

            return result;
        }

        public ReadOnlySpan<byte> PeekSpan(int amount)
        {
            if (amount == 0)
                return ReadOnlySpan<byte>.Empty;

            if (Position + amount > Length)
                throw new EndOfStreamException($"Unexpected end of stream while reading bits.");

            return Memory.Span.Slice(Position, amount);
        }

        public ReadOnlyMemory<byte> ReadMemory(int amount)
        {
            var result = PeekMemory(amount);

            Consume(amount);

            return result;
        }

        public ReadOnlyMemory<byte> PeekMemory(int amount)
        {
            if (amount == 0)
                return ReadOnlyMemory<byte>.Empty;

            if (Position + amount > Length)
                throw new EndOfStreamException($"Unexpected end of stream while reading bits.");

            return Memory.Slice(Position, amount);
        }

        public void Consume(int amount)
        {
            Position += amount;
        }

        public byte ReadByte()
        {
            var bytes = ReadSpan(1);

            return bytes[0];
        }

        public short ReadShort(int bytesAmount = sizeof(short))
        {
            if (bytesAmount == 0)
                return default;

            ArgumentOutOfRangeException.ThrowIfLessThan(bytesAmount, 0, nameof(bytesAmount));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(bytesAmount, sizeof(short), nameof(bytesAmount));

            var bytes = ReadSpan(bytesAmount);
            var value = new short();
            var valueSpan = MemoryMarshal.Cast<short, byte>(new Span<short>(ref value));

            ReadValue(valueSpan, bytes);

            return value;
        }

        public ushort ReadUShort(int bytesAmount = sizeof(ushort))
        {
            if (bytesAmount == 0)
                return default;

            ArgumentOutOfRangeException.ThrowIfLessThan(bytesAmount, 0, nameof(bytesAmount));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(bytesAmount, sizeof(ushort), nameof(bytesAmount));

            var bytes = ReadSpan(bytesAmount);
            var value = new ushort();
            var valueSpan = MemoryMarshal.Cast<ushort, byte>(new Span<ushort>(ref value));

            ReadValue(valueSpan, bytes);

            return value;
        }

        public int ReadInt(int bytesAmount = sizeof(int))
        {
            if (bytesAmount == 0)
                return default;

            ArgumentOutOfRangeException.ThrowIfLessThan(bytesAmount, 0, nameof(bytesAmount));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(bytesAmount, sizeof(int), nameof(bytesAmount));

            var bytes = ReadSpan(bytesAmount);
            var value = 0;
            var valueSpan = MemoryMarshal.Cast<int, byte>(new Span<int>(ref value));

            ReadValue(valueSpan, bytes);

            return value;
        }

        public uint ReadUInt(int bytesAmount = sizeof(uint))
        {
            if (bytesAmount == 0)
                return default;

            ArgumentOutOfRangeException.ThrowIfLessThan(bytesAmount, 0, nameof(bytesAmount));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(bytesAmount, sizeof(uint), nameof(bytesAmount));

            var bytes = ReadSpan(bytesAmount);
            var value = 0U;
            var valueSpan = MemoryMarshal.Cast<uint, byte>(new Span<uint>(ref value));

            ReadValue(valueSpan, bytes);

            return value;
        }

        public long ReadLong(int bytesAmount = sizeof(long))
        {
            if (bytesAmount == 0)
                return default;

            ArgumentOutOfRangeException.ThrowIfLessThan(bytesAmount, 0, nameof(bytesAmount));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(bytesAmount, sizeof(long), nameof(bytesAmount));

            var bytes = ReadSpan(bytesAmount);
            var value = 0L;
            var valueSpan = MemoryMarshal.Cast<long, byte>(new Span<long>(ref value));

            ReadValue(valueSpan, bytes);

            return value;
        }

        public ulong ReadULong(int bytesAmount = sizeof(ulong))
        {
            if (bytesAmount == 0)
                return default;

            ArgumentOutOfRangeException.ThrowIfLessThan(bytesAmount, 0, nameof(bytesAmount));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(bytesAmount, sizeof(ulong), nameof(bytesAmount));

            var bytes = ReadSpan(bytesAmount);
            var value = 0UL;
            var valueSpan = MemoryMarshal.Cast<ulong, byte>(new Span<ulong>(ref value));

            ReadValue(valueSpan, bytes);

            return value;
        }

        public StreamReader ReadString(int bytesAmount)
        {
            if (bytesAmount == 0)
                return StreamReader.Null;

            var bytes = ReadMemory(bytesAmount);

            if (bytes.Span[^1] != 0)
                throw new FormatException("String must be null-terminated");

            bytes = bytes[..^1];

            return new StreamReader(bytes.AsStream(), Encoding);
        }

        private static void ReadValue(Span<byte> value, ReadOnlySpan<byte> bytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                for (var i = 0; i < bytes.Length; i++)
                    value[i] = bytes[bytes.Length - 1 - i];
            }
            else
            {
                for (var i = 0; i < bytes.Length; i++)
                    value[i] = bytes[i];
            }
        }
    }
}
