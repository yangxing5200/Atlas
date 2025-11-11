// Core/CacheService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.Core
{
    /// <summary>
    /// 缓存服务核心实现
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly ICacheProvider _provider;
        private readonly ICacheSerializer _serializer;
        private readonly ITagManager _tagManager;
        private readonly ICacheKeyGenerator _keyGenerator;
        private readonly IScopeContextAccessor _scopeAccessor;
        private readonly ICacheInvalidator _invalidator;
        private long _totalGets;
        private long _totalSets;
        private long _totalHits;
        private long _totalMisses;
        private long _totalInvalidations;

        public CacheService(
            ICacheProvider provider,
            ICacheSerializer serializer,
            ITagManager tagManager,
            ICacheKeyGenerator keyGenerator,
            IScopeContextAccessor scopeAccessor,
            ICacheInvalidator invalidator)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _tagManager = tagManager ?? throw new ArgumentNullException(nameof(tagManager));
            _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
            _scopeAccessor = scopeAccessor ?? throw new ArgumentNullException(nameof(scopeAccessor));
            _invalidator = invalidator ?? throw new ArgumentNullException(nameof(invalidator));
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _totalGets);

            var fullKey = GenerateScopedKey(key);
            var data = await _provider.GetAsync(fullKey, cancellationToken);

            if (data == null)
            {
                Interlocked.Increment(ref _totalMisses);
                return default;
            }

            // 反序列化包装的值
            var cachedValue = _serializer.Deserialize<CachedValue<T>>(data);
            if (cachedValue == null)
            {
                Interlocked.Increment(ref _totalMisses);
                return default;
            }

            // 检查 Tag 版本是否有效
            if (cachedValue.TagVersions.Any())
            {
                var currentVersions = await _tagManager.GetTagVersionsAsync(
                    cachedValue.TagVersions.Keys,
                    cancellationToken
                );

                // 如果任何 Tag 版本不匹配，认为缓存失效
                foreach (var kvp in cachedValue.TagVersions)
                {
                    if (!currentVersions.TryGetValue(kvp.Key, out var currentVersion) ||
                        currentVersion != kvp.Value)
                    {
                        // Tag 版本已改变，缓存失效
                        Interlocked.Increment(ref _totalMisses);

                        // 可选：删除过期的缓存
                        await _provider.RemoveAsync(fullKey, cancellationToken);

                        return default;
                    }
                }
            }

            Interlocked.Increment(ref _totalHits);
            return cachedValue.Value;
        }

        public async Task SetAsync<T>(
            string key,
            T value,
            CacheOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _totalSets);

            options ??= CacheOptions.Default;
            var fullKey = GenerateScopedKey(key, options.Scope);

            // 获取当前 Tag 版本
            Dictionary<string, long> tagVersions = new();
            if (options.Tags.Any())
            {
                var versions = await _tagManager.GetTagVersionsAsync(options.Tags, cancellationToken);
                tagVersions = versions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            // 包装值和 Tag 版本
            var cachedValue = new CachedValue<T>
            {
                Value = value,
                TagVersions = tagVersions,
                CachedAt = DateTime.UtcNow
            };

            var data = _serializer.Serialize(cachedValue);
            await _provider.SetAsync(fullKey, data, options.AbsoluteExpiration, cancellationToken);
        }

        public async Task<CacheResult<T>> GetOrSetAsync<T>(
            string key,
            Func<Task<T>> factory,
            CacheOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            var value = await GetAsync<T>(key, cancellationToken);

            if (value != null)
            {
                return CacheResult<T>.Hit(value, CacheSource.Cache, sw.ElapsedMilliseconds);
            }

            // 处理值类型的默认值情况
            if (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null)
            {
                // 引用类型或可空值类型，null 表示缓存未命中
                value = await factory();

                if (value != null)
                {
                    await SetAsync(key, value, options, cancellationToken);
                }

                return CacheResult<T>.Miss(value, CacheSource.Factory, sw.ElapsedMilliseconds);
            }
            else
            {
                // 值类型的特殊处理
                value = await factory();
                await SetAsync(key, value, options, cancellationToken);
                return CacheResult<T>.Miss(value, CacheSource.Factory, sw.ElapsedMilliseconds);
            }
        }

        public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            var fullKey = GenerateScopedKey(key);
            return await _provider.RemoveAsync(fullKey, cancellationToken);
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            var fullKey = GenerateScopedKey(key);
            return await _provider.ExistsAsync(fullKey, cancellationToken);
        }

        public async Task<IDictionary<string, T?>> GetManyAsync<T>(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
        {
            var keyList = keys.ToList();
            var fullKeys = new List<string>();
            foreach (var key in keyList)
            {
                fullKeys.Add(GenerateScopedKey(key));
            }

            var results = await _provider.GetManyAsync(fullKeys, cancellationToken);

            var output = new Dictionary<string, T?>();
            var keyArray = keyList.ToArray();
            var fullKeyArray = fullKeys.ToArray();

            for (int i = 0; i < keyArray.Length; i++)
            {
                var originalKey = keyArray[i];
                var fullKey = fullKeyArray[i];

                if (results.TryGetValue(fullKey, out var data) && data != null)
                {
                    var cachedValue = _serializer.Deserialize<CachedValue<T>>(data);
                    if (cachedValue != null)
                    {
                        // 检查 Tag 版本
                        bool isValid = true;
                        if (cachedValue.TagVersions.Any())
                        {
                            var currentVersions = await _tagManager.GetTagVersionsAsync(
                                cachedValue.TagVersions.Keys,
                                cancellationToken
                            );

                            foreach (var kvp in cachedValue.TagVersions)
                            {
                                if (!currentVersions.TryGetValue(kvp.Key, out var currentVersion) ||
                                    currentVersion != kvp.Value)
                                {
                                    isValid = false;
                                    break;
                                }
                            }
                        }

                        output[originalKey] = isValid ? cachedValue.Value : default;
                    }
                    else
                    {
                        output[originalKey] = default;
                    }
                }
                else
                {
                    output[originalKey] = default;
                }
            }

            return output;
        }

        // SetManyAsync 也需要修改
        public async Task SetManyAsync<T>(
            IDictionary<string, T> items,
            CacheOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= CacheOptions.Default;

            // 获取 Tag 版本
            Dictionary<string, long> tagVersions = new();
            if (options.Tags.Any())
            {
                var versions = await _tagManager.GetTagVersionsAsync(options.Tags, cancellationToken);
                tagVersions = versions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            var serializedItems = new Dictionary<string, byte[]>();
            foreach (var kvp in items)
            {
                var fullKey = GenerateScopedKey(kvp.Key, options.Scope);

                var cachedValue = new CachedValue<T>
                {
                    Value = kvp.Value,
                    TagVersions = tagVersions,
                    CachedAt = DateTime.UtcNow
                };

                var data = _serializer.Serialize(cachedValue);
                serializedItems[fullKey] = data;
            }

            await _provider.SetManyAsync(serializedItems, options.AbsoluteExpiration, cancellationToken);
        }

        public async Task<int> RemoveManyAsync(
            IEnumerable<string> keys,
            CancellationToken cancellationToken = default)
        {
            var fullKeys = new List<string>();
            foreach (var key in keys)
            {
                fullKeys.Add(GenerateScopedKey(key));
            }
            return await _provider.RemoveManyAsync(fullKeys, cancellationToken);
        }

        public async Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            await _invalidator.InvalidateByTagAsync(tag, cancellationToken);
            Interlocked.Increment(ref _totalInvalidations);
        }

        public async Task InvalidateByTagsAsync(
            IEnumerable<string> tags,
            CancellationToken cancellationToken = default)
        {
            await _invalidator.InvalidateByTagsAsync(tags, cancellationToken);
            var count = tags.Count();
            Interlocked.Add(ref _totalInvalidations, count);
        }

        public async Task InvalidateScopeAsync(CacheScope scope, CancellationToken cancellationToken = default)
        {
            await _invalidator.InvalidateByScopeAsync(scope, cancellationToken: cancellationToken);
        }

        public async Task InvalidateTenantAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            var pattern = $"T:{tenantId}:*";
            await _invalidator.InvalidateByPatternAsync(pattern, cancellationToken);
        }

        public async Task InvalidateStoreAsync(
            string tenantId,
            string storeId,
            CancellationToken cancellationToken = default)
        {
            var pattern = $"S:{tenantId}:{storeId}:*";
            await _invalidator.InvalidateByPatternAsync(pattern, cancellationToken);
        }

        public async Task InvalidateUserAsync(
            string tenantId,
            string userId,
            CancellationToken cancellationToken = default)
        {
            var pattern = $"U:{tenantId}:{userId}:*";
            await _invalidator.InvalidateByPatternAsync(pattern, cancellationToken);
        }

        public Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var statistics = new CacheStatistics
            {
                TotalGets = _totalGets,
                TotalSets = _totalSets,
                TotalHits = _totalHits,
                TotalMisses = _totalMisses,
                TotalInvalidations = _totalInvalidations
            };

            return Task.FromResult(statistics);
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await _provider.ClearAsync(cancellationToken);
        }

        private string GenerateScopedKey(string baseKey, CacheScope? scope = null)
        {
            var currentScope = scope ?? CacheScope.Global;
            var context = _scopeAccessor.Current;

            var scopeValues = new Dictionary<string, string>();

            if (currentScope >= CacheScope.Tenant && context?.TenantId != null)
            {
                scopeValues["TenantId"] = context.TenantId;
            }

            if (currentScope >= CacheScope.Store && context?.StoreId != null)
            {
                scopeValues["StoreId"] = context.StoreId;
            }

            if (currentScope == CacheScope.User && context?.UserId != null)
            {
                scopeValues["UserId"] = context.UserId;
            }

            return _keyGenerator.GenerateKey(baseKey, currentScope, scopeValues);
        }

        private async Task<string> AppendTagVersionsToKey(
            string key,
            ISet<string> tags,
            CancellationToken cancellationToken)
        {
            var versions = await _tagManager.GetTagVersionsAsync(tags, cancellationToken);
            var versionString = string.Join("_", versions.Values.OrderBy(v => v));
            return $"{key}:v{versionString}";
        }
    }
}