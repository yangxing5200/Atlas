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

    public override Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration,
        CancellationToken cancellationToken = default) where T : class
    {
        return SetAsync(key, value, expiration, Array.Empty<string>(), cancellationToken);
    }

    // 带标签的设置 (使用 Redis Sets)
    public override async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default) where T : class
    {
        key = NormalizeKey(key);
        var tagList = tags.ToList();

        try
        {
            var serialized = _serializer.Serialize(value);

            // 使用 Redis Transaction 确保原子性
            var transaction = Database.CreateTransaction();

            // 1. 设置缓存值
            _ = transaction.StringSetAsync(key, serialized, expiration);

            // 2. 为每个标签添加键到对应的 Set
            if (tagList.Any())
            {
                // 标签索引键：tag:tagName
                foreach (var tag in tagList)
                {
                    var tagKey = $"tag:{tag}";
                    _ = transaction.SetAddAsync(tagKey, key);

                    // 标签也设置过期时间（比缓存长一些，防止孤儿标签）
                    _ = transaction.KeyExpireAsync(tagKey, expiration.Add(TimeSpan.FromMinutes(5)));
                }

                // 存储键的标签列表（用于清理）
                var keyTagsKey = $"keytags:{key}";
                _ = transaction.SetAddAsync(keyTagsKey, tagList.Select(t => (RedisValue)t).ToArray());
                _ = transaction.KeyExpireAsync(keyTagsKey, expiration);
            }

            await transaction.ExecuteAsync();
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
            // 清理标签索引
            await CleanupTagIndexes(key);

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
            // 批量清理标签索引
            foreach (var key in redisKeys)
            {
                await CleanupTagIndexes(key.ToString());
            }

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
                    // 清理标签索引
                    foreach (var key in keys)
                    {
                        await CleanupTagIndexes(key.ToString());
                    }

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

    // 按标签失效
    public override async Task<long> RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        try
        {
            var keysToDelete = new HashSet<RedisKey>();

            // 从每个标签的 Set 中获取键
            foreach (var tag in tags)
            {
                var tagKey = $"tag:{tag}";
                var keys = await Database.SetMembersAsync(tagKey);

                foreach (var key in keys)
                {
                    if (!key.IsNullOrEmpty)
                    {
                        keysToDelete.Add(key.ToString()!);
                    }
                }

                // 删除标签 Set 本身
                await Database.KeyDeleteAsync(tagKey);
            }

            if (keysToDelete.Count == 0)
            {
                _logger.LogDebug("No keys found for tags: {Tags}", string.Join(", ", tags));
                return 0;
            }

            // 批量删除缓存键
            long deleted = await Database.KeyDeleteAsync(keysToDelete.ToArray());

            // 清理标签索引
            foreach (var key in keysToDelete)
            {
                await CleanupTagIndexes(key.ToString()!);
            }

            _logger.LogInformation("Removed {Count} keys by tags: {Tags}",
                deleted, string.Join(", ", tags));

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove by tags from Redis");
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

    // 清理标签索引
    private async Task CleanupTagIndexes(string key)
    {
        try
        {
            var keyTagsKey = $"keytags:{key}";
            var tags = await Database.SetMembersAsync(keyTagsKey);

            if (tags.Length > 0)
            {
                var transaction = Database.CreateTransaction();

                // 从每个标签的 Set 中移除这个键
                foreach (var tag in tags)
                {
                    if (!tag.IsNullOrEmpty)
                    {
                        var tagKey = $"tag:{tag}";
                        _ = transaction.SetRemoveAsync(tagKey, key);
                    }
                }

                // 删除键的标签列表
                _ = transaction.KeyDeleteAsync(keyTagsKey);

                await transaction.ExecuteAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup tag indexes for key: {Key}", key);
        }
    }
}