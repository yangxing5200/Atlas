// Providers/Hybrid/HybridCacheProvider.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;

namespace Atlas.Infrastructure.Caching.Providers.Hybrid
{
    public class HybridCacheProvider : ICacheProvider
    {
        private readonly ICacheProvider _l1Cache; // Memory
        private readonly ICacheProvider _l2Cache; // Redis/Distributed
        private readonly HybridCacheOptions _options;

        public HybridCacheProvider(
            ICacheProvider l1Cache,
            ICacheProvider l2Cache,
            HybridCacheOptions options)
        {
            _l1Cache = l1Cache ?? throw new ArgumentNullException(nameof(l1Cache));
            _l2Cache = l2Cache ?? throw new ArgumentNullException(nameof(l2Cache));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            // Try L1 first
            var value = await _l1Cache.GetAsync(key, cancellationToken);
            if (value != null)
            {
                return value;
            }

            // Try L2
            value = await _l2Cache.GetAsync(key, cancellationToken);
            if (value != null && _options.EnableL1Promotion)
            {
                // Promote to L1
                await _l1Cache.SetAsync(key, value, _options.L1Expiration, cancellationToken);
            }

            return value;
        }

        public async Task SetAsync(string key, byte[] value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            // Write to both levels
            var l1Task = _l1Cache.SetAsync(key, value, _options.L1Expiration ?? expiration, cancellationToken);
            var l2Task = _l2Cache.SetAsync(key, value, expiration, cancellationToken);

            await Task.WhenAll(l1Task, l2Task);
        }

        public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            var l1Task = _l1Cache.RemoveAsync(key, cancellationToken);
            var l2Task = _l2Cache.RemoveAsync(key, cancellationToken);

            var results = await Task.WhenAll(l1Task, l2Task);
            return results.Any(r => r);
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            // Check L1 first
            if (await _l1Cache.ExistsAsync(key, cancellationToken))
                return true;

            // Check L2
            return await _l2Cache.ExistsAsync(key, cancellationToken);
        }

        public async Task<IDictionary<string, byte[]?>> GetManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            var keyList = keys.ToList();
            var result = new Dictionary<string, byte[]?>();

            // Get from L1
            var l1Results = await _l1Cache.GetManyAsync(keyList, cancellationToken);
            var l1Misses = new List<string>();

            foreach (var key in keyList)
            {
                if (l1Results.TryGetValue(key, out var value) && value != null)
                {
                    result[key] = value;
                }
                else
                {
                    l1Misses.Add(key);
                }
            }

            // Get L1 misses from L2
            if (l1Misses.Any())
            {
                var l2Results = await _l2Cache.GetManyAsync(l1Misses, cancellationToken);

                foreach (var kvp in l2Results)
                {
                    result[kvp.Key] = kvp.Value;

                    // Promote to L1
                    if (kvp.Value != null && _options.EnableL1Promotion)
                    {
                        await _l1Cache.SetAsync(kvp.Key, kvp.Value, _options.L1Expiration, cancellationToken);
                    }
                }
            }

            return result;
        }

        public async Task SetManyAsync(IDictionary<string, byte[]> items, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            var l1Task = _l1Cache.SetManyAsync(items, _options.L1Expiration ?? expiration, cancellationToken);
            var l2Task = _l2Cache.SetManyAsync(items, expiration, cancellationToken);

            await Task.WhenAll(l1Task, l2Task);
        }

        public async Task<int> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            var l1Task = _l1Cache.RemoveManyAsync(keys, cancellationToken);
            var l2Task = _l2Cache.RemoveManyAsync(keys, cancellationToken);

            var results = await Task.WhenAll(l1Task, l2Task);
            return results.Max();
        }

        public async Task<IEnumerable<string>> GetKeysByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            // Pattern matching only on L2 (distributed cache)
            return await _l2Cache.GetKeysByPatternAsync(pattern, cancellationToken);
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await Task.WhenAll(
                _l1Cache.ClearAsync(cancellationToken),
                _l2Cache.ClearAsync(cancellationToken));
        }
    }

    public class HybridCacheOptions
    {
        public TimeSpan? L1Expiration { get; set; } = TimeSpan.FromMinutes(5);
        public bool EnableL1Promotion { get; set; } = true;
    }
}