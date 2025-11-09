using Atlas.Infrastructure.Caching.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Caching.Storage;

/// <summary>
/// 混合存储适配器（L1 + L2）
/// </summary>
public class HybridStorageAdapter : StorageAdapterBase
{
    private readonly MemoryStorageAdapter _l1;
    private readonly RedisStorageAdapter _l2;
    private readonly ILogger<HybridStorageAdapter> _logger;
    private readonly CacheOptions _options;

    public HybridStorageAdapter(
        MemoryStorageAdapter l1,
        RedisStorageAdapter l2,
        IOptions<CacheOptions> options,
        ILogger<HybridStorageAdapter> logger)
    {
        _l1 = l1;
        _l2 = l2;
        _options = options.Value;
        _logger = logger;
    }

    public override async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        key = NormalizeKey(key);

        // Try L1 first
        var l1Value = await _l1.GetAsync<T>(key, cancellationToken);
        if (l1Value != null)
        {
            _logger.LogTrace("L1 cache hit: {Key}", key);
            return l1Value;
        }

        // Try L2
        var l2Value = await _l2.GetAsync<T>(key, cancellationToken);
        if (l2Value != null)
        {
            _logger.LogTrace("L2 cache hit: {Key}", key);

            // Backfill L1
            await _l1.SetAsync(key, l2Value, TimeSpan.FromMinutes(5), cancellationToken);
            return l2Value;
        }

        return null;
    }

    public override async Task<Dictionary<string, T>> GetManyAsync<T>(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default) where T : class
    {
        var keyList = keys.Select(NormalizeKey).ToList();
        var result = new Dictionary<string, T>();

        // Try L1 first
        var l1Results = await _l1.GetManyAsync<T>(keyList, cancellationToken);
        foreach (var item in l1Results)
        {
            result[item.Key] = item.Value;
        }

        // Find missing keys
        var missingKeys = keyList.Except(result.Keys).ToList();
        if (missingKeys.Count == 0)
            return result;

        // Try L2 for missing keys
        var l2Results = await _l2.GetManyAsync<T>(missingKeys, cancellationToken);
        foreach (var item in l2Results)
        {
            result[item.Key] = item.Value;

            // Backfill L1
            await _l1.SetAsync(item.Key, item.Value, TimeSpan.FromMinutes(5), cancellationToken);
        }

        return result;
    }

    public override async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration,
        CancellationToken cancellationToken = default) where T : class
    {
        key = NormalizeKey(key);

        // Write to both layers
        var l1Task = _l1.SetAsync(key, value, expiration, cancellationToken);
        var l2Task = _l2.SetAsync(key, value, expiration, cancellationToken);

        await Task.WhenAll(l1Task, l2Task);
    }

    public override async Task SetManyAsync<T>(
        Dictionary<string, T> items,
        TimeSpan expiration,
        CancellationToken cancellationToken = default) where T : class
    {
        var l1Task = _l1.SetManyAsync(items, expiration, cancellationToken);
        var l2Task = _l2.SetManyAsync(items, expiration, cancellationToken);

        await Task.WhenAll(l1Task, l2Task);
    }

    public override async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);

        var l1Task = _l1.RemoveAsync(key, cancellationToken);
        var l2Task = _l2.RemoveAsync(key, cancellationToken);

        await Task.WhenAll(l1Task, l2Task);
        return true;
    }

    public override async Task<long> RemoveManyAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var keyList = keys.Select(NormalizeKey).ToList();

        var l1Task = _l1.RemoveManyAsync(keyList, cancellationToken);
        var l2Task = _l2.RemoveManyAsync(keyList, cancellationToken);

        await Task.WhenAll(l1Task, l2Task);
        return keyList.Count;
    }

    public override async Task<long> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        var l1Task = _l1.RemoveByPatternAsync(pattern, cancellationToken);
        var l2Task = _l2.RemoveByPatternAsync(pattern, cancellationToken);

        var results = await Task.WhenAll(l1Task, l2Task);
        return results[1]; // Return L2 count
    }

    public override async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            _l1.ClearAsync(cancellationToken),
            _l2.ClearAsync(cancellationToken));
    }

    public override async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);

        // Check L1 first (faster)
        if (await _l1.ExistsAsync(key, cancellationToken))
            return true;

        return await _l2.ExistsAsync(key, cancellationToken);
    }
}