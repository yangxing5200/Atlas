using System.Collections.Concurrent;
using Atlas.Infrastructure.Caching.Keys;
using Atlas.Infrastructure.Caching.Storage;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Loading;

/// <summary>
/// 缓存加载协调器，提供防击穿和批量加载能力
/// </summary>
public class LoadingCoordinator
{
    private readonly IStorageAdapter _storage;
    private readonly ICacheKeyBuilder _keyBuilder;
    private readonly ILogger<LoadingCoordinator> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public LoadingCoordinator(
        IStorageAdapter storage,
        ICacheKeyBuilder keyBuilder,
        ILogger<LoadingCoordinator> logger)
    {
        _storage = storage;
        _keyBuilder = keyBuilder;
        _logger = logger;
    }

    /// <summary>
    /// 获取或创建缓存，使用双重检查锁防止击穿
    /// </summary>
    public async Task<T?> GetOrCreateAsync<T>(
        CacheKeyInstance keyInstance,
        Func<Task<T>> factory,
        CancellationToken cancellationToken = default) where T : class
    {
        var cached = await _storage.GetAsync<T>(keyInstance.UniqueKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogTrace("Cache hit for key: {Key}", keyInstance.UniqueKey);
            return cached;
        }

        _logger.LogTrace("Cache miss for key: {Key}, acquiring lock", keyInstance.UniqueKey);

        var lockKey = GetLockKey(keyInstance.UniqueKey);
        var semaphore = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check pattern
            cached = await _storage.GetAsync<T>(keyInstance.UniqueKey, cancellationToken);
            if (cached != null)
            {
                _logger.LogTrace("Cache hit on double-check for key: {Key}", keyInstance.UniqueKey);
                return cached;
            }

            _logger.LogDebug("Loading data for cache key: {Key}", keyInstance.UniqueKey);

            var value = await factory();

            if (value != null)
            {
                // 有依赖关系才使用标签索引
                if (keyInstance.Tags.Count > 0)
                {
                    await _storage.SetAsync(
                        keyInstance.UniqueKey,
                        value,
                        keyInstance.ActualExpiration,
                        keyInstance.Tags,
                        cancellationToken);

                    _logger.LogInformation(
                        "Cached key: {Key} with {TagCount} tags: {Tags}",
                        keyInstance.UniqueKey,
                        keyInstance.Tags.Count,
                        string.Join(", ", keyInstance.Tags));
                }
                else
                {
                    await _storage.SetAsync(
                        keyInstance.UniqueKey,
                        value,
                        keyInstance.ActualExpiration,
                        cancellationToken);

                    _logger.LogDebug("Cached key (no tags): {Key}", keyInstance.UniqueKey);
                }
            }
            else
            {
                // 缓存空值防止穿透
                await _storage.SetAsync(
                    keyInstance.UniqueKey,
                    CreateNullPlaceholder<T>(),
                    TimeSpan.FromMinutes(1),
                    cancellationToken);

                _logger.LogDebug("Cached null placeholder for key: {Key}", keyInstance.UniqueKey);
            }

            return value;
        }
        finally
        {
            semaphore.Release();
            _locks.TryRemove(lockKey, out _);
        }
    }

    /// <summary>
    /// 批量获取或创建缓存，根据是否有标签选择存储策略
    /// </summary>
    public async Task<Dictionary<string, T>> GetOrCreateManyAsync<T>(
        CacheKeyDefinition definition,
        IEnumerable<object> instanceValues,
        Func<IEnumerable<object>, Task<Dictionary<object, T>>> bulkFactory,
        CancellationToken cancellationToken = default) where T : class
    {
        var instanceList = instanceValues.ToList();
        var result = new Dictionary<string, T>();

        var keyInstances = _keyBuilder.BuildMany(definition, instanceList).ToList();
        var keyMap = keyInstances.ToDictionary(k => k.UniqueKey, k => k);

        _logger.LogDebug("Batch loading {Count} cache keys", keyInstances.Count);

        var cached = await _storage.GetManyAsync<T>(keyMap.Keys, cancellationToken);
        foreach (var item in cached)
        {
            result[item.Key] = item.Value;
        }

        _logger.LogDebug("Cache hit for {HitCount}/{TotalCount} keys",
            cached.Count, keyInstances.Count);

        var missingKeys = keyMap.Keys.Except(result.Keys).ToList();
        if (missingKeys.Count == 0)
            return result;

        _logger.LogDebug("Loading {Count} missing keys from source", missingKeys.Count);

        var missingInstanceValues = missingKeys
            .Select(k => keyMap[k].InstanceValue!)
            .ToList();

        var loaded = await bulkFactory(missingInstanceValues);

        var hasAnyTags = keyInstances.Any(k => k.Tags.Count > 0);

        if (hasAnyTags)
        {
            // 有标签：单独设置以维护标签索引
            foreach (var item in loaded)
            {
                var keyInstance = keyInstances.First(k => Equals(k.InstanceValue, item.Key));

                await _storage.SetAsync(
                    keyInstance.UniqueKey,
                    item.Value,
                    keyInstance.ActualExpiration,
                    keyInstance.Tags,
                    cancellationToken);

                result[keyInstance.UniqueKey] = item.Value;

                if (keyInstance.Tags.Count > 0)
                {
                    _logger.LogTrace("Cached key: {Key} with tags: {Tags}",
                        keyInstance.UniqueKey,
                        string.Join(", ", keyInstance.Tags));
                }
            }

            _logger.LogInformation("Successfully cached {Count} items with tags", loaded.Count);
        }
        else
        {
            // 无标签：批量设置提高性能
            var itemsToCache = new Dictionary<string, T>();
            foreach (var item in loaded)
            {
                var keyInstance = keyInstances.First(k => Equals(k.InstanceValue, item.Key));
                itemsToCache[keyInstance.UniqueKey] = item.Value;
                result[keyInstance.UniqueKey] = item.Value;
            }

            if (itemsToCache.Count > 0)
            {
                await _storage.SetManyAsync(
                    itemsToCache,
                    keyInstances.First().ActualExpiration,
                    cancellationToken);

                _logger.LogInformation("Successfully cached {Count} items (no tags)", loaded.Count);
            }
        }

        return result;
    }

    private string GetLockKey(string cacheKey) => $"lock:{cacheKey}";

    private T CreateNullPlaceholder<T>() where T : class => default(T)!;
}