using OpenVpn.IO;
using System.Text;

namespace OpenVpn.Tests
{
    public class PacketReaderTests
    {
        [Fact]
        public void Constructor_WithValidMemory_InitializesCorrectly()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var reader = new PacketReader(data);

            Assert.Equal(5, reader.Length);
            Assert.Equal(0, reader.Position);
            Assert.Equal(5, reader.Available);
        }

        [Fact]
        public void Constructor_WithEmptyMemory_InitializesCorrectly()
        {
            var reader = new PacketReader(ReadOnlyMemory<byte>.Empty);

            Assert.Equal(0, reader.Length);
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.Available);
        }

        [Fact]
        public void AvailableMemory_ReturnsCorrectSlice()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var reader = new PacketReader(data);

            reader.Position = 2;
            var available = reader.AvailableMemory;

            Assert.Equal(3, available.Length);
            Assert.Equal(new byte[] { 3, 4, 5 }, available.ToArray());
        }

        [Fact]
        public void AvailableSpan_ReturnsCorrectSlice()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var reader = new PacketReader(data);

            reader.Position = 1;
            var available = reader.AvailableSpan;

            Assert.Equal(4, available.Length);
            Assert.Equal(new byte[] { 2, 3, 4, 5 }, available.ToArray());
        }

        [Fact]
        public void ReadByte_ValidPosition_ReturnsCorrectByte()
        {
            var data = new byte[] { 0x42, 0x43, 0x44 };
            var reader = new PacketReader(data);

            var result = reader.ReadByte();

            Assert.Equal(0x42, result);
            Assert.Equal(1, reader.Position);
        }

        [Fact]
        public void ReadByte_AtEndOfStream_ThrowsEndOfStreamException()
        {
            var data = new byte[] { 0x42 };
            var reader = new PacketReader(data);
            reader.Position = 1;

            Assert.Throws<EndOfStreamException>(() => reader.ReadByte());
        }

        [Fact]
        public void ReadSpan_ValidAmount_ReturnsCorrectData()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var reader = new PacketReader(data);

            var result = reader.ReadSpan(3);

            Assert.Equal(new byte[] { 1, 2, 3 }, result.ToArray());
            Assert.Equal(3, reader.Position);
        }

        [Fact]
        public void ReadSpan_ZeroAmount_ReturnsEmpty()
        {
            var data = new byte[] { 1, 2, 3 };
            var reader = new PacketReader(data);

            var result = reader.ReadSpan(0);

            Assert.True(result.IsEmpty);
            Assert.Equal(0, reader.Position);
        }

        [Fact]
        public void ReadSpan_ExceedsAvailable_ThrowsEndOfStreamException()
        {
            var data = new byte[] { 1, 2, 3 };
            var reader = new PacketReader(data);

            Assert.Throws<EndOfStreamException>(() => reader.ReadSpan(5));
        }

        [Fact]
        public void PeekSpan_ValidAmount_ReturnsDataWithoutAdvancing()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var reader = new PacketReader(data);

            var result = reader.PeekSpan(3);

            Assert.Equal(new byte[] { 1, 2, 3 }, result.ToArray());
            Assert.Equal(0, reader.Position); // Position should not advance
        }

        [Fact]
        public void ReadMemory_ValidAmount_ReturnsCorrectData()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var reader = new PacketReader(data);

            var result = reader.ReadMemory(3);

            Assert.Equal(new byte[] { 1, 2, 3 }, result.ToArray());
            Assert.Equal(3, reader.Position);
        }

        [Fact]
        public void PeekMemory_ValidAmount_ReturnsDataWithoutAdvancing()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var reader = new PacketReader(data);

            var result = reader.PeekMemory(2);

            Assert.Equal(new byte[] { 1, 2 }, result.ToArray());
            Assert.Equal(0, reader.Position);
        }

        [Fact]
        public void Consume_ValidAmount_AdvancesPosition()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var reader = new PacketReader(data);

            reader.Consume(3);

            Assert.Equal(3, reader.Position);
            Assert.Equal(2, reader.Available);
        }

        [Theory]
        [InlineData(new byte[] { 0x12 }, 1, (short)0x12)]
        [InlineData(new byte[] { 0x12, 0x34 }, 2, (short)0x1234)]
        [InlineData(new byte[] { 0x12, 0x34, 0x56 }, 2, (short)0x1234)]
        public void ReadShort_VariousInputs_ReturnsCorrectValue(byte[] data, int bytesAmount, short expected)
        {
            var reader = new PacketReader(data);

            var result = reader.ReadShort(bytesAmount);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReadShort_ZeroBytes_ReturnsDefault()
        {
            var data = new byte[] { 0x12, 0x34 };
            var reader = new PacketReader(data);

            var result = reader.ReadShort(0);

            Assert.Equal(0, result);
            Assert.Equal(0, reader.Position);
        }

        [Fact]
        public void ReadShort_ExceedsTypeSize_ThrowsArgumentOutOfRangeException()
        {
            var data = new byte[] { 0x12, 0x34, 0x56, 0x78 };
            var reader = new PacketReader(data);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadShort(3));
        }

        [Fact]
        public void ReadShort_NegativeBytes_ThrowsArgumentOutOfRangeException()
        {
            var data = new byte[] { 0x12, 0x34 };
            var reader = new PacketReader(data);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadShort(-1));
        }

        [Theory]
        [InlineData(new byte[] { 0x12 }, 1, (ushort)0x12)]
        [InlineData(new byte[] { 0x12, 0x34 }, 2, (ushort)0x1234)]
        public void ReadUShort_VariousInputs_ReturnsCorrectValue(byte[] data, int bytesAmount, ushort expected)
        {
            var reader = new PacketReader(data);

            var result = reader.ReadUShort(bytesAmount);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReadUShort_ZeroBytes_ReturnsDefault()
        {
            var data = new byte[] { 0x12, 0x34 };
            var reader = new PacketReader(data);

            var result = reader.ReadUShort(0);

            Assert.Equal(0, result);
            Assert.Equal(0, reader.Position);
        }

        [Fact]
        public void ReadUShort_ExceedsTypeSize_ThrowsArgumentOutOfRangeException()
        {
            var data = new byte[] { 0x12, 0x34, 0x56, 0x78 };
            var reader = new PacketReader(data);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadUShort(3));
        }

        [Fact]
        public void ReadUShort_NegativeBytes_ThrowsArgumentOutOfRangeException()
        {
            var data = new byte[] { 0x12, 0x34 };
            var reader = new PacketReader(data);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadUShort(-1));
        }

        [Theory]
        [InlineData(new byte[] { 0x12 }, 1, 0x12)]
        [InlineData(new byte[] { 0x12, 0x34 }, 2, 0x1234)]
        [InlineData(new byte[] { 0x12, 0x34, 0x56, 0x78 }, 4, 0x12345678)]
        public void ReadInt_VariousInputs_ReturnsCorrectValue(byte[] data, int bytesAmount, int expected)
        {
            var reader = new PacketReader(data);

            var result = reader.ReadInt(bytesAmount);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReadInt_ZeroBytes_ReturnsDefault()
        {
            var data = new byte[] { 0x12, 0x34, 0x12, 0x34 };
            var reader = new PacketReader(data);

            var result = reader.ReadInt(0);

            Assert.Equal(0, result);
            Assert.Equal(0, reader.Position);
        }

        [Fact]
        public void ReadInt_ExceedsTypeSize_ThrowsArgumentOutOfRangeException()
        {
            var data = new byte[] { 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34 };
            var reader = new PacketReader(data);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadInt(5));
        }

        [Fact]
        public void ReadInt_NegativeBytes_ThrowsArgumentOutOfRangeException()
        {
            var data = new byte[] { 0x12, 0x34 };
            var reader = new PacketReader(data);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadInt(-1));
        }

        [Theory]
        [InlineData(new byte[] { 0x12 }, 1, 0x12U)]
        [InlineData(new byte[] { 0x12, 0x34 }, 2, 0x1234U)]
        [InlineData(new byte[] { 0x12, 0x34, 0x56, 0x78 }, 4, 0x12345678U)]
        public void ReadUInt_VariousInputs_ReturnsCorrectValue(byte[] data, int bytesAmount, uint expected)
        {
            var reader = new PacketReader(data);

            var result = reader.ReadUInt(bytesAmount);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReadUInt_ZeroBytes_ReturnsDefault()
        {
            var data = new byte[] { 0x12, 0x34, 0x12, 0x34 };
            var reader = new PacketReader(data);

            var result = reader.ReadUInt(0);

            Assert.Equal(0u, result);
            Assert.Equal(0, reader.Position);
        }

        [Fact]
        public void ReadUInt_ExceedsTypeSize_ThrowsArgumentOutOfRangeException()
        {
            var data = new byte[] { 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34 };
            var reader = new PacketReader(data);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadUInt(5));
        }

        [Fact]
        public void ReadUInt_NegativeBytes_ThrowsArgumentOutOfRangeException()
        {
            var data = new byte[] { 0x12, 0x34 };
            var reader = new PacketReader(data);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadUInt(-1));
        }

        [Theory]
        [InlineData(new byte[] { 0x12 }, 1, 0x12L)]
        [InlineData(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }, 8, 0x123456789ABCDEF0L)]
        public void ReadLong_VariousInputs_ReturnsCorrectValue(byte[] data, int bytesAmount, long expected)
        {
            var reader = new PacketReader(data);

            var result = reader.ReadLong(bytesAmount);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReadLong_ZeroBytes_ReturnsDefault()
        {
            var data = new byte[] { 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34 };
            var reader = new PacketReader(data);

            var result = reader.ReadLong(0);

            Assert.Equal(0u, result);
            Assert.Equal(0, reader.Position);
        }

        [Fact]
        public void ReadLong_ExceedsTypeSize_ThrowsArgumentOutOfRangeException()
        {
            var data = new byte[] { 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34 };
            var reader = new PacketReader(data);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadLong(9));
        }

        [Fact]
        public void ReadLong_NegativeBytes_ThrowsArgumentOutOfRangeException()
        {
            var data = new byte[] { 0x12, 0x34 };
            var reader = new PacketReader(data);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadLong(-1));
        }

        [Theory]
        [InlineData(new byte[] { 0x12 }, 1, 0x12UL)]
        [InlineData(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }, 8, 0x123456789ABCDEF0UL)]
        public void ReadULong_VariousInputs_ReturnsCorrectValue(byte[] data, int bytesAmount, ulong expected)
        {
            var reader = new PacketReader(data);

            var result = reader.ReadULong(bytesAmount);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReadULong_ZeroBytes_ReturnsDefault()
        {
            var data = new byte[] { 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34 };
            var reader = new PacketReader(data);

            var result = reader.ReadLong(0);

            Assert.Equal(0u, result);
            Assert.Equal(0, reader.Position);
        }

        [Fact]
        public void ReadULong_ExceedsTypeSize_ThrowsArgumentOutOfRangeException()
        {
            var data = new byte[] { 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34, 0x12, 0x34 };
            var reader = new PacketReader(data);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadLong(9));
        }

        [Fact]
        public void ReadULong_NegativeBytes_ThrowsArgumentOutOfRangeException()
        {
            var data = new byte[] { 0x12, 0x34 };
            var reader = new PacketReader(data);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadULong(-1));
        }

        [Fact]
        public void ReadString_NullTerminatedString_ReturnsCorrectString()
        {
            var text = "Hello World";
            var bytes = Encoding.UTF8.GetBytes(text).Concat(new byte[] { 0 }).ToArray();
            var reader = new PacketReader(bytes);

            using var stringReader = reader.ReadString(bytes.Length);
            var result = stringReader.ReadToEnd();

            Assert.Equal(text, result);
        }

        [Fact]
        public void ReadString_ZeroBytes_ReturnZeroLengthReader()
        {
            var data = new byte[] { 1, 2, 3 };
            var reader = new PacketReader(data);

            var result = reader.ReadString(0);

            Assert.Equal(0, reader.Position);
        }

        [Fact]
        public void ReadString_NotNullTerminated_ThrowsFormatException()
        {
            var bytes = Encoding.UTF8.GetBytes("Hello");
            var reader = new PacketReader(bytes);

            Assert.Throws<FormatException>(() => reader.ReadString(bytes.Length));
        }

        [Fact]
        public void MultipleReads_MaintainCorrectPosition()
        {
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
            var reader = new PacketReader(data);

            var byte1 = reader.ReadByte();
            var short1 = reader.ReadShort(2);
            var int1 = reader.ReadInt(4);
            var remaining = reader.ReadByte();

            Assert.Equal(0x01, byte1);
            Assert.Equal(0x0203, short1);
            Assert.Equal(0x04050607, int1);
            Assert.Equal(0x08, remaining);
            Assert.Equal(8, reader.Position);
        }

        [Fact]
        public void LargeData_HandlesCorrectly()
        {
            var data = new byte[100000];

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 256);
            }

            var reader = new PacketReader(data);

            for (int i = 0; i < 1000; i++)
            {
                var chunk = reader.ReadSpan(100);
                Assert.Equal(100, chunk.Length);
            }

            Assert.Equal(100000, reader.Position);
            Assert.Equal(0, reader.Available);
        }

        [Fact]
        public void Encoding_DefaultIsUTF8()
        {
            var reader = new PacketReader(ReadOnlyMemory<byte>.Empty);

            Assert.Equal(Encoding.UTF8, reader.Encoding);
        }
    }
}