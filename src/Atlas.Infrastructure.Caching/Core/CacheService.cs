using Atlas.Infrastructure.Caching.Dependencies;
using Atlas.Infrastructure.Caching.Invalidation;
using Atlas.Infrastructure.Caching.Keys;
using Atlas.Infrastructure.Caching.Loading;
using Atlas.Infrastructure.Caching.Metrics;
using Atlas.Infrastructure.Caching.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Caching.Core;

public class CacheService : ICacheService, IAsyncCacheService, ISyncCacheService
{
    private readonly IStorageAdapter _storage;
    private readonly ICacheKeyBuilder _keyBuilder;
    private readonly LoadingCoordinator _loadingCoordinator;
    private readonly InvalidationCoordinator _invalidationCoordinator;
    private readonly MetricsCollector _metricsCollector;
    private readonly ILogger<CacheService> _logger;
    private readonly CacheOptions _options;

    public CacheService(
        IStorageAdapter storage,
        ICacheKeyBuilder keyBuilder,
        LoadingCoordinator loadingCoordinator,
        InvalidationCoordinator invalidationCoordinator,
        MetricsCollector metricsCollector,
        IOptions<CacheOptions> options,
        ILogger<CacheService> logger)
    {
        _storage = storage;
        _keyBuilder = keyBuilder;
        _loadingCoordinator = loadingCoordinator;
        _invalidationCoordinator = invalidationCoordinator;
        _metricsCollector = metricsCollector;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<T?> GetOrCreateAsync<T>(
        CacheKeyDefinition definition,
        Func<Task<T>> factory,
        object? instanceValue = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var keyInstance = _keyBuilder.Build(definition, instanceValue);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = await _loadingCoordinator.GetOrCreateAsync(
                keyInstance,
                factory,
                cancellationToken);

            stopwatch.Stop();
            _metricsCollector.RecordGet(keyInstance.UniqueKey, result != null, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to get or create cache for key {Key}", keyInstance.UniqueKey);
            _metricsCollector.RecordError(keyInstance.UniqueKey, "GetOrCreate");
            throw;
        }
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

    public async Task<Dictionary<string, T>> GetOrCreateManyAsync<T>(
        CacheKeyDefinition definition,
        IEnumerable<object> instanceValues,
        Func<IEnumerable<object>, Task<Dictionary<object, T>>> bulkFactory,
        CancellationToken cancellationToken = default) where T : class
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = await _loadingCoordinator.GetOrCreateManyAsync(
                definition,
                instanceValues,
                bulkFactory,
                cancellationToken);

            stopwatch.Stop();
            _metricsCollector.RecordBulkGet(definition.Name, result.Count, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to get or create many for key {KeyName}", definition.Name);
            _metricsCollector.RecordError(definition.Name, "GetOrCreateMany");
            throw;
        }
    }

    public async Task SetAsync<T>(
        CacheKeyDefinition definition,
        T value,
        object? instanceValue = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var keyInstance = _keyBuilder.Build(definition, instanceValue);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await _storage.SetAsync(keyInstance.UniqueKey, value, definition.DefaultExpiration, cancellationToken);
            
            stopwatch.Stop();
            _metricsCollector.RecordSet(keyInstance.UniqueKey, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to set cache for key {Key}", keyInstance.UniqueKey);
            _metricsCollector.RecordError(keyInstance.UniqueKey, "Set");
            throw;
        }
    }

    public void Set<T>(
        CacheKeyDefinition definition,
        T value,
        object? instanceValue = null) where T : class
    {
        SetAsync(definition, value, instanceValue).GetAwaiter().GetResult();
    }

    public async Task RemoveAsync(
        CacheKeyDefinition definition,
        object? instanceValue = null,
        CancellationToken cancellationToken = default)
    {
        var keyInstance = _keyBuilder.Build(definition, instanceValue);
        await _invalidationCoordinator.InvalidateAsync(new[] { keyInstance.UniqueKey }, cancellationToken);
    }

    public void Remove(
        CacheKeyDefinition definition,
        object? instanceValue = null)
    {
        RemoveAsync(definition, instanceValue).GetAwaiter().GetResult();
    }

    public async Task RemoveByPatternAsync(
        string pattern,
        CancellationToken cancellationToken = default)
    {
        await _invalidationCoordinator.InvalidateByPatternAsync(pattern, cancellationToken);
    }

    public void RemoveByPattern(string pattern)
    {
        RemoveByPatternAsync(pattern).GetAwaiter().GetResult();
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _storage.ClearAsync(cancellationToken);
        _logger.LogInformation("Cache cleared");
    }

    public async Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return await _metricsCollector.GetStatisticsAsync(cancellationToken);
    }
}