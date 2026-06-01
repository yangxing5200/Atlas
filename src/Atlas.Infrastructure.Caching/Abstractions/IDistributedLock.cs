using System;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Caching.Abstractions
{
    /// <summary>
    /// Represents a distributed lock that can be released when no longer needed.
    /// </summary>
    public interface IDistributedLock : IAsyncDisposable
    {
        /// <summary>
        /// Gets a value indicating whether the lock is currently held.
        /// </summary>
        bool IsAcquired { get; }

        /// <summary>
        /// Gets the resource identifier this lock is protecting.
        /// </summary>
        string Resource { get; }

        /// <summary>
        /// Releases the lock asynchronously.
        /// </summary>
        Task ReleaseAsync();
    }

    /// <summary>
    /// Provides distributed locking capabilities for multi-instance deployments.
    /// </summary>
    public interface IDistributedLockProvider
    {
        /// <summary>
        /// Attempts to acquire a lock on the specified resource.
        /// Returns immediately if the lock cannot be acquired.
        /// </summary>
        /// <param name="resource">The resource identifier to lock.</param>
        /// <param name="expiry">The lock expiration time.</param>
        /// <param name="wait">Optional time to wait for the lock. If null, returns immediately if lock cannot be acquired.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The acquired lock, or null if the lock could not be acquired.</returns>
        Task<IDistributedLock?> TryAcquireAsync(
            string resource,
            TimeSpan expiry,
            TimeSpan? wait = null,
            CancellationToken ct = default);

        /// <summary>
        /// Acquires a lock on the specified resource, waiting until successful or timeout.
        /// </summary>
        /// <param name="resource">The resource identifier to lock.</param>
        /// <param name="expiry">The lock expiration time.</param>
        /// <param name="wait">Time to wait for the lock. If null, waits indefinitely.</param>
        /// <param name="retry">Interval between retry attempts. If null, uses a default interval.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The acquired lock.</returns>
        /// <exception cref="TimeoutException">Thrown if the lock cannot be acquired within the wait period.</exception>
        Task<IDistributedLock> AcquireAsync(
            string resource,
            TimeSpan expiry,
            TimeSpan? wait = null,
            TimeSpan? retry = null,
            CancellationToken ct = default);
    }
}
