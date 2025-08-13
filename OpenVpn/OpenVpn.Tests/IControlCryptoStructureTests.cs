using OpenVpn.Control.Crypto;

namespace OpenVpn.Tests
{
    /// <summary>
    /// Comprehensive data structure tests for IControlCrypto interface following Write -> Send -> Check output structure pattern
    /// and Receive -> Read -> Check input structure validation pattern.
    /// </summary>
    public class IControlCryptoStructureTests
    {
        [Fact]
        public void Write_Send_CheckOutputStructure_PlainCrypto_PreservesDataIntegrity()
        {
            // Arrange
            using var crypto = new PlainCrypto();
            var testData = GenerateTestData(64);
            crypto.Connect();

            // Act - Write Input
            crypto.WriteInput(testData);

            // Check Output Structure - Should preserve data exactly
            var outputBuffer = new byte[testData.Length + 10]; // Extra space
            var bytesRead = crypto.ReadOutput(outputBuffer);

            // Assert
            Assert.Equal(testData.Length, bytesRead);
            Assert.Equal(testData, outputBuffer.AsSpan(0, bytesRead).ToArray());
        }

        [Fact]
        public void Receive_Read_CheckInputStructure_PlainCrypto_ValidatesInputFlow()
        {
            // Arrange
            using var crypto = new PlainCrypto();
            var testData = GenerateTestData(64);
            crypto.Connect();

            // Act - Write Output (simulating received data)
            crypto.WriteOutput(testData);

            // Check Input Structure - Should read data exactly as written
            var inputBuffer = new byte[testData.Length + 10]; // Extra space
            var bytesRead = crypto.ReadInput(inputBuffer);

            // Assert
            Assert.Equal(testData.Length, bytesRead);
            Assert.Equal(testData, inputBuffer.AsSpan(0, bytesRead).ToArray());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(16)]
        [InlineData(64)]
        [InlineData(256)]
        [InlineData(1024)]
        [InlineData(8192)]
        public void Write_Send_CheckBoundaryValues_HandlesVariousDataSizes(int dataSize)
        {
            // Arrange
            using var crypto = new PlainCrypto();
            var testData = GenerateTestData(dataSize);
            crypto.Connect();

            // Act
            crypto.WriteInput(testData);
            var outputBuffer = new byte[Math.Max(dataSize, 1) + 10];
            var bytesRead = crypto.ReadOutput(outputBuffer);

            // Assert boundary value handling
            Assert.Equal(dataSize, bytesRead);
            if (dataSize > 0)
            {
                Assert.Equal(testData, outputBuffer.AsSpan(0, bytesRead).ToArray());
            }
        }

        [Fact]
        public void Write_Send_CheckMultipleWrites_PreservesDataOrder()
        {
            // Arrange
            using var crypto = new PlainCrypto();
            var data1 = GenerateTestData(32);
            var data2 = GenerateTestData(48);
            var data3 = GenerateTestData(16);
            crypto.Connect();

            // Act - Multiple writes
            crypto.WriteInput(data1);
            crypto.WriteInput(data2);
            crypto.WriteInput(data3);

            // Check data order preservation
            var totalSize = data1.Length + data2.Length + data3.Length;
            var outputBuffer = new byte[totalSize + 10];
            var bytesRead = crypto.ReadOutput(outputBuffer);

            // Assert
            Assert.Equal(totalSize, bytesRead);
            
            var expectedData = new byte[totalSize];
            data1.CopyTo(expectedData, 0);
            data2.CopyTo(expectedData, data1.Length);
            data3.CopyTo(expectedData, data1.Length + data2.Length);
            
            Assert.Equal(expectedData, outputBuffer.AsSpan(0, bytesRead).ToArray());
        }

        [Fact]
        public void Write_Send_CheckPartialReads_HandlesBufferLimitations()
        {
            // Arrange
            using var crypto = new PlainCrypto();
            var testData = GenerateTestData(100);
            crypto.Connect();

            // Act - Write data
            crypto.WriteInput(testData);

            // Read in smaller chunks
            var outputBuffer = new byte[30]; // Smaller than input
            var totalBytesRead = 0;
            var readData = new List<byte>();

            while (totalBytesRead < testData.Length)
            {
                var bytesRead = crypto.ReadOutput(outputBuffer);
                if (bytesRead == 0) break;
                
                readData.AddRange(outputBuffer.AsSpan(0, bytesRead).ToArray());
                totalBytesRead += bytesRead;
            }

            // Assert
            Assert.Equal(testData.Length, totalBytesRead);
            Assert.Equal(testData, readData.ToArray());
        }

