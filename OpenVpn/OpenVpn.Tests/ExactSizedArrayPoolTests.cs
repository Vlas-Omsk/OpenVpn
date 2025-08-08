using OpenVpn.Buffers;

namespace OpenVpn.Tests
{
    public class ExactSizedArrayPoolTests
    {
        [Fact]
        public void Rent_ReturnsArrayOfExactSize()
        {
            var pool = new ExactSizedArrayPool<int>();

            var array = pool.Rent(10);

            Assert.Equal(10, array.Length);
        }

        [Fact]
        public void RentZeroSize_ReturnsEmptyArray()
        {
            var pool = new ExactSizedArrayPool<int>();

            var array = pool.Rent(0);

            Assert.Same(Array.Empty<int>(), array);
        }

        [Fact]
        public void RentNegativeSize_ThrowsArgumentOutOfRangeException()
        {
            var pool = new ExactSizedArrayPool<int>();

            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Rent(-1));
        }

        [Fact]
        public void RentSameSize_ReusesArray()
        {
            var pool = new ExactSizedArrayPool<int>();

            var array1 = pool.Rent(10);

            pool.Return(array1);

            var array2 = pool.Rent(10);

            Assert.Same(array1, array2);
        }

        [Fact]
        public void ReturnNullArray_ThrowsArgumentNullException()
        {
            var pool = new ExactSizedArrayPool<int>();

            Assert.Throws<ArgumentNullException>(() => pool.Return(null!));
        }

        [Fact]
        public void ReturnWithClearArray_ClearsContents()
        {
            var pool = new ExactSizedArrayPool<int>();

            var array = pool.Rent(5);
            array[0] = 42;
            array[4] = 99;

            pool.Return(array, clearArray: true);

            var reusedArray = pool.Rent(5);

            Assert.All(reusedArray, item => Assert.Equal(0, item));
        }

        [Fact]
        public void GetPooledCount_ReturnsCorrectCount()
        {
            var pool = new ExactSizedArrayPool<int>();

            Assert.Equal(0, pool.GetPooledCount(10));

            var array1 = pool.Rent(10);
            var array2 = pool.Rent(10);
            pool.Return(array1);
            pool.Return(array2);

            Assert.Equal(2, pool.GetPooledCount(10));

            pool.Rent(10);
            Assert.Equal(1, pool.GetPooledCount(10));
        }

        [Fact]
        public void Clear_RemovesAllPooledArrays()
        {
            var pool = new ExactSizedArrayPool<int>();

            var array1 = pool.Rent(10);
            var array2 = pool.Rent(20);
            pool.Return(array1);
            pool.Return(array2);

            Assert.Equal(1, pool.GetPooledCount(10));
            Assert.Equal(1, pool.GetPooledCount(20));

            pool.Clear();

            Assert.Equal(0, pool.GetPooledCount(10));
            Assert.Equal(0, pool.GetPooledCount(20));
        }

        [Fact]
        public void MaxArraysPerBucket_LimitsPoolSize()
        {
            var pool = new ExactSizedArrayPool<int>(maxArraysPerBucket: 2);

            var array1 = pool.Rent(10);
            var array2 = pool.Rent(10);
            var array3 = pool.Rent(10);

            pool.Return(array1);
            pool.Return(array2);
            pool.Return(array3);

            Assert.Equal(2, pool.GetPooledCount(10));
        }

        [Fact]
        public async Task ConcurrentOperations_ShouldNotThrow()
        {
            var pool = new ExactSizedArrayPool<int>();
            const int operationsPerTask = 1000;
            const int numberOfTasks = 10;

            var tasks = new Task[numberOfTasks];

            for (int i = 0; i < numberOfTasks; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < operationsPerTask; j++)
                    {
                        var array = pool.Rent(10);
                        array[0] = j;
                        pool.Return(array);
                    }
                });
            }

            await Task.WhenAll(tasks);

            Assert.True(pool.GetPooledCount(10) >= 0);
        }

        [Fact]
        public void Dispose_PreventsSubsequentOperations()
        {
            var pool = new ExactSizedArrayPool<int>();

            pool.Dispose();

            Assert.Throws<ObjectDisposedException>(() => pool.Rent(10));
            Assert.Throws<ObjectDisposedException>(() => pool.Return(new int[10]));
            Assert.Throws<ObjectDisposedException>(() => pool.Clear());
            Assert.Throws<ObjectDisposedException>(() => pool.GetPooledCount(10));
        }

        [Fact]
        public void GetPooledSizes_ReturnsActiveSizes()
        {
            var pool = new ExactSizedArrayPool<int>();

            var array10 = pool.Rent(10);
            var array20 = pool.Rent(20);
            pool.Return(array10);
            pool.Return(array20);

            var sizes = pool.GetPooledSizes();
            Assert.Contains(10, sizes);
            Assert.Contains(20, sizes);
        }
    }
}
