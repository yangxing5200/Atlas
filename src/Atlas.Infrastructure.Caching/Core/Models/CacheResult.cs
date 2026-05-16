namespace Atlas.Infrastructure.Caching.Core.Models
{
    /// <summary>
    /// Result of a cache operation.
    /// </summary>
    public class CacheResult<T>
    {
        public T? Value { get; set; }
        public bool IsHit { get; set; }
        public CacheSource Source { get; set; }
        public long? LatencyMs { get; set; }

        public static CacheResult<T> Hit(T? value, CacheSource source = CacheSource.Cache, long? latencyMs = null)
        {
            return new CacheResult<T>
            {
                Value = value,
                IsHit = true,
                Source = source,
                LatencyMs = latencyMs
            };
        }

        public static CacheResult<T> Miss(T? value = default, CacheSource source = CacheSource.Factory, long? latencyMs = null)
        {
            return new CacheResult<T>
            {
                Value = value,
                IsHit = false,
                Source = source,
                LatencyMs = latencyMs
            };
        }
    }

    public enum CacheSource
    {
        Cache,
        Factory,
        L1Cache,
        L2Cache
    }
}
