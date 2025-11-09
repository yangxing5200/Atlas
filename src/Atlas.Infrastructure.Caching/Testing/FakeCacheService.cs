using System.Collections.Concurrent;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Keys;
using Atlas.Infrastructure.Caching.Metrics;

namespace Atlas.Infrastructure.Caching.Testing;

/// <summary>
/// 可验证的假缓存服务（用于集成测试）
/// </summary>
public class FakeCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly List<string> _getCalls = new();
    private readonly List<string> _setCalls = new();
    private readonly List<string> _removeCalls = new();

    public Task<T?> GetOrCreateAsync<T>(
        CacheKeyDefinition definition,
        Func<Task<T>> factory,
        object? instanceValue = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var key = $"{definition.Name}:{instanceValue}";
        _getCalls.Add(key);

        if (_cache.TryGetValue(key, out var cached))
            return Task.FromResult(cached as T);

        return factory();
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
        throw new NotImplementedException();
    }

    public Task SetAsync<T>(
        CacheKeyDefinition definition,
        T value,
        object? instanceValue = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var key = $"{definition.Name}:{instanceValue}";
        _setCalls.Add(key);
        _cache[key] = value!;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(
        CacheKeyDefinition definition,
        object? instanceValue = null,
        CancellationToken cancellationToken = default)
    {
        var key = $"{definition.Name}:{instanceValue}";
        _removeCalls.Add(key);
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();
        _getCalls.Clear();
        _setCalls.Clear();
        _removeCalls.Clear();
        return Task.CompletedTask;
    }

    public Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CacheStatistics());
    }

    // 验证方法
    public bool WasGetCalled(string key) => _getCalls.Contains(key);
    public bool WasSetCalled(string key) => _setCalls.Contains(key);
    public bool WasRemoveCalled(string key) => _removeCalls.Contains(key);
    public int GetCallCount => _getCalls.Count;
    public int SetCallCount => _setCalls.Count;
}