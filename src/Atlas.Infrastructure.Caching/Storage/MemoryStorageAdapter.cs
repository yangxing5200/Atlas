using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Atlas.Infrastructure.Caching.Core;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Caching.Storage;

public class MemoryStorageAdapter : StorageAdapterBase
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryStorageAdapter> _logger;
    private readonly CacheOptions _options;

    // 键索引
    private readonly ConcurrentDictionary<string, byte> _keyIndex = new();

    // 标签系统：标签 -> 键集合
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _tagToKeys = new();

    // 标签系统：键 -> 标签集合
    private readonly ConcurrentDictionary<string, HashSet<string>> _keyToTags = new();

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
        // 无标签版本：调用带标签版本，传入空标签
        return SetAsync(key, value, expiration, Array.Empty<string>(), cancellationToken);
    }

    // 带标签的设置
    public override Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default) where T : class
    {
        key = NormalizeKey(key);
        var tagList = tags.ToList();

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration,
            Size = EstimateSize(value)
        };

        // 过期回调：清理索引
        options.RegisterPostEvictionCallback((k, v, reason, state) =>
        {
            if (k is string keyStr)
            {
                RemoveFromIndexes(keyStr);
            }
        });

        _cache.Set(key, value, options);

        // 更新键索引
        _keyIndex.TryAdd(key, 0);

        // 更新标签索引
        if (tagList.Any())
        {
            _keyToTags[key] = new HashSet<string>(tagList);

            foreach (var tag in tagList)
            {
                var keys = _tagToKeys.GetOrAdd(tag, _ => new ConcurrentBag<string>());
                keys.Add(key);
            }
        }

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
        RemoveFromIndexes(key);
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
        }
        return count;
    }

    public override Task<long> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        pattern = NormalizeKey(pattern);
        var regex = ConvertPatternToRegex(pattern);

        var matchedKeys = _keyIndex.Keys
            .Where(key => regex.IsMatch(key))
            .ToList();

        long count = 0;
        foreach (var key in matchedKeys)
        {
            _cache.Remove(key);
            RemoveFromIndexes(key);
            count++;
        }

        _logger.LogDebug("Removed {Count} keys matching pattern: {Pattern}", count, pattern);
        return Task.FromResult(count);
    }

    // 按标签失效
    public override Task<long> RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        var keysToRemove = new HashSet<string>();

        foreach (var tag in tags)
        {
            if (_tagToKeys.TryGetValue(tag, out var keys))
            {
                foreach (var key in keys)
                {
                    keysToRemove.Add(key);
                }
            }
        }

        long count = 0;
        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
            RemoveFromIndexes(key);
            count++;
        }

        _logger.LogInformation("Removed {Count} keys by tags: {Tags}",
            count, string.Join(", ", tags));

        return Task.FromResult(count);
    }

    public override Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var allKeys = _keyIndex.Keys.ToList();

        foreach (var key in allKeys)
        {
            _cache.Remove(key);
            RemoveFromIndexes(key);
        }

        _logger.LogInformation("Cleared {Count} keys from memory cache", allKeys.Count);
        return Task.CompletedTask;
    }

    public override Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }

    // 清理索引
    private void RemoveFromIndexes(string key)
    {
        _keyIndex.TryRemove(key, out _);

        // 清理标签索引
        if (_keyToTags.TryRemove(key, out var tags))
        {
            foreach (var tag in tags)
            {
                if (_tagToKeys.TryGetValue(tag, out var keys))
                {
                    // ConcurrentBag 不支持直接删除，重建集合
                    var newKeys = new ConcurrentBag<string>(keys.Where(k => k != key));
                    _tagToKeys.TryUpdate(tag, newKeys, keys);
                }
            }
        }
    }

    private Regex ConvertPatternToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".");

        return new Regex($"^{escaped}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    private long EstimateSize<T>(T value)
    {
        if (value is string str)
            return str.Length;

        return 1;
    }
}