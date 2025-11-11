// Providers/Redis/RedisTagVersionStore.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using Atlas.Infrastructure.Caching.Abstractions;

namespace Atlas.Infrastructure.Caching.Providers.Redis
{
    public class RedisTagVersionStore : ITagVersionStore
    {
        private readonly IConnectionMultiplexer _redis;
        private const string TagPrefix = "tag:v:";
        private IDatabase Database => _redis.GetDatabase();

        public RedisTagVersionStore(IConnectionMultiplexer redis)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        }

        public async Task<long> GetVersionAsync(string tag, CancellationToken cancellationToken = default)
        {
            var key = GetTagKey(tag);
            var value = await Database.StringGetAsync(key);

            if (value.HasValue && long.TryParse(value, out var version))
            {
                return version;
            }

            // Initialize tag version
            await Database.StringSetAsync(key, 1, flags: CommandFlags.FireAndForget);
            return 1;
        }

        public async Task<IDictionary<string, long>> GetVersionsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        {
            var tagArray = tags.ToArray();
            var keys = tagArray.Select(t => (RedisKey)GetTagKey(t)).ToArray();
            var values = await Database.StringGetAsync(keys);

            var result = new Dictionary<string, long>();
            for (int i = 0; i < tagArray.Length; i++)
            {
                if (values[i].HasValue && long.TryParse(values[i], out var version))
                {
                    result[tagArray[i]] = version;
                }
                else
                {
                    result[tagArray[i]] = 1;
                    await Database.StringSetAsync(GetTagKey(tagArray[i]), 1, flags: CommandFlags.FireAndForget);
                }
            }

            return result;
        }

        public async Task<long> IncrementVersionAsync(string tag, CancellationToken cancellationToken = default)
        {
            var key = GetTagKey(tag);
            return await Database.StringIncrementAsync(key);
        }

        public async Task IncrementVersionsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        {
            var batch = Database.CreateBatch();
            var tasks = tags.Select(tag => batch.StringIncrementAsync(GetTagKey(tag))).ToList();
            batch.Execute();
            await Task.WhenAll(tasks);
        }

        public async Task<IEnumerable<string>> GetAllTagsAsync(CancellationToken cancellationToken = default)
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{TagPrefix}*");

            return await Task.FromResult(keys.Select(k => k.ToString().Replace(TagPrefix, "")));
        }

        private string GetTagKey(string tag) => $"{TagPrefix}{tag}";
    }
}