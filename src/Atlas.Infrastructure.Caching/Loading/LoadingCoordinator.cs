using System.Collections.Concurrent;
using Atlas.Infrastructure.Caching.Keys;
using Atlas.Infrastructure.Caching.Storage;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Loading;

/// <summary>
/// 加载协调器
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
    /// 获取或创建单个缓存
    /// </summary>
    public async Task<T?> GetOrCreateAsync<T>(
        CacheKeyInstance keyInstance,
        Func<Task<T>> factory,
        CancellationToken cancellationToken = default) where T : class
    {
        // 尝试从缓存获取
        var cached = await _storage.GetAsync<T>(keyInstance.UniqueKey, cancellationToken);
        if (cached != null)
            return cached;

        // 使用锁防止缓存击穿
        var lockKey = GetLockKey(keyInstance.UniqueKey);
        var semaphore = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check
            cached = await _storage.GetAsync<T>(keyInstance.UniqueKey, cancellationToken);
            if (cached != null)
                return cached;

            // 加载数据
            var value = await factory();
            
            if (value != null)
            {
                // 写入缓存
                await _storage.SetAsync(
                    keyInstance.UniqueKey,
                    value,
                    keyInstance.ActualExpiration,
                    cancellationToken);
            }
            else
            {
                // 缓存空值，防止缓存穿透
                await _storage.SetAsync(
                    keyInstance.UniqueKey,
                    CreateNullPlaceholder<T>(),
                    TimeSpan.FromMinutes(1),
                    cancellationToken);
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
    /// 批量获取或创建
    /// </summary>
    public async Task<Dictionary<string, T>> GetOrCreateManyAsync<T>(
        CacheKeyDefinition definition,
        IEnumerable<object> instanceValues,
        Func<IEnumerable<object>, Task<Dictionary<object, T>>> bulkFactory,
        CancellationToken cancellationToken = default) where T : class
    {
        var instanceList = instanceValues.ToList();
        var result = new Dictionary<string, T>();

        // 构建所有键实例
        var keyInstances = _keyBuilder.BuildMany(definition, instanceList).ToList();
        var keyMap = keyInstances.ToDictionary(k => k.UniqueKey, k => k);

        // 尝试从缓存批量获取
        var cached = await _storage.GetManyAsync<T>(keyMap.Keys, cancellationToken);
        foreach (var item in cached)
        {
            result[item.Key] = item.Value;
        }

        // 找出未命中的键
        var missingKeys = keyMap.Keys.Except(result.Keys).ToList();
        if (missingKeys.Count == 0)
            return result;

        // 提取未命中的实例值
        var missingInstanceValues = missingKeys
            .Select(k => keyMap[k].InstanceValue!)
            .ToList();

        // 批量加载
        var loaded = await bulkFactory(missingInstanceValues);

        // 批量写入缓存
        var toCache = new Dictionary<string, T>();
        foreach (var item in loaded)
        {
            var keyInstance = keyInstances.First(k => Equals(k.InstanceValue, item.Key));
            toCache[keyInstance.UniqueKey] = item.Value;
            result[keyInstance.UniqueKey] = item.Value;
        }

        if (toCache.Count > 0)
        {
            await _storage.SetManyAsync(
                toCache,
                definition.DefaultExpiration,
                cancellationToken);
        }

        return result;
    }

    private string GetLockKey(string cacheKey) => $"lock:{cacheKey}";

    private T CreateNullPlaceholder<T>() where T : class
    {
        // 创建一个特殊的空值标记
        // 实际实现可能需要更复杂的逻辑
        return default(T)!;
    }
}