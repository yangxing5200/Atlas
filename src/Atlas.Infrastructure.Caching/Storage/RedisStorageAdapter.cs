using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Atlas.Infrastructure.Caching.Storage;

/// <summary>
/// Redis存储适配器（L2）
/// </summary>
public class RedisStorageAdapter : StorageAdapterBase
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ICacheSerializer _serializer;
    private readonly ILogger<RedisStorageAdapter> _logger;
    private readonly CacheOptions _options;
    private IDatabase Database => _redis.GetDatabase();

    public RedisStorageAdapter(
        IConnectionMultiplexer redis,
        ICacheSerializer serializer,
        IOptions<CacheOptions> options,
        ILogger<RedisStorageAdapter> logger)
    {
        _redis = redis;
        _serializer = serializer;
        _options = options.Value;
        _logger = logger;
    }

    public override async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        key = NormalizeKey(key);

        try
        {
            var value = await Database.StringGetAsync(key);
            if (!value.HasValue)
                return null;

            return _serializer.Deserialize<T>(value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get from Redis: {Key}", key);
            return null;
        }
    }

    public override async Task<Dictionary<string, T>> GetManyAsync<T>(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default) where T : class
    {
        var redisKeys = keys.Select(k => (RedisKey)NormalizeKey(k)).ToArray();
        var result = new Dictionary<string, T>();

        try
        {
            var values = await Database.StringGetAsync(redisKeys);

            for (int i = 0; i < redisKeys.Length; i++)
            {
                if (values[i].HasValue)
                {
                    var deserialized = _serializer.Deserialize<T>(values[i]!);
                    if (deserialized != null)
                    {
                        result[redisKeys[i].ToString()] = deserialized;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get many from Redis");
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

        try
        {
            var serialized = _serializer.Serialize(value);
            await Database.StringSetAsync(key, serialized, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set in Redis: {Key}", key);
            throw;
        }
    }

    public override async Task SetManyAsync<T>(
        Dictionary<string, T> items,
        TimeSpan expiration,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var batch = Database.CreateBatch();
            var tasks = new List<Task>();

            foreach (var item in items)
            {
                var key = NormalizeKey(item.Key);
                var serialized = _serializer.Serialize(item.Value);
                tasks.Add(batch.StringSetAsync(key, serialized, expiration));
            }

            batch.Execute();
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set many in Redis");
            throw;
        }
    }

    public override async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);

        try
        {
            return await Database.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove from Redis: {Key}", key);
            return false;
        }
    }

    public override async Task<long> RemoveManyAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var redisKeys = keys.Select(k => (RedisKey)NormalizeKey(k)).ToArray();

        try
        {
            return await Database.KeyDeleteAsync(redisKeys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove many from Redis");
            return 0;
        }
    }

    public override async Task<long> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoints = _redis.GetEndPoints();
            long totalDeleted = 0;

            foreach (var endpoint in endpoints)
            {
                var server = _redis.GetServer(endpoint);
                var keys = server.Keys(pattern: pattern).ToArray();

                if (keys.Length > 0)
                {
                    totalDeleted += await Database.KeyDeleteAsync(keys);
                }
            }

            return totalDeleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove by pattern from Redis: {Pattern}", pattern);
            return 0;
        }
    }

    public override async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoints = _redis.GetEndPoints();

            foreach (var endpoint in endpoints)
            {
                var server = _redis.GetServer(endpoint);
                await server.FlushDatabaseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear Redis");
            throw;
        }
    }

    public override async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);

        try
        {
            return await Database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence in Redis: {Key}", key);
            return false;
        }
    }
}