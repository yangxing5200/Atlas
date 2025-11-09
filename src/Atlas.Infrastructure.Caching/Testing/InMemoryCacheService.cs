using System.Collections.Concurrent;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Keys;
using Atlas.Infrastructure.Caching.Metrics;

namespace Atlas.Infrastructure.Caching.Testing;

/// <summary>
/// 纯内存缓存服务（用于单元测试）
/// </summary>
public class InMemoryCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly ICacheKeyBuilder _keyBuilder;

    public InMemoryCacheService(ICacheKeyBuilder keyBuilder)
    {
        _keyBuilder = keyBuilder;
    }

    public async Task<T?> GetOrCreateAsync<T>(
        CacheKeyDefinition definition,
        Func<Task<T>> factory,
        object? instanceValue = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var key = _keyBuilder.Build(definition, instanceValue).UniqueKey;

        if (_cache.TryGetValue(key, out var cached))
            return cached as T;

        var value = await factory();
        if (value != null)
            _cache[key] = value;

        return value;
    }

    public T? GetOrCreate<T>(
        CacheKeyDefinition definition,
        Func<T> factory,
        object? instanceValue = null) where T : class
    {
        return GetOrCreateAsync(definition, () => Task.FromResult(factory()), instanceValue)
            .GetAwaiter()
            .GetResult();
    }

    public Task<Dictionary<string, T>> GetOrCreateManyAsync<T>(
        CacheKeyDefinition definition,
        IEnumerable<object> instanceValues,
        Func<IEnumerable<object>, Task<Dictionary<object, T>>> bulkFactory,
        CancellationToken cancellationToken = default) where T : class
    {
        throw new NotImplementedException("Use GetOrCreateAsync in tests");
    }

    public Task SetAsync<T>(
        CacheKeyDefinition definition,
        T value,
        object? instanceValue = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var key = _keyBuilder.Build(definition, instanceValue).UniqueKey;
        _cache[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(
        CacheKeyDefinition definition,
        object? instanceValue = null,
        CancellationToken cancellationToken = default)
    {
        var key = _keyBuilder.Build(definition, instanceValue).UniqueKey;
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _cache.Keys.Where(k => MatchPattern(k, pattern)).ToList();
        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();
        return Task.CompletedTask;
    }

    public Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CacheStatistics());
    }

    private bool MatchPattern(string key, string pattern)
    {
        return key.StartsWith(pattern.Replace("*", ""));
    }
}