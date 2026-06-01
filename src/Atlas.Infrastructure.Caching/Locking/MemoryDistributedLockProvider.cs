using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;

namespace Atlas.Infrastructure.Caching.Locking
{
    /// <summary>
    /// Represents a lock acquired through MemoryDistributedLockProvider.
    /// </summary>
    internal sealed class MemoryDistributedLock : IDistributedLock
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _lockStore;
        private readonly Timer? _expiryTimer;
        private int _released;

        public bool IsAcquired => _released == 0;
        public string Resource { get; }

        internal MemoryDistributedLock(
            string resource,
            SemaphoreSlim semaphore,
            ConcurrentDictionary<string, SemaphoreSlim> lockStore,
            TimeSpan expiry)
        {
            Resource = resource;
            _semaphore = semaphore;
            _lockStore = lockStore;

            // Set up automatic expiry
            if (expiry > TimeSpan.Zero)
            {
                _expiryTimer = new Timer(
                    _ => _ = ReleaseAsync(),
                    null,
                    expiry,
                    Timeout.InfiniteTimeSpan);
            }
        }

        public async Task ReleaseAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _expiryTimer?.Dispose();
                _semaphore.Release();
                
                // Note: We don't remove the semaphore from the store to avoid race conditions.
                // The semaphore will be reused for subsequent lock requests on the same resource.
                // This is a safe tradeoff as the memory overhead is minimal.
            }

            await Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await ReleaseAsync();
        }
    }

    /// <summary>
    /// In-memory distributed lock provider using SemaphoreSlim.
    /// Suitable for single-instance deployments or testing.
    /// </summary>
    /// <remarks>
    /// This implementation is NOT suitable for multi-instance deployments.
    /// For multi-instance scenarios, use a Redis-based implementation.
    /// </remarks>
    public class MemoryDistributedLockProvider : IDistributedLockProvider
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Attempts to acquire a lock on the specified resource.
        /// </summary>
        public async Task<IDistributedLock?> TryAcquireAsync(
            string resource,
            TimeSpan expiry,
            TimeSpan? wait = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(resource))
                throw new ArgumentException("Resource cannot be null or empty.", nameof(resource));

            if (expiry <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(expiry), "Expiry must be positive.");

            var semaphore = _locks.GetOrAdd(resource, _ => new SemaphoreSlim(1, 1));
            var waitTime = wait ?? TimeSpan.Zero;

            try
            {
                if (await semaphore.WaitAsync(waitTime, ct))
                {
                    return new MemoryDistributedLock(resource, semaphore, _locks, expiry);
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Acquires a lock on the specified resource, waiting until successful or timeout.
        /// </summary>
        public async Task<IDistributedLock> AcquireAsync(
            string resource,
            TimeSpan expiry,
            TimeSpan? wait = null,
            TimeSpan? retry = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(resource))
                throw new ArgumentException("Resource cannot be null or empty.", nameof(resource));

            if (expiry <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(expiry), "Expiry must be positive.");

            var retryInterval = retry ?? DefaultRetryInterval;
            var deadline = wait.HasValue ? DateTime.UtcNow.Add(wait.Value) : DateTime.MaxValue;

            while (!ct.IsCancellationRequested)
            {
                var lockResult = await TryAcquireAsync(resource, expiry, TimeSpan.Zero, ct);
                if (lockResult != null)
                {
                    return lockResult;
                }

                // Check if we've exceeded the wait time
                if (wait.HasValue && DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException($"Failed to acquire lock on resource '{resource}' within the specified wait period.");
                }

                // Wait before retrying
                await Task.Delay(retryInterval, ct);
            }

            ct.ThrowIfCancellationRequested();
            throw new OperationCanceledException(ct);
        }
    }
}
