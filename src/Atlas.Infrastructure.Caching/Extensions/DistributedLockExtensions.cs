using System;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;

namespace Atlas.Infrastructure.Caching.Extensions
{
    /// <summary>
    /// Extension methods for IDistributedLockProvider to simplify common locking patterns.
    /// </summary>
    public static class DistributedLockExtensions
    {
        /// <summary>
        /// Default lock expiry time.
        /// </summary>
        private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Executes an action within a distributed lock.
        /// </summary>
        /// <typeparam name="T">The return type of the action.</typeparam>
        /// <param name="provider">The lock provider.</param>
        /// <param name="resource">The resource to lock.</param>
        /// <param name="action">The action to execute while holding the lock.</param>
        /// <param name="expiry">Optional lock expiry time. Defaults to 5 minutes.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The result of the action.</returns>
        /// <exception cref="TimeoutException">Thrown if the lock cannot be acquired.</exception>
        public static async Task<T> WithLockAsync<T>(
            this IDistributedLockProvider provider,
            string resource,
            Func<Task<T>> action,
            TimeSpan? expiry = null,
            CancellationToken ct = default)
        {
            await using var lockHandle = await provider.AcquireAsync(
                resource,
                expiry ?? DefaultExpiry,
                wait: TimeSpan.FromSeconds(30),
                ct: ct);

            return await action();
        }

        /// <summary>
        /// Executes an action within a distributed lock without returning a value.
        /// </summary>
        /// <param name="provider">The lock provider.</param>
        /// <param name="resource">The resource to lock.</param>
        /// <param name="action">The action to execute while holding the lock.</param>
        /// <param name="expiry">Optional lock expiry time. Defaults to 5 minutes.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <exception cref="TimeoutException">Thrown if the lock cannot be acquired.</exception>
        public static async Task WithLockAsync(
            this IDistributedLockProvider provider,
            string resource,
            Func<Task> action,
            TimeSpan? expiry = null,
            CancellationToken ct = default)
        {
            await using var lockHandle = await provider.AcquireAsync(
                resource,
                expiry ?? DefaultExpiry,
                wait: TimeSpan.FromSeconds(30),
                ct: ct);

            await action();
        }

        /// <summary>
        /// Tries to execute an action within a distributed lock.
        /// Returns default value if the lock cannot be acquired.
        /// </summary>
        /// <typeparam name="T">The return type of the action.</typeparam>
        /// <param name="provider">The lock provider.</param>
        /// <param name="resource">The resource to lock.</param>
        /// <param name="action">The action to execute while holding the lock.</param>
        /// <param name="expiry">Optional lock expiry time. Defaults to 5 minutes.</param>
        /// <param name="wait">Optional time to wait for the lock.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The result of the action, or default if the lock could not be acquired.</returns>
        public static async Task<T?> TryWithLockAsync<T>(
            this IDistributedLockProvider provider,
            string resource,
            Func<Task<T>> action,
            TimeSpan? expiry = null,
            TimeSpan? wait = null,
            CancellationToken ct = default)
        {
            var lockHandle = await provider.TryAcquireAsync(
                resource,
                expiry ?? DefaultExpiry,
                wait,
                ct);

            if (lockHandle == null)
            {
                return default;
            }

            await using (lockHandle)
            {
                return await action();
            }
        }

        /// <summary>
        /// Tries to execute an action within a distributed lock.
        /// Returns false if the lock cannot be acquired.
        /// </summary>
        /// <param name="provider">The lock provider.</param>
        /// <param name="resource">The resource to lock.</param>
        /// <param name="action">The action to execute while holding the lock.</param>
        /// <param name="expiry">Optional lock expiry time. Defaults to 5 minutes.</param>
        /// <param name="wait">Optional time to wait for the lock.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the action was executed, false if the lock could not be acquired.</returns>
        public static async Task<bool> TryWithLockAsync(
            this IDistributedLockProvider provider,
            string resource,
            Func<Task> action,
            TimeSpan? expiry = null,
            TimeSpan? wait = null,
            CancellationToken ct = default)
        {
            var lockHandle = await provider.TryAcquireAsync(
                resource,
                expiry ?? DefaultExpiry,
                wait,
                ct);

            if (lockHandle == null)
            {
                return false;
            }

            await using (lockHandle)
            {
                await action();
                return true;
            }
        }
    }
}
