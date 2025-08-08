using OpenVpn.Buffers;

namespace OpenVpn.Tests
{
    public class PipeTests
    {
        [Fact]
        public void Constructor_InitializesCorrectly()
        {
            const int capacity = 1024;
            var pipe = new Pipe(capacity);

            Assert.Equal(0, pipe.Available);
            Assert.Equal(capacity, pipe.Capacity);
        }

        [Fact]
        public void Constructor_WithNegativeCapacity_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Pipe(-1));
        }

        [Fact]
        public void Write_EmptySpan_ReturnsZero()
        {
            var pipe = new Pipe();

            var result = pipe.Write(ReadOnlySpan<byte>.Empty);

            Assert.Equal(0, result);
            Assert.Equal(0, pipe.Available);
        }

        [Fact]
        public void Write_ValidData_ReturnsCorrectBytesWritten()
        {
            var pipe = new Pipe();
            var data = new byte[] { 1, 2, 3, 4, 5 };

            var result = pipe.Write(data);

            Assert.Equal(5, result);
            Assert.Equal(5, pipe.Available);
        }

        [Fact]
        public void Write_MultipleTimes_AccumulatesData()
        {
            var pipe = new Pipe();
            var data1 = new byte[] { 1, 2, 3 };
            var data2 = new byte[] { 4, 5, 6 };

            pipe.Write(data1);
            pipe.Write(data2);

            Assert.Equal(6, pipe.Available);
        }

        [Fact]
        public void Write_ExceedsCapacity_ExpandsBuffer()
        {
            var pipe = new Pipe(4);
            var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            var result = pipe.Write(data);

            Assert.Equal(8, result);
            Assert.Equal(8, pipe.Available);
            Assert.True(pipe.Capacity >= 8);
        }

        [Fact]
        public void Read_EmptyPipe_ReturnsZero()
        {
            var pipe = new Pipe();
            var buffer = new byte[10];

            var result = pipe.Read(buffer);

            Assert.Equal(0, result);
        }

        [Fact]
        public void Read_WithData_ReturnsDataAndConsumes()
        {
            var pipe = new Pipe();
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var buffer = new byte[10];

            pipe.Write(data);
            var result = pipe.Read(buffer);

            Assert.Equal(5, result);
            Assert.Equal(0, pipe.Available);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 0, 0, 0, 0, 0 }, buffer);
        }

        [Fact]
        public void Read_PartialData_ReturnsAvailableBytes()
        {
            var pipe = new Pipe();
            var data = new byte[] { 1, 2, 3 };
            var buffer = new byte[10];

            pipe.Write(data);
            var result = pipe.Read(buffer);

            Assert.Equal(3, result);
            Assert.Equal(new byte[] { 1, 2, 3, 0, 0, 0, 0, 0, 0, 0 }, buffer);
        }

        [Fact]
        public void Read_SmallBuffer_ReturnsBufferSizeBytes()
        {
            var pipe = new Pipe();
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var buffer = new byte[3];

            pipe.Write(data);
            var result = pipe.Read(buffer);

            Assert.Equal(3, result);
            Assert.Equal(2, pipe.Available); // 2 bytes remaining
            Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
        }

        [Fact]
        public void Peek_WithData_ReturnsDataWithoutConsuming()
        {
            var pipe = new Pipe();
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var buffer = new byte[3];

            pipe.Write(data);
            var result = pipe.Peek(buffer);

            Assert.Equal(3, result);
            Assert.Equal(5, pipe.Available); // Data not consumed
            Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
        }

        [Fact]
        public void Peek_WithSkip_ReturnsDataFromOffset()
        {
            var pipe = new Pipe();
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var buffer = new byte[3];

            pipe.Write(data);
            var result = pipe.Peek(buffer, skip: 2);

            Assert.Equal(3, result);
            Assert.Equal(5, pipe.Available);
            Assert.Equal(new byte[] { 3, 4, 5 }, buffer);
        }

        [Fact]
        public void Peek_WithSkipExceedingData_ReturnsZero()
        {
            var pipe = new Pipe();
            var data = new byte[] { 1, 2, 3 };
            var buffer = new byte[3];

            pipe.Write(data);
            var result = pipe.Peek(buffer, skip: 3); // Skip exactly the length

            Assert.Equal(0, result);
        }

        [Fact]
        public void Consume_ValidAmount_AdvancesReadPosition()
        {
            var pipe = new Pipe();
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var buffer = new byte[10];

            pipe.Write(data);
            pipe.Consume(2);

            var result = pipe.Read(buffer);
            Assert.Equal(3, result);
            Assert.Equal(new byte[] { 3, 4, 5, 0, 0, 0, 0, 0, 0, 0 }, buffer);
        }

        [Fact]
        public void Consume_NegativeAmount_ThrowsArgumentOutOfRangeException()
        {
            var pipe = new Pipe();

            Assert.Throws<ArgumentOutOfRangeException>(() => pipe.Consume(-1));
        }

        [Fact]
        public void Consume_MoreThanAvailable_ThrowsArgumentOutOfRangeException()
        {
            var pipe = new Pipe();
            var data = new byte[] { 1, 2, 3 };

            pipe.Write(data);

            Assert.Throws<ArgumentOutOfRangeException>(() => pipe.Consume(5));
        }

        [Fact]
        public void Consume_AllData_ResetsPositions()
        {
            var pipe = new Pipe();
            var data = new byte[] { 1, 2, 3 };

            pipe.Write(data);
            pipe.Consume(3);

            Assert.Equal(0, pipe.Available);
        }

        [Fact]
        public void Clear_WithData_RemovesAllData()
        {
            var pipe = new Pipe();
            var data = new byte[] { 1, 2, 3, 4, 5 };

            pipe.Write(data);
            pipe.Clear();

            Assert.Equal(0, pipe.Available);
        }

        [Fact]
        public void AvailableSpan_ReturnsCorrectData()
        {
            var pipe = new Pipe();
            var data = new byte[] { 1, 2, 3, 4, 5 };

            pipe.Write(data);
            var span = pipe.AvailableSpan;

            Assert.Equal(5, span.Length);
            Assert.Equal(data, span.ToArray());
        }

        [Fact]
        public void AvailableMemory_ReturnsCorrectData()
        {
            var pipe = new Pipe();
            var data = new byte[] { 1, 2, 3, 4, 5 };

            pipe.Write(data);
            var memory = pipe.AvailableMemory;

            Assert.Equal(5, memory.Length);
            Assert.Equal(data, memory.ToArray());
        }

        [Fact]
        public void BufferCompaction_TriggersWhenReadPositionSignificant()
        {
            var pipe = new Pipe(100);
            var data = new byte[50];

            // Fill half the buffer
            pipe.Write(data);
            pipe.Consume(30); // Move read position significantly

            var originalCapacity = pipe.Capacity;

            // Write data that would trigger compaction
            var newData = new byte[60];
            pipe.Write(newData);

            // Buffer should have compacted and potentially grown
            Assert.True(pipe.Available == 80); // 20 + 60 bytes
        }

        [Fact]
        public void BufferGrowth_IncreasesCapacityCorrectly()
        {
            var pipe = new Pipe(10);
            var largeData = new byte[50];

            pipe.Write(largeData);

            Assert.True(pipe.Capacity >= 50);
            Assert.Equal(50, pipe.Available);
        }

        [Fact]
        public void MultipleOperations_MaintainDataIntegrity()
        {
            var pipe = new Pipe();
            var buffer = new byte[10];

            // Write some data
            pipe.Write(new byte[] { 1, 2, 3 });

            // Read part of it
            var read1 = pipe.Read(buffer.AsSpan(0, 2));
            Assert.Equal(2, read1);
            Assert.Equal(1, pipe.Available);

            // Write more data
            pipe.Write(new byte[] { 4, 5, 6 });
            Assert.Equal(4, pipe.Available);

            // Read remaining data
            var read2 = pipe.Read(buffer.AsSpan(2, 4));
            Assert.Equal(4, read2);
            Assert.Equal(0, pipe.Available);

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 0, 0, 0, 0 }, buffer);
        }

        [Fact]
        public void LargeDataOperations_HandleCorrectly()
        {
            var pipe = new Pipe();
            var largeData = new byte[100000];

            // Fill with pattern
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }

            pipe.Write(largeData);
            Assert.Equal(100000, pipe.Available);

            var readBuffer = new byte[100000];
            var bytesRead = pipe.Read(readBuffer);

            Assert.Equal(100000, bytesRead);
            Assert.Equal(largeData, readBuffer);
            Assert.Equal(0, pipe.Available);
        }

        [Fact]
        public void EmptyBuffer_Operations_HandleGracefully()
        {
            var pipe = new Pipe();

            Assert.Equal(0, pipe.Available);
            Assert.True(pipe.AvailableSpan.IsEmpty);
            Assert.True(pipe.AvailableMemory.IsEmpty);

            var buffer = new byte[10];
            Assert.Equal(0, pipe.Read(buffer));
            Assert.Equal(0, pipe.Peek(buffer));

            pipe.Consume(0); // Should not throw
            pipe.Clear(); // Should not throw
        }
    }
}