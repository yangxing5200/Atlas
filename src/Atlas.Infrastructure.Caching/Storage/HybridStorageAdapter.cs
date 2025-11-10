using Atlas.Infrastructure.Caching.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Caching.Storage;

/// <summary>
/// 混合存储适配器（L1 + L2）
/// </summary>
public class HybridStorageAdapter : StorageAdapterBase
{
    private readonly IStorageAdapter _l1Cache;  // Memory
    private readonly IStorageAdapter _l2Cache;  // Redis
    private readonly ILogger<HybridStorageAdapter> _logger;
    private readonly CacheOptions _options;

    public HybridStorageAdapter(
        MemoryStorageAdapter l1Cache,
        RedisStorageAdapter l2Cache,
        IOptions<CacheOptions> options,
        ILogger<HybridStorageAdapter> logger)
    {
        _l1Cache = l1Cache;
        _l2Cache = l2Cache;
        _options = options.Value;
        _logger = logger;
    }

    public override async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        // 先查 L1
        var l1Value = await _l1Cache.GetAsync<T>(key, cancellationToken);
        if (l1Value != null)
        {
            _logger.LogTrace("L1 cache hit: {Key}", key);
            return l1Value;
        }

        // 再查 L2
        var l2Value = await _l2Cache.GetAsync<T>(key, cancellationToken);
        if (l2Value != null)
        {
            _logger.LogTrace("L2 cache hit: {Key}", key);

            // 回写 L1（不带标签，因为从 L2 读取时无法获取原始标签）
            await _l1Cache.SetAsync(key, l2Value, TimeSpan.FromSeconds(_options.DefaultExpirationSeconds), cancellationToken);
            return l2Value;
        }

        _logger.LogTrace("Cache miss: {Key}", key);
        return null;
    }

    public override async Task<Dictionary<string, T>> GetManyAsync<T>(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default) where T : class
    {
        var keyList = keys.ToList();
        var result = new Dictionary<string, T>();

        // 从 L1 批量获取
        var l1Results = await _l1Cache.GetManyAsync<T>(keyList, cancellationToken);
        foreach (var kvp in l1Results)
        {
            result[kvp.Key] = kvp.Value;
        }

        // 找出 L1 未命中的键
        var missingKeys = keyList.Except(result.Keys).ToList();
        if (missingKeys.Count == 0)
            return result;

        // 从 L2 获取缺失的键
        var l2Results = await _l2Cache.GetManyAsync<T>(missingKeys, cancellationToken);
        foreach (var kvp in l2Results)
        {
            result[kvp.Key] = kvp.Value;

            // 回写 L1
            await _l1Cache.SetAsync(kvp.Key, kvp.Value, TimeSpan.FromSeconds(_options.DefaultExpirationSeconds), cancellationToken);
        }

        return result;
    }

    public override Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration,
        CancellationToken cancellationToken = default) where T : class
    {
        // 无标签版本：调用带标签版本
        return SetAsync(key, value, expiration, Array.Empty<string>(), cancellationToken);
    }

    // 实现带标签的设置
    public override async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default) where T : class
    {
        var tagList = tags.ToList();

        // 并行写入 L1 和 L2
        await Task.WhenAll(
            _l1Cache.SetAsync(key, value, expiration, tagList, cancellationToken),
            _l2Cache.SetAsync(key, value, expiration, tagList, cancellationToken)
        );

        _logger.LogTrace("Set cache with tags: {Key}, Tags: {Tags}",
            key, string.Join(", ", tagList));
    }

    public override async Task SetManyAsync<T>(
        Dictionary<string, T> items,
        TimeSpan expiration,
        CancellationToken cancellationToken = default) where T : class
    {
        await Task.WhenAll(
            _l1Cache.SetManyAsync(items, expiration, cancellationToken),
            _l2Cache.SetManyAsync(items, expiration, cancellationToken)
        );
    }

    public override async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            _l1Cache.RemoveAsync(key, cancellationToken),
            _l2Cache.RemoveAsync(key, cancellationToken)
        );

        return true;
    }

    public override async Task<long> RemoveManyAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var results = await Task.WhenAll(
            _l1Cache.RemoveManyAsync(keys, cancellationToken),
            _l2Cache.RemoveManyAsync(keys, cancellationToken)
        );

        // 返回 L2 的删除数量（更准确）
        return results[1];
    }

    public override async Task<long> RemoveByPatternAsync(
        string pattern,
        CancellationToken cancellationToken = default)
    {
        var results = await Task.WhenAll(
            _l1Cache.RemoveByPatternAsync(pattern, cancellationToken),
            _l2Cache.RemoveByPatternAsync(pattern, cancellationToken)
        );

        _logger.LogInformation("Removed by pattern: {Pattern}, L1: {L1Count}, L2: {L2Count}",
            pattern, results[0], results[1]);

        return results[1];
    }

    // 实现按标签失效
    public override async Task<long> RemoveByTagsAsync(
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        var tagList = tags.ToList();

        var results = await Task.WhenAll(
            _l1Cache.RemoveByTagsAsync(tagList, cancellationToken),
            _l2Cache.RemoveByTagsAsync(tagList, cancellationToken)
        );

        _logger.LogInformation("Removed by tags: {Tags}, L1: {L1Count}, L2: {L2Count}",
            string.Join(", ", tagList), results[0], results[1]);

        return results[1];
    }

    public override async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            _l1Cache.ClearAsync(cancellationToken),
            _l2Cache.ClearAsync(cancellationToken)
        );

        _logger.LogWarning("Cleared all cache layers");
    }

    public override async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        // 优先查 L1
        if (await _l1Cache.ExistsAsync(key, cancellationToken))
            return true;

        // 再查 L2
        return await _l2Cache.ExistsAsync(key, cancellationToken);
    }
}