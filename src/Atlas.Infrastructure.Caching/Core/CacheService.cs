using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using System.Diagnostics;

namespace Atlas.Infrastructure.Caching.Core
{
    /// <summary>
    /// 缓存服务实现
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly ICacheProvider _provider;
        private readonly ICacheSerializer _serializer;
        private readonly ITagManager _tagManager;
        private readonly ICacheKeyGenerator _keyGenerator;
        private readonly IScopeContextAccessor _scopeAccessor;
        private readonly ICacheInvalidator _invalidator;

        // 使用固定大小的锁池避免内存泄漏
        private readonly SemaphoreSlim[] _lockPool;
        private const int LockPoolSize = 1024;

        // 使用更精确的统计计数
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

            // 初始化锁池
            _lockPool = new SemaphoreSlim[LockPoolSize];
            for (int i = 0; i < LockPoolSize; i++)
            {
                _lockPool[i] = new SemaphoreSlim(1, 1);
            }
        }

        public async Task<T?> GetAsync<T>(
            CacheKeyDefinition definition,
            object? instanceValue = null,
            CancellationToken cancellationToken = default)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            Interlocked.Increment(ref _totalGets);

            var baseKey = definition.BuildKey(instanceValue);
            var fullKey = GenerateScopedKey(baseKey, definition.Scope);
            var data = await _provider.GetAsync(fullKey, cancellationToken);

            if (data == null)
            {
                Interlocked.Increment(ref _totalMisses);
                return default;
            }

            var cachedValue = _serializer.Deserialize<CachedValue<T>>(data);
            if (cachedValue == null)
            {
                Interlocked.Increment(ref _totalMisses);
                return default;
            }

            // 抽取为独立方法便于复用
            if (cachedValue.TagVersions.Any())
            {
                bool isValid = await ValidateTagVersionsAsync(cachedValue.TagVersions, cancellationToken);
                if (!isValid)
                {
                    Interlocked.Increment(ref _totalMisses);
                    // 异步删除失效缓存，不阻塞返回
                    _ = _provider.RemoveAsync(fullKey, cancellationToken);
                    return default;
                }
            }

            Interlocked.Increment(ref _totalHits);
            return cachedValue.Value;
        }

        public async Task SetAsync<T>(
            CacheKeyDefinition definition,
            T value,
            object? instanceValue = null,
            CacheOptions? optionsOverride = null,
            CancellationToken cancellationToken = default)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            // 明确处理null值
            if (!definition.AllowNull && value == null)
            {
                // 静默返回，保持原有行为
                return;
            }

            Interlocked.Increment(ref _totalSets);

            var baseKey = definition.BuildKey(instanceValue);
            var fullKey = GenerateScopedKey(baseKey, definition.Scope);
            var options = optionsOverride ?? definition.CreateOptions(_scopeAccessor.Current, instanceValue);

            // 获取当前Tag版本
            Dictionary<string, long> tagVersions = new();
            if (options.Tags.Any())
            {
                var versions = await _tagManager.GetTagVersionsAsync(options.Tags, cancellationToken);
                tagVersions = versions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            // 添加IsNull标记，用于区分null值和不存在
            var cachedValue = new CachedValue<T>
            {
                Value = value!,
                TagVersions = tagVersions,
                CachedAt = DateTime.UtcNow,
                IsNull = value == null
            };

            var data = _serializer.Serialize(cachedValue);
            await _provider.SetAsync(fullKey, data, options.AbsoluteExpiration, cancellationToken);
        }

        public async Task<CacheResult<T>> GetOrSetAsync<T>(
            CacheKeyDefinition definition,
            Func<Task<T>> factory,
            object? instanceValue = null,
            CacheOptions? optionsOverride = null,
            CancellationToken cancellationToken = default)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var sw = Stopwatch.StartNew();

            var (value, exists, isNull) = await GetWithExistsAsync<T>(definition, instanceValue, cancellationToken);

            if (exists)
            {
                // 如果允许null且值确实是null，也算命中
                if (isNull && definition.AllowNull)
                {
                    return CacheResult<T>.Hit(default, CacheSource.Cache, sw.ElapsedMilliseconds);
                }

                if (value != null)
                {
                    return CacheResult<T>.Hit(value, CacheSource.Cache, sw.ElapsedMilliseconds);
                }
            }

            var baseKey = definition.BuildKey(instanceValue);
            var lockKey = GenerateScopedKey(baseKey, definition.Scope);
            var semaphore = GetLockForKey(lockKey);

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // 双重检查：再次尝试读取缓存
                (value, exists, isNull) = await GetWithExistsAsync<T>(definition, instanceValue, cancellationToken);

                if (exists)
                {
                    if (isNull && definition.AllowNull)
                    {
                        return CacheResult<T>.Hit(default, CacheSource.Cache, sw.ElapsedMilliseconds);
                    }

                    if (value != null)
                    {
                        return CacheResult<T>.Hit(value, CacheSource.Cache, sw.ElapsedMilliseconds);
                    }
                }

                // 调用factory获取数据
                value = await factory();

                if (value != null || definition.AllowNull)
                {
                    await SetAsync(definition, value!, instanceValue, optionsOverride, cancellationToken);
                }

                return CacheResult<T>.Miss(value, CacheSource.Factory, sw.ElapsedMilliseconds);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<bool> RemoveAsync(
            CacheKeyDefinition definition,
            object? instanceValue = null,
            CancellationToken cancellationToken = default)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var baseKey = definition.BuildKey(instanceValue);
            var fullKey = GenerateScopedKey(baseKey, definition.Scope);
            return await _provider.RemoveAsync(fullKey, cancellationToken);
        }

        public async Task<bool> ExistsAsync(
            CacheKeyDefinition definition,
            object? instanceValue = null,
            CancellationToken cancellationToken = default)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var baseKey = definition.BuildKey(instanceValue);
            var fullKey = GenerateScopedKey(baseKey, definition.Scope);
            return await _provider.ExistsAsync(fullKey, cancellationToken);
        }

        public async Task<IDictionary<object, T?>> GetManyAsync<T>(
            CacheKeyDefinition definition,
            IEnumerable<object> instanceValues,
            CancellationToken cancellationToken = default)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var valueList = instanceValues.ToList();
            if (!valueList.Any())
                return new Dictionary<object, T?>();

            var keyMapping = new Dictionary<string, object>();

            foreach (var value in valueList)
            {
                var baseKey = definition.BuildKey(value);
                var fullKey = GenerateScopedKey(baseKey, definition.Scope);
                keyMapping[fullKey] = value;
            }

            // 批量获取所有缓存数据
            var results = await _provider.GetManyAsync(keyMapping.Keys, cancellationToken);

            // 批量收集和查询
            var allTags = new HashSet<string>();
            var deserializedCache = new Dictionary<string, CachedValue<T>?>();

            // 第一遍：反序列化并收集所有tags
            foreach (var kvp in results)
            {
                if (kvp.Value != null)
                {
                    var cachedValue = _serializer.Deserialize<CachedValue<T>>(kvp.Value);
                    deserializedCache[kvp.Key] = cachedValue;

                    if (cachedValue?.TagVersions != null)
                    {
                        foreach (var tag in cachedValue.TagVersions.Keys)
                        {
                            allTags.Add(tag);
                        }
                    }
                }
                else
                {
                    deserializedCache[kvp.Key] = null;
                }
            }

            // 一次性批量查询所有tag版本
            Dictionary<string, long> currentVersions = new();
            if (allTags.Any())
            {
                currentVersions = (await _tagManager.GetTagVersionsAsync(allTags, cancellationToken))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            // 第二遍：验证并构建结果
            var output = new Dictionary<object, T?>();
            var keysToRemove = new List<string>();

            foreach (var kvp in keyMapping)
            {
                var fullKey = kvp.Key;
                var instanceValue = kvp.Value;

                if (deserializedCache.TryGetValue(fullKey, out var cachedValue) && cachedValue != null)
                {
                    // 验证Tag版本
                    bool isValid = true;
                    if (cachedValue.TagVersions.Any())
                    {
                        foreach (var tagKvp in cachedValue.TagVersions)
                        {
                            if (!currentVersions.TryGetValue(tagKvp.Key, out var currentVersion) ||
                                currentVersion != tagKvp.Value)
                            {
                                isValid = false;
                                keysToRemove.Add(fullKey);
                                break;
                            }
                        }
                    }

                    output[instanceValue] = isValid ? cachedValue.Value : default;
                }
                else
                {
                    output[instanceValue] = default;
                }
            }

            // 异步批量删除失效的缓存
            if (keysToRemove.Any())
            {
                _ = _provider.RemoveManyAsync(keysToRemove, cancellationToken);
            }

            return output;
        }

        public async Task SetManyAsync<T>(
            CacheKeyDefinition definition,
            IDictionary<object, T> items,
            CacheOptions? optionsOverride = null,
            CancellationToken cancellationToken = default)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            if (!items.Any())
                return;

            var options = optionsOverride ?? definition.CreateOptions(_scopeAccessor.Current);

            // 获取Tag版本（一次性查询）
            Dictionary<string, long> tagVersions = new();
            if (options.Tags.Any())
            {
                var versions = await _tagManager.GetTagVersionsAsync(options.Tags, cancellationToken);
                tagVersions = versions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            var serializedItems = new Dictionary<string, byte[]>();
            foreach (var kvp in items)
            {
                // 跳过不允许null的情况
                if (!definition.AllowNull && kvp.Value == null)
                    continue;

                var baseKey = definition.BuildKey(kvp.Key);
                var fullKey = GenerateScopedKey(baseKey, definition.Scope);

                var cachedValue = new CachedValue<T>
                {
                    Value = kvp.Value,
                    TagVersions = tagVersions,
                    CachedAt = DateTime.UtcNow,
                    IsNull = kvp.Value == null
                };

                var data = _serializer.Serialize(cachedValue);
                serializedItems[fullKey] = data;
            }

            if (serializedItems.Any())
            {
                await _provider.SetManyAsync(serializedItems, options.AbsoluteExpiration, cancellationToken);
            }
        }

        public async Task<int> RemoveManyAsync(
            CacheKeyDefinition definition,
            IEnumerable<object> instanceValues,
            CancellationToken cancellationToken = default)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var fullKeys = new List<string>();
            foreach (var value in instanceValues)
            {
                var baseKey = definition.BuildKey(value);
                var fullKey = GenerateScopedKey(baseKey, definition.Scope);
                fullKeys.Add(fullKey);
            }

            if (!fullKeys.Any())
                return 0;

            return await _provider.RemoveManyAsync(fullKeys, cancellationToken);
        }

        public async Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            await _invalidator.InvalidateByTagAsync(tag, cancellationToken);
            Interlocked.Increment(ref _totalInvalidations);
        }

        public async Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        {
            var tagList = tags.ToList();
            if (!tagList.Any())
                return;

            await _invalidator.InvalidateByTagsAsync(tagList, cancellationToken);
            Interlocked.Add(ref _totalInvalidations, tagList.Count);
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

        public async Task InvalidateStoreAsync(string tenantId, string storeId, CancellationToken cancellationToken = default)
        {
            var pattern = $"S:{tenantId}:{storeId}:*";
            await _invalidator.InvalidateByPatternAsync(pattern, cancellationToken);
        }

        public async Task InvalidateUserAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
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

        // ========== 私有辅助方法 ==========

        private string GenerateScopedKey(string baseKey, CacheScope scope)
        {
            var context = _scopeAccessor.Current;
            var scopeValues = new Dictionary<string, string>();

            switch (scope)
            {
                case CacheScope.Tenant:
                    if (context?.TenantId != null)
                        scopeValues["TenantId"] = context.TenantId;
                    break;

                case CacheScope.Store:
                    if (context?.TenantId != null)
                        scopeValues["TenantId"] = context.TenantId;
                    if (context?.StoreId != null)
                        scopeValues["StoreId"] = context.StoreId;
                    break;

                case CacheScope.User:
                    if (context?.TenantId != null)
                        scopeValues["TenantId"] = context.TenantId;
                    if (context?.UserId != null)
                        scopeValues["UserId"] = context.UserId;
                    break;
            }

            return _keyGenerator.GenerateKey(baseKey, scope, scopeValues);
        }

        /// <summary>
        /// 使用锁池获取锁，避免内存泄漏
        /// </summary>
        private SemaphoreSlim GetLockForKey(string key)
        {
            var hash = Math.Abs(key.GetHashCode());
            return _lockPool[hash % LockPoolSize];
        }

        /// <summary>
        /// 批量验证Tag版本
        /// </summary>
        private async Task<bool> ValidateTagVersionsAsync(
            Dictionary<string, long> cachedTagVersions,
            CancellationToken cancellationToken)
        {
            if (!cachedTagVersions.Any())
                return true;

            var currentVersions = await _tagManager.GetTagVersionsAsync(
                cachedTagVersions.Keys,
                cancellationToken);

            foreach (var kvp in cachedTagVersions)
            {
                if (!currentVersions.TryGetValue(kvp.Key, out var currentVersion) ||
                    currentVersion != kvp.Value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 同时返回值和存在状态，避免双重查询
        /// </summary>
        private async Task<(T? value, bool exists, bool isNull)> GetWithExistsAsync<T>(
            CacheKeyDefinition definition,
            object? instanceValue,
            CancellationToken cancellationToken)
        {
            var baseKey = definition.BuildKey(instanceValue);
            var fullKey = GenerateScopedKey(baseKey, definition.Scope);
            var data = await _provider.GetAsync(fullKey, cancellationToken);

            if (data == null)
            {
                return (default, false, false);
            }

            var cachedValue = _serializer.Deserialize<CachedValue<T>>(data);
            if (cachedValue == null)
            {
                return (default, false, false);
            }

            // 验证Tag版本
            if (cachedValue.TagVersions.Any())
            {
                bool isValid = await ValidateTagVersionsAsync(cachedValue.TagVersions, cancellationToken);
                if (!isValid)
                {
                    // 异步删除失效缓存
                    _ = _provider.RemoveAsync(fullKey, cancellationToken);
                    return (default, false, false);
                }
            }

            return (cachedValue.Value, true, cachedValue.IsNull);
        }
    }
}