using OpenVpn.IO;
using System.Text;

namespace OpenVpn.Tests
{
    public class PacketWriterTests
    {
        [Fact]
        public void Constructor_WithStream_InitializesCorrectly()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.NotNull(writer);
            Assert.Equal(Encoding.UTF8, writer.Encoding);
        }

        [Fact]
        public void Constructor_WithLeaveOpenTrue_DoesNotDisposeStream()
        {
            var stream = new MemoryStream();

            using (var writer = new PacketWriter(stream, leaveOpen: true))
            {
                writer.WriteByte(42);
            }

            stream.WriteByte(43);

            Assert.Equal(2, stream.Length);

            stream.Dispose();
        }

        [Fact]
        public void Constructor_WithLeaveOpenFalse_DisposesStream()
        {
            var stream = new MemoryStream();

            using (var writer = new PacketWriter(stream, leaveOpen: false))
            {
                writer.WriteByte(42);
            }

            Assert.Throws<ObjectDisposedException>(() => stream.WriteByte(43));
        }

        [Fact]
        public void WriteByte_ValidValue_WritesToStream()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            writer.WriteByte(0x42);

            Assert.Equal(new byte[] { 0x42 }, stream.ToArray());
        }

        [Fact]
        public void WriteByte_AfterDispose_ThrowsObjectDisposedException()
        {
            using var stream = new MemoryStream();
            var writer = new PacketWriter(stream, leaveOpen: true);

            writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => writer.WriteByte(42));
        }

        [Fact]
        public void WriteBytes_ValidSpan_WritesToStream()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            var data = new byte[] { 1, 2, 3, 4, 5 };

            writer.WriteBytes(data);

            Assert.Equal(data, stream.ToArray());
        }

        [Fact]
        public void WriteBytes_EmptySpan_WritesNothing()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            writer.WriteBytes(ReadOnlySpan<byte>.Empty);

            Assert.Equal(0, stream.Length);
        }

        [Theory]
        [InlineData((short)0x1234, 2, new byte[] { 0x12, 0x34 })]
        [InlineData((short)0x1234, 1, new byte[] { 0x34 })]
        [InlineData((short)0x1234, 0, new byte[] { })]
        public void WriteShort_VariousInputs_WritesCorrectBytes(short value, int bytesAmount, byte[] expected)
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            writer.WriteShort(value, bytesAmount);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void WriteShort_NegativeBytesAmount_ThrowsArgumentOutOfRangeException()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteShort(123, -1));
        }

        [Fact]
        public void WriteShort_ExceedsTypeSize_ThrowsArgumentOutOfRangeException()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteShort(123, 3));
        }

        [Theory]
        [InlineData((ushort)0x1234, 2, new byte[] { 0x12, 0x34 })]
        [InlineData((ushort)0x1234, 1, new byte[] { 0x34 })]
        public void WriteUShort_VariousInputs_WritesCorrectBytes(ushort value, int bytesAmount, byte[] expected)
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            writer.WriteUShort(value, bytesAmount);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void WriteUShort_NegativeBytesAmount_ThrowsArgumentOutOfRangeException()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteUShort(123, -1));
        }

        [Fact]
        public void WriteUShort_ExceedsTypeSize_ThrowsArgumentOutOfRangeException()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteUShort(123, 3));
        }

        [Theory]
        [InlineData(0x12345678, 4, new byte[] { 0x12, 0x34, 0x56, 0x78 })]
        [InlineData(0x12345678, 2, new byte[] { 0x56, 0x78 })]
        [InlineData(0x12345678, 1, new byte[] { 0x78 })]
        public void WriteInt_VariousInputs_WritesCorrectBytes(int value, int bytesAmount, byte[] expected)
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            writer.WriteInt(value, bytesAmount);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void WriteInt_NegativeBytesAmount_ThrowsArgumentOutOfRangeException()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteInt(123, -1));
        }

        [Fact]
        public void WriteInt_ExceedsTypeSize_ThrowsArgumentOutOfRangeException()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteInt(123, 5));
        }

        [Theory]
        [InlineData(0x12345678U, 4, new byte[] { 0x12, 0x34, 0x56, 0x78 })]
        [InlineData(0x12345678U, 2, new byte[] { 0x56, 0x78 })]
        public void WriteUInt_VariousInputs_WritesCorrectBytes(uint value, int bytesAmount, byte[] expected)
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            writer.WriteUInt(value, bytesAmount);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void WriteUInt_NegativeBytesAmount_ThrowsArgumentOutOfRangeException()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteUInt(123, -1));
        }

        [Fact]
        public void WriteUInt_ExceedsTypeSize_ThrowsArgumentOutOfRangeException()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteUInt(123, 5));
        }

        [Theory]
        [InlineData(0x123456789ABCDEF0L, 8, new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 })]
        [InlineData(0x123456789ABCDEF0L, 4, new byte[] { 0x9A, 0xBC, 0xDE, 0xF0 })]
        public void WriteLong_VariousInputs_WritesCorrectBytes(long value, int bytesAmount, byte[] expected)
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            writer.WriteLong(value, bytesAmount);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void WriteLong_NegativeBytesAmount_ThrowsArgumentOutOfRangeException()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteLong(123, -1));
        }

        [Fact]
        public void WriteLong_ExceedsTypeSize_ThrowsArgumentOutOfRangeException()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteLong(123, 9));
        }

        [Theory]
        [InlineData(0x123456789ABCDEF0UL, 8, new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 })]
        [InlineData(0x123456789ABCDEF0UL, 4, new byte[] { 0x9A, 0xBC, 0xDE, 0xF0 })]
        public void WriteULong_VariousInputs_WritesCorrectBytes(ulong value, int bytesAmount, byte[] expected)
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            writer.WriteULong(value, bytesAmount);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void WriteULong_NegativeBytesAmount_ThrowsArgumentOutOfRangeException()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteULong(123, -1));
        }

        [Fact]
        public void WriteULong_ExceedsTypeSize_ThrowsArgumentOutOfRangeException()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteULong(123, 9));
        }

        [Fact]
        public void GetStringSize_EmptyString_ReturnsZero()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            var size = writer.GetStringSize("");

            Assert.Equal(0, size);
        }

        [Fact]
        public void GetStringSize_NonEmptyString_ReturnsCorrectSize()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);
            var text = "Hello";

            var size = writer.GetStringSize(text);

            var expectedSize = Encoding.UTF8.GetByteCount(text) + /* Null terminator */ 1;
            Assert.Equal(expectedSize, size);
        }

        [Fact]
        public void WriteString_EmptyString_WritesNothing()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            writer.WriteString("");

            Assert.Equal(0, stream.Length);
        }

        [Fact]
        public void WriteString_NonEmptyString_WritesWithNullTerminator()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);
            var text = "Hello";

            writer.WriteString(text);

            var expected = Encoding.UTF8.GetBytes(text).Concat(new byte[] { 0 }).ToArray();
            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void WriteString_UnicodeString_HandlesCorrectly()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);
            var text = "Hello 世界";

            writer.WriteString(text);

            var expected = Encoding.UTF8.GetBytes(text).Concat(new byte[] { 0 }).ToArray();
            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void MultipleWrites_MaintainCorrectOrder()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            writer.WriteByte(0x01);
            writer.WriteShort(0x0203, 2);
            writer.WriteInt(0x04050607, 4);
            writer.WriteByte(0x08);

            var expected = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void LargeData_HandlesCorrectly()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            var largeData = new byte[100000];

            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }

            writer.WriteBytes(largeData);

            Assert.Equal(largeData, stream.ToArray());
        }

        [Fact]
        public void RoundTrip_WriteThenRead_PreservesData()
        {
            using var stream = new MemoryStream();

            // Write data
            using (var writer = new PacketWriter(stream, leaveOpen: true))
            {
                writer.WriteByte(0x42);
                writer.WriteShort(0x1234);
                writer.WriteInt(0x56789ABC);
                writer.WriteString("Test");
            }

            // Read data back
            stream.Position = 0;
            var reader = new PacketReader(stream.ToArray());

            var byte1 = reader.ReadByte();
            var short1 = reader.ReadShort();
            var int1 = reader.ReadInt();
            using var stringReader = reader.ReadString(5); // "Test" + null terminator
            var text = stringReader.ReadToEnd();

            Assert.Equal(0x42, byte1);
            Assert.Equal(0x1234, short1);
            Assert.Equal(0x56789ABC, int1);
            Assert.Equal("Test", text);
        }

        [Fact]
        public void WriteToClosedStream_ThrowsException()
        {
            var stream = new MemoryStream();
            using var writer = new PacketWriter(stream, leaveOpen: true);

            stream.Close();

            Assert.Throws<ObjectDisposedException>(() => writer.WriteByte(42));
        }

        [Fact]
        public void Encoding_DefaultIsUTF8()
        {
            using var stream = new MemoryStream();
            using var writer = new PacketWriter(stream);

            Assert.Equal(Encoding.UTF8, writer.Encoding);
        }
    }
}