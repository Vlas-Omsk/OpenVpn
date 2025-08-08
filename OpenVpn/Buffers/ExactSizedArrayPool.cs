using System.Collections.Concurrent;

namespace OpenVpn.Buffers
{
    /// <summary>
    /// Provides an array pool that returns arrays of exactly the requested size,
    /// unlike .NET's ArrayPool which may return larger arrays.
    /// </summary>
    /// <typeparam name="T">The type of elements in the arrays.</typeparam>
    internal sealed class ExactSizedArrayPool<T> : IDisposable
    {
        private readonly ConcurrentDictionary<int, ConcurrentQueue<T[]>> _buckets;
        private readonly int _maxArraysPerBucket;
        private readonly ConcurrentDictionary<int, int> _bucketCounts;
        private volatile bool _disposed;

        /// <summary>
        /// Gets the shared instance.
        /// </summary>
        public static ExactSizedArrayPool<T> Shared { get; } = new ExactSizedArrayPool<T>();

        /// <summary>
        /// Initializes a new instance with default settings.
        /// </summary>
        public ExactSizedArrayPool() : this(maxArraysPerBucket: 50)
        {
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="maxArraysPerBucket">The maximum number of arrays to retain per size bucket.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when maxArraysPerBucket is less than 1.</exception>
        public ExactSizedArrayPool(int maxArraysPerBucket)
        {
            if (maxArraysPerBucket < 1)
                throw new ArgumentOutOfRangeException(nameof(maxArraysPerBucket), "Must be at least 1");

            _maxArraysPerBucket = maxArraysPerBucket;
            _buckets = new ConcurrentDictionary<int, ConcurrentQueue<T[]>>();
            _bucketCounts = new ConcurrentDictionary<int, int>();
        }

        /// <summary>
        /// Rents an array of exactly the specified size from the pool.
        /// </summary>
        /// <param name="size">The exact size of the array to rent.</param>
        /// <returns>An array of exactly the requested size.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when size is less than 0.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
        public T[] Rent(int size)
        {
            if (size < 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Size cannot be negative");

            ThrowIfDisposed();

            if (size == 0)
                return Array.Empty<T>();

            var bucket = _buckets.GetOrAdd(size, _ => new ConcurrentQueue<T[]>());

            if (bucket.TryDequeue(out T[]? array))
            {
                // Decrement the count for this bucket
                _bucketCounts.AddOrUpdate(size, 0, (_, count) => Math.Max(0, count - 1));
                return array;
            }

            // No array available in pool, create a new one
            return new T[size];
        }

        /// <summary>
        /// Returns an array to the pool for potential reuse.
        /// </summary>
        /// <param name="array">The array to return to the pool.</param>
        /// <param name="clearArray">Whether to clear the array contents before returning to pool.</param>
        /// <exception cref="ArgumentNullException">Thrown when array is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
        public void Return(T[] array, bool clearArray = false)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            ThrowIfDisposed();

            if (array.Length == 0)
                return; // Don't pool empty arrays

            var size = array.Length;
            var currentCount = _bucketCounts.GetOrAdd(size, 0);

            // Check if we've reached the maximum for this bucket
            if (currentCount >= _maxArraysPerBucket)
                return; // Don't add to pool, let GC handle it

            if (clearArray)
            {
                Array.Clear(array, 0, array.Length);
            }

            var bucket = _buckets.GetOrAdd(size, _ => new ConcurrentQueue<T[]>());
            bucket.Enqueue(array);

            // Increment the count for this bucket
            _bucketCounts.AddOrUpdate(size, 1, (_, count) => count + 1);
        }

        /// <summary>
        /// Clears all arrays from the pool.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
        public void Clear()
        {
            ThrowIfDisposed();

            foreach (var bucket in _buckets.Values)
            {
                while (bucket.TryDequeue(out _))
                {
                    // Remove all arrays from the bucket
                }
            }

            _bucketCounts.Clear();
        }

        /// <summary>
        /// Gets the number of arrays currently pooled for the specified size.
        /// </summary>
        /// <param name="size">The array size to check.</param>
        /// <returns>The number of arrays in the pool for the specified size.</returns>
        public int GetPooledCount(int size)
        {
            ThrowIfDisposed();
            return _bucketCounts.GetValueOrDefault(size, 0);
        }

        /// <summary>
        /// Gets all the sizes that currently have arrays in the pool.
        /// </summary>
        /// <returns>An enumerable of sizes that have pooled arrays.</returns>
        public IEnumerable<int> GetPooledSizes()
        {
            ThrowIfDisposed();
            return _bucketCounts.Keys;
        }

        /// <summary>
        /// Disposes the array pool and clears all pooled arrays.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Clear();
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ExactSizedArrayPool<T>));
        }
    }
}
