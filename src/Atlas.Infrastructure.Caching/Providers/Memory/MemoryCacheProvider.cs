// Providers/Memory/MemoryCacheProvider.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Atlas.Infrastructure.Caching.Abstractions;

namespace Atlas.Infrastructure.Caching.Providers.Memory
{
    public class MemoryCacheProvider : ICacheProvider
    {
        private readonly IMemoryCache _cache;
        private readonly ConcurrentDictionary<string, byte> _keys = new();
        
        /// <summary>
        /// Cache for compiled Regex instances to improve pattern matching performance.
        /// Uses LRU-style eviction when capacity is exceeded.
        /// </summary>
        private readonly ConcurrentDictionary<string, Regex> _regexCache = new();
        
        /// <summary>
        /// Maximum number of cached Regex instances to prevent memory leaks.
        /// </summary>
        private const int MaxRegexCacheSize = 100;

        public MemoryCacheProvider(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            _cache.TryGetValue(key, out byte[]? value);
            return Task.FromResult(value);
        }

        public Task SetAsync(string key, byte[] value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            var options = new MemoryCacheEntryOptions();

            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration.Value;
            }

            options.RegisterPostEvictionCallback((k, v, r, s) =>
            {
                if (k != null)
                {
                    _keys.TryRemove(k.ToString()!, out _);
                }
            });

            _cache.Set(key, value, options);
            _keys.TryAdd(key, 0);

            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
            return Task.FromResult(true);
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_cache.TryGetValue(key, out _));
        }

        public Task<IDictionary<string, byte[]?>> GetManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<string, byte[]?>();
            foreach (var key in keys)
            {
                byte[]? value = null;
                if (_cache.TryGetValue(key, out value))
                {
                    result[key] = value;
                }
                else
                {
                    result[key] = null;
                }
            }

            return Task.FromResult<IDictionary<string, byte[]?>>(result);
        }

        public Task SetManyAsync(IDictionary<string, byte[]> items, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            foreach (var item in items)
            {
                SetAsync(item.Key, item.Value, expiration, cancellationToken).Wait();
            }
            return Task.CompletedTask;
        }

        public Task<int> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            var count = 0;
            foreach (var key in keys)
            {
                _cache.Remove(key);
                byte dummyValue;
                if (_keys.TryRemove(key, out dummyValue))
                    count++;
            }
            return Task.FromResult(count);
        }

        public Task<IEnumerable<string>> GetKeysByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            // Get or create compiled Regex from cache
            var regex = GetOrCreateCompiledRegex(pattern);
            
            var matchingKeys = _keys.Keys.Where(k => regex.IsMatch(k)).ToList();
            return Task.FromResult<IEnumerable<string>>(matchingKeys);
        }
        
        /// <summary>
        /// Gets a compiled Regex from cache or creates and caches a new one.
        /// Implements capacity limit to prevent memory leaks.
        /// </summary>
        private Regex GetOrCreateCompiledRegex(string pattern)
        {
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            
            if (_regexCache.TryGetValue(regexPattern, out var cachedRegex))
            {
                return cachedRegex;
            }
            
            // Check cache capacity and clear oldest entries if needed
            if (_regexCache.Count >= MaxRegexCacheSize)
            {
                // Simple eviction: clear half of the cache when full
                // This is a simple strategy; in production, consider LRU eviction
                var keysToRemove = _regexCache.Keys.Take(MaxRegexCacheSize / 2).ToList();
                foreach (var key in keysToRemove)
                {
                    _regexCache.TryRemove(key, out _);
                }
            }
            
            // Create compiled Regex for better performance
            var compiledRegex = new Regex(regexPattern, RegexOptions.Compiled);
            _regexCache.TryAdd(regexPattern, compiledRegex);
            
            return compiledRegex;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            foreach (var key in _keys.Keys.ToList())
            {
                _cache.Remove(key);
            }
            _keys.Clear();
            return Task.CompletedTask;
        }
    }
}