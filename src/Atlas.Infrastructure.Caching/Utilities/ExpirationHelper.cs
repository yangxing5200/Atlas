// Utilities/ExpirationHelper.cs
using System;

namespace Atlas.Infrastructure.Caching.Utilities
{
    public static class ExpirationHelper
    {
        public static TimeSpan CalculateExpiration(DateTime? expiresAt)
        {
            if (!expiresAt.HasValue)
                return TimeSpan.Zero;

            var timeUntilExpiration = expiresAt.Value - DateTime.UtcNow;
            return timeUntilExpiration > TimeSpan.Zero ? timeUntilExpiration : TimeSpan.Zero;
        }

        public static DateTime? CalculateExpirationTime(TimeSpan? absoluteExpiration, TimeSpan? slidingExpiration)
        {
            if (absoluteExpiration.HasValue)
            {
                return DateTime.UtcNow.Add(absoluteExpiration.Value);
            }

            if (slidingExpiration.HasValue)
            {
                return DateTime.UtcNow.Add(slidingExpiration.Value);
            }

            return null;
        }

        public static TimeSpan AddJitter(TimeSpan baseExpiration, double jitterFactor = 0.1)
        {
            var random = new Random();
            var jitter = baseExpiration.TotalMilliseconds * jitterFactor * (random.NextDouble() - 0.5);
            return baseExpiration.Add(TimeSpan.FromMilliseconds(jitter));
        }
    }
}