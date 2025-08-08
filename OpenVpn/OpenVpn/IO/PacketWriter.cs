using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenVpn.IO
{
    internal sealed class PacketWriter : IDisposable
    {
        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private bool _disposed;

        public PacketWriter(Stream stream, bool leaveOpen = false)
        {
            _stream = stream;
            _leaveOpen = leaveOpen;
        }

        public Encoding Encoding { get; } = Encoding.UTF8;

        public void WriteByte(byte value)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PacketWriter));

            _stream.WriteByte(value);
        }

        public void WriteBytes(ReadOnlySpan<byte> value)
        {
            _stream.Write(value);
        }

        public void WriteShort(short value, int bytesAmount = sizeof(short))
        {
            if (bytesAmount == 0)
                return;

            ArgumentOutOfRangeException.ThrowIfLessThan(bytesAmount, 0, nameof(bytesAmount));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(bytesAmount, sizeof(short), nameof(bytesAmount));

            var valueSpan = MemoryMarshal.Cast<short, byte>(new Span<short>(ref value));

            WriteValue(valueSpan, bytesAmount);
        }

        public void WriteUShort(ushort value, int bytesAmount = sizeof(ushort))
        {
            if (bytesAmount == 0)
                return;

            ArgumentOutOfRangeException.ThrowIfLessThan(bytesAmount, 0, nameof(bytesAmount));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(bytesAmount, sizeof(ushort), nameof(bytesAmount));

            var valueSpan = MemoryMarshal.Cast<ushort, byte>(new Span<ushort>(ref value));

            WriteValue(valueSpan, bytesAmount);
        }

        public void WriteInt(int value, int bytesAmount = sizeof(int))
        {
            if (bytesAmount == 0)
                return;

            ArgumentOutOfRangeException.ThrowIfLessThan(bytesAmount, 0, nameof(bytesAmount));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(bytesAmount, sizeof(int), nameof(bytesAmount));

            var valueSpan = MemoryMarshal.Cast<int, byte>(new Span<int>(ref value));

            WriteValue(valueSpan, bytesAmount);
        }

        public void WriteUInt(uint value, int bytesAmount = sizeof(uint))
        {
            if (bytesAmount == 0)
                return;

            ArgumentOutOfRangeException.ThrowIfLessThan(bytesAmount, 0, nameof(bytesAmount));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(bytesAmount, sizeof(uint), nameof(bytesAmount));

            var valueSpan = MemoryMarshal.Cast<uint, byte>(new Span<uint>(ref value));

            WriteValue(valueSpan, bytesAmount);
        }

        public void WriteLong(long value, int bytesAmount = sizeof(long))
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bytesAmount, 0, nameof(bytesAmount));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(bytesAmount, sizeof(long), nameof(bytesAmount));

            var valueSpan = MemoryMarshal.Cast<long, byte>(new Span<long>(ref value));

            WriteValue(valueSpan, bytesAmount);
        }

        public void WriteULong(ulong value, int bytesAmount = sizeof(ulong))
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bytesAmount, 0, nameof(bytesAmount));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(bytesAmount, sizeof(ulong), nameof(bytesAmount));

            var valueSpan = MemoryMarshal.Cast<ulong, byte>(new Span<ulong>(ref value));

            WriteValue(valueSpan, bytesAmount);
        }

        public int GetStringSize(string str)
        {
            var bytesAmount = Encoding.GetByteCount(str);

            return bytesAmount == 0 ?
                0 :
                bytesAmount + 1;
        }

        public void WriteString(string str)
        {
            var bytesAmount = GetStringSize(str);

            var bytes = ArrayPool<byte>.Shared.Rent(bytesAmount);

            try
            {
                var readedBytes = Encoding.GetBytes(str, bytes);

                if (readedBytes > 0)
                {
                    WriteBytes(bytes.AsSpan(0, readedBytes));
                    WriteByte(0);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        private void WriteValue(ReadOnlySpan<byte> value, int bytesAmount)
        {
            if (BitConverter.IsLittleEndian)
            {
                for (var i = 0; i < bytesAmount; i++)
                    WriteByte(value[bytesAmount - 1 - i]);
            }
            else
            {
                WriteBytes(value.Slice(value.Length - bytesAmount));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (!_leaveOpen)
                    _stream?.Dispose();

                _disposed = true;
            }
        }
    }
}