        [Fact]
        public void Receive_Read_CheckBidirectionalFlow_VerifiesInputOutputSeparation()
        {
            // Arrange
            using var crypto = new PlainCrypto();
            var inputData = GenerateTestData(64);
            var outputData = GenerateTestData(48);
            crypto.Connect();

            // Act - Bidirectional data flow
            crypto.WriteInput(inputData);  // WriteInput -> should be readable via ReadOutput
            crypto.WriteOutput(outputData); // WriteOutput -> should be readable via ReadInput

            // Check separation of input and output streams
            var readInputBuffer = new byte[outputData.Length + 10];
            var readOutputBuffer = new byte[inputData.Length + 10];

            var inputBytesRead = crypto.ReadInput(readInputBuffer);
            var outputBytesRead = crypto.ReadOutput(readOutputBuffer);

            // Assert correct flow: WriteInput->ReadOutput, WriteOutput->ReadInput
            Assert.Equal(inputData.Length, outputBytesRead); // Input data comes out as output
            Assert.Equal(outputData.Length, inputBytesRead); // Output data comes in as input
            Assert.Equal(inputData, readOutputBuffer.AsSpan(0, outputBytesRead).ToArray());
            Assert.Equal(outputData, readInputBuffer.AsSpan(0, inputBytesRead).ToArray());
        }

        [Fact]
        public void Write_Send_CheckPacketBoundaries_VerifiesDiscretePackets()
        {
            // Arrange
            using var crypto = new PlainCrypto();
            var packet1 = GenerateTestData(32);
            var packet2 = GenerateTestData(48);
            crypto.Connect();

            // Act - Write discrete packets
            crypto.WriteInput(packet1);
            
            // Read first packet completely
            var buffer1 = new byte[packet1.Length];
            var bytes1 = crypto.ReadOutput(buffer1);
            
            crypto.WriteInput(packet2);
            
            // Read second packet
            var buffer2 = new byte[packet2.Length];
            var bytes2 = crypto.ReadOutput(buffer2);

            // Assert packet boundaries are maintained
            Assert.Equal(packet1.Length, bytes1);
            Assert.Equal(packet2.Length, bytes2);
            Assert.Equal(packet1, buffer1);
            Assert.Equal(packet2, buffer2);
        }

        [Fact]
        public void Write_Send_CheckEmptyReads_HandlesNoDataAvailable()
        {
            // Arrange
            using var crypto = new PlainCrypto();
            crypto.Connect();

            // Act - Try to read when no data written
            var buffer = new byte[100];
            var bytesRead = crypto.ReadOutput(buffer);

            // Assert
            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public async Task Receive_Read_CheckConcurrentAccess_VerifiesThreadSafety()
        {
            // Arrange
            using var crypto = new PlainCrypto();
            crypto.Connect();
            var testData = GenerateTestData(1000);
            var results = new List<byte[]>();
            var lockObject = new object();

            // Act - Concurrent write and read operations
            var writeTask = Task.Run(async () =>
            {
                // Split data into chunks and write concurrently
                for (int i = 0; i < testData.Length; i += 10)
                {
                    var chunkSize = Math.Min(10, testData.Length - i);
                    var chunk = new byte[chunkSize];
                    Array.Copy(testData, i, chunk, 0, chunkSize);
                    crypto.WriteInput(chunk);
                    await Task.Delay(1); // Small delay to encourage interleaving
                }
            });

            var readTask = Task.Run(async () =>
            {
                var buffer = new byte[50];
                var totalRead = 0;
                
                while (totalRead < testData.Length)
                {
                    var bytesRead = crypto.ReadOutput(buffer);
                    if (bytesRead > 0)
                    {
                        lock (lockObject)
                        {
                            results.Add(buffer.AsSpan(0, bytesRead).ToArray());
                        }
                        totalRead += bytesRead;
                    }
                    else
                    {
                        await Task.Delay(1); // Brief wait if no data available
                    }
                }
            });

            // Wait for completion
            await Task.WhenAll(writeTask, readTask);

            // Assert data integrity
            var reconstructedData = results.SelectMany(chunk => chunk).ToArray();
            Assert.Equal(testData, reconstructedData);
        }

        [Theory]
        [InlineData(new byte[] { 0x00 })]
        [InlineData(new byte[] { 0xFF })]
        [InlineData(new byte[] { 0x00, 0xFF, 0x55, 0xAA })]
        public void Write_Send_CheckSpecialByteValues_HandlesEdgeCases(byte[] specialData)
        {
            // Arrange
            using var crypto = new PlainCrypto();
            crypto.Connect();

            // Act
            crypto.WriteInput(specialData);
            var outputBuffer = new byte[specialData.Length + 10];
            var bytesRead = crypto.ReadOutput(outputBuffer);

            // Assert special byte values are preserved
            Assert.Equal(specialData.Length, bytesRead);
            Assert.Equal(specialData, outputBuffer.AsSpan(0, bytesRead).ToArray());
        }

        [Fact]
        public void Dispose_CheckResourceCleanup_VerifiesProperDisposal()
        {
            // Arrange
            var crypto = new PlainCrypto();
            var testData = GenerateTestData(64);
            crypto.Connect();
            crypto.WriteInput(testData);

            // Act
            crypto.Dispose();

            // Verify disposal doesn't throw
            Assert.True(true, "Disposal completed without exception");
        }

        private static byte[] GenerateTestData(int length)
        {
            var data = new byte[length];
            var random = new Random(42); // Fixed seed for reproducible tests
            random.NextBytes(data);
            return data;
        }
    }
}