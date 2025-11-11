// Utilities/RetryHelper.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Caching.Utilities
{
    public static class RetryHelper
    {
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            int maxRetries = 3,
            TimeSpan? delay = null,
            CancellationToken cancellationToken = default)
        {
            var retryDelay = delay ?? TimeSpan.FromMilliseconds(100);
            var attempts = 0;

            while (true)
            {
                try
                {
                    return await operation();
                }
                catch (Exception) when (attempts < maxRetries)
                {
                    attempts++;
                    await Task.Delay(retryDelay * attempts, cancellationToken);
                }
            }
        }

        public static async Task ExecuteWithRetryAsync(
            Func<Task> operation,
            int maxRetries = 3,
            TimeSpan? delay = null,
            CancellationToken cancellationToken = default)
        {
            var retryDelay = delay ?? TimeSpan.FromMilliseconds(100);
            var attempts = 0;

            while (true)
            {
                try
                {
                    await operation();
                    return;
                }
                catch (Exception) when (attempts < maxRetries)
                {
                    attempts++;
                    await Task.Delay(retryDelay * attempts, cancellationToken);
                }
            }
        }
    }
}