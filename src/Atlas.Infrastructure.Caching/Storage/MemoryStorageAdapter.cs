using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Atlas.Infrastructure.Caching.Core;

namespace Atlas.Infrastructure.Caching.Storage;

/// <summary>
/// 内存存储适配器（L1）
/// </summary>
public class MemoryStorageAdapter : StorageAdapterBase
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryStorageAdapter> _logger;
    private readonly CacheOptions _options;

    public MemoryStorageAdapter(
        IMemoryCache cache,
        IOptions<CacheOptions> options,
        ILogger<MemoryStorageAdapter> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public override Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        key = NormalizeKey(key);
        var value = _cache.Get<T>(key);
        return Task.FromResult(value);
    }

    public override async Task<Dictionary<string, T>> GetManyAsync<T>(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default) where T : class
    {
        var result = new Dictionary<string, T>();

        foreach (var key in keys)
        {
            var value = await GetAsync<T>(key, cancellationToken);
            if (value != null)
            {
                result[key] = value;
            }
        }

        return result;
    }

    public override Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration,
        CancellationToken cancellationToken = default) where T : class
    {
        key = NormalizeKey(key);

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration,
            Size = EstimateSize(value)
        };

        _cache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public override async Task SetManyAsync<T>(
        Dictionary<string, T> items,
        TimeSpan expiration,
        CancellationToken cancellationToken = default) where T : class
    {
        foreach (var item in items)
        {
            await SetAsync(item.Key, item.Value, expiration, cancellationToken);
        }
    }

    public override Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        _cache.Remove(key);
        return Task.FromResult(true);
    }

    public override async Task<long> RemoveManyAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        long count = 0;
        foreach (var key in keys)
        {
            if (await RemoveAsync(key, cancellationToken))
                count++;
            if (key.EndsWith("*"))
            {
                await RemoveAsync(key.TrimEnd('*'), cancellationToken);
            }
        }
        return count;
    }

    public override Task<long> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // MemoryCache doesn't support pattern matching natively
        // This is a limitation - would need to track all keys separately
        _logger.LogWarning("Pattern-based removal not fully supported in MemoryCache");
        return Task.FromResult(0L);
    }

    public override Task ClearAsync(CancellationToken cancellationToken = default)
    {
        // MemoryCache doesn't have a Clear method
        // Would need to dispose and recreate, which is not ideal
        _logger.LogWarning("Clear not fully supported in MemoryCache");
        return Task.CompletedTask;
    }

    public override Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }

    private long EstimateSize<T>(T value)
    {
        // Simple size estimation - in production, use more sophisticated approach
        if (value is string str)
            return str.Length;

        return 1; // Default size unit
    }
}