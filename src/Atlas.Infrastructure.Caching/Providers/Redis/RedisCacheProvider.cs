// Providers/Redis/RedisCacheProvider.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using Atlas.Infrastructure.Caching.Abstractions;

namespace Atlas.Infrastructure.Caching.Providers.Redis
{
    public class RedisCacheProvider : ICacheProvider
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly string _instanceName;
        private IDatabase Database => _redis.GetDatabase();

        public RedisCacheProvider(IConnectionMultiplexer redis, string instanceName = "")
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _instanceName = instanceName ?? string.Empty;
        }

        public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            var value = await Database.StringGetAsync(GetFullKey(key));
            return value.HasValue ? (byte[]?)value : null;
        }

        public async Task SetAsync(string key, byte[] value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            await Database.StringSetAsync(GetFullKey(key), value, expiration);
        }

        public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            return await Database.KeyDeleteAsync(GetFullKey(key));
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return await Database.KeyExistsAsync(GetFullKey(key));
        }

        public async Task<IDictionary<string, byte[]?>> GetManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            var redisKeys = keys.Select(k => (RedisKey)GetFullKey(k)).ToArray();
            var values = await Database.StringGetAsync(redisKeys);

            var result = new Dictionary<string, byte[]?>();
            var keyArray = keys.ToArray();

            for (int i = 0; i < keyArray.Length; i++)
            {
                result[keyArray[i]] = values[i].HasValue ? (byte[]?)values[i] : null;
            }

            return result;
        }

        public async Task SetManyAsync(IDictionary<string, byte[]> items, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            var batch = Database.CreateBatch();
            var tasks = new List<Task>();

            foreach (var item in items)
            {
                tasks.Add(batch.StringSetAsync(GetFullKey(item.Key), item.Value, expiration));
            }

            batch.Execute();
            await Task.WhenAll(tasks);
        }

        public async Task<int> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            var redisKeys = keys.Select(k => (RedisKey)GetFullKey(k)).ToArray();
            return (int)await Database.KeyDeleteAsync(redisKeys);
        }

        public async Task<IEnumerable<string>> GetKeysByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: GetFullKey(pattern));

            return await Task.FromResult(keys.Select(k => StripInstanceName(k.ToString())));
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            await server.FlushDatabaseAsync();
        }

        private string GetFullKey(string key)
        {
            return string.IsNullOrEmpty(_instanceName) ? key : $"{_instanceName}:{key}";
        }

        private string StripInstanceName(string fullKey)
        {
            if (string.IsNullOrEmpty(_instanceName))
                return fullKey;

            var prefix = $"{_instanceName}:";
            return fullKey.StartsWith(prefix) ? fullKey.Substring(prefix.Length) : fullKey;
        }
    }
}