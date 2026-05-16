using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Atlas.Infrastructure.Caching.Core
{
    /// <summary>
    /// �������ʵ�֣�֧��ͬ�������ͷֲ�ʽ����ʧЧ֪ͨ��
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly ICacheProvider _provider;
        private readonly ICacheSerializer _serializer;
        private readonly ITagManager _tagManager;
        private readonly ICacheKeyGenerator _keyGenerator;
        private readonly IScopeContextAccessor _scopeAccessor;
        private readonly ICacheInvalidator _invalidator;
        private readonly ICacheInvalidationBus? _invalidationBus; // ��ѡ�ķֲ�ʽʧЧ����

        // ʹ�ù̶���С�����ر����ڴ�й©
        private readonly SemaphoreSlim[] _lockPool;
        private const int LockPoolSize = 1024;

        // ʹ�ø���ȷ��ͳ�Ƽ���
        private long _totalGets;
        private long _totalSets;
        private long _totalHits;
        private long _totalMisses;
        private long _totalInvalidations;

        // �����ڴ滺�棨����ͬ��������
        private readonly ConcurrentDictionary<string, object> _cache = new();
        private readonly ConcurrentDictionary<string, DateTime> _expirations = new();

        public CacheService(
            ICacheProvider provider,
            ICacheSerializer serializer,
            ITagManager tagManager,
            ICacheKeyGenerator keyGenerator,
            IScopeContextAccessor scopeAccessor,
            ICacheInvalidator invalidator,
            ICacheInvalidationBus? invalidationBus = null)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _tagManager = tagManager ?? throw new ArgumentNullException(nameof(tagManager));
            _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
            _scopeAccessor = scopeAccessor ?? throw new ArgumentNullException(nameof(scopeAccessor));
            _invalidator = invalidator ?? throw new ArgumentNullException(nameof(invalidator));
            _invalidationBus = invalidationBus;

            // ��ʼ������
            _lockPool = new SemaphoreSlim[LockPoolSize];
            for (int i = 0; i < LockPoolSize; i++)
            {
                _lockPool[i] = new SemaphoreSlim(1, 1);
            }

            // CacheService is scoped. Do not subscribe it to the singleton
            // invalidation bus, otherwise every request leaves a live handler.
            // L1 providers that own process-level state subscribe once instead.
        }

        // ================= ����ͬ���ӿ� =================

        public T? Get<T>(string key)
        {
            if (_cache.TryGetValue(key, out var value))
            {
                if (_expirations.TryGetValue(key, out var exp) && exp < DateTime.UtcNow)
                {
                    _cache.TryRemove(key, out _);
                    _expirations.TryRemove(key, out _);
                    return default;
                }

                return (T)value;
            }

            return default;
        }

        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            _cache[key] = value!;
            if (expiration.HasValue)
            {
                _expirations[key] = DateTime.UtcNow.Add(expiration.Value);
            }
            else
            {
                _expirations.TryRemove(key, out _);
            }
        }

        public bool Remove(string key)
        {
            var removed = _cache.TryRemove(key, out _);
            _expirations.TryRemove(key, out _);

            // ��������ʧЧ֪ͨ��������������Fire-and-forget��
            if (removed && _invalidationBus != null)
            {
                _ = _invalidationBus.PublishInvalidationAsync(key);
            }

            return removed;
        }

        public bool Exists(string key)
        {
            if (!_cache.ContainsKey(key))
                return false;

            // ����Ƿ����
            if (_expirations.TryGetValue(key, out var exp) && exp < DateTime.UtcNow)
            {
                _cache.TryRemove(key, out _);
                _expirations.TryRemove(key, out _);
                return false;
            }

            return true;
        }

        // ================= ���������� Definition ��ͬ������ =================

        public T? Get<T>(CacheKeyDefinition definition, object? instanceValue = null)
        {
            return GetAsync<T>(definition, instanceValue).GetAwaiter().GetResult();
        }

        public void Set<T>(
            CacheKeyDefinition definition,
            T value,
            object? instanceValue = null,
            CacheOptions? optionsOverride = null)
        {
            SetAsync(definition, value, instanceValue, optionsOverride).GetAwaiter().GetResult();
        }

        public CacheResult<T> GetOrSet<T>(
            CacheKeyDefinition definition,
            Func<T> factory,
            object? instanceValue = null,
            CacheOptions? optionsOverride = null)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            return GetOrSetAsync(
                    definition,
                    () => Task.FromResult(factory()),
                    instanceValue,
                    optionsOverride)
                .GetAwaiter()
                .GetResult();
        }

        public bool Remove(CacheKeyDefinition definition, object? instanceValue = null)
        {
            return RemoveAsync(definition, instanceValue).GetAwaiter().GetResult();
        }

        public bool Exists(CacheKeyDefinition definition, object? instanceValue = null)
        {
            return ExistsAsync(definition, instanceValue).GetAwaiter().GetResult();
        }

        // ================= �첽������ԭ��ʵ�֣����ֲ��䣩 =================

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

            // ��ȡΪ�����������ڸ���
            if (cachedValue.TagVersions.Any())
            {
                bool isValid = await ValidateTagVersionsAsync(cachedValue.TagVersions, cancellationToken);
                if (!isValid)
                {
                    Interlocked.Increment(ref _totalMisses);
                    // �첽ɾ��ʧЧ���棬����������
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

            // ��ȷ����nullֵ
            if (!definition.AllowNull && value == null)
            {
                // ��Ĭ���أ�����ԭ����Ϊ
                return;
            }

            Interlocked.Increment(ref _totalSets);

            var baseKey = definition.BuildKey(instanceValue);
            var fullKey = GenerateScopedKey(baseKey, definition.Scope);
            var options = optionsOverride ?? definition.CreateOptions(_scopeAccessor.Current, instanceValue);

            // ��ȡ��ǰTag�汾
            Dictionary<string, long> tagVersions = new();
            if (options.Tags.Any())
            {
                var versions = await _tagManager.GetTagVersionsAsync(options.Tags, cancellationToken);
                tagVersions = versions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            // ���IsNull��ǣ���������nullֵ�Ͳ�����
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
                // �������null��ֵȷʵ��null��Ҳ������
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
                // ˫�ؼ�飺�ٴγ��Զ�ȡ����
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

                // ����factory��ȡ����
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

            var result = await _provider.RemoveAsync(fullKey, cancellationToken);

            // ��������ʧЧ֪ͨ������������
            if (result && _invalidationBus != null)
            {
                await _invalidationBus.PublishInvalidationAsync(fullKey);
            }

            return result;
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

            // ������ȡ���л�������
            var results = await _provider.GetManyAsync(keyMapping.Keys, cancellationToken);

            // �����ռ��Ͳ�ѯ
            var allTags = new HashSet<string>();
            var deserializedCache = new Dictionary<string, CachedValue<T>?>();

            // ��һ�飺�����л����ռ�����tags
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
            }

            // ������֤����tag�汾��һ���Բ�ѯ��
            Dictionary<string, long> currentTagVersions = new();
            if (allTags.Any())
            {
                currentTagVersions = (await _tagManager.GetTagVersionsAsync(allTags, cancellationToken))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            // �ڶ��飺��֤�����ؽ��
            var output = new Dictionary<object, T?>();
            foreach (var kvp in keyMapping)
            {
                var fullKey = kvp.Key;
                var instanceValue = kvp.Value;

                if (!deserializedCache.TryGetValue(fullKey, out var cachedValue) || cachedValue == null)
                {
                    output[instanceValue] = default;
                    continue;
                }

                // ��֤tag�汾
                bool isValid = true;
                if (cachedValue.TagVersions.Any())
                {
                    foreach (var tagKvp in cachedValue.TagVersions)
                    {
                        if (!currentTagVersions.TryGetValue(tagKvp.Key, out var currentVersion) ||
                            currentVersion != tagKvp.Value)
                        {
                            isValid = false;
                            break;
                        }
                    }
                }

                output[instanceValue] = isValid ? cachedValue.Value : default;
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

            var options = optionsOverride ?? definition.CreateOptions(_scopeAccessor.Current, null);

            // ������ȡTag�汾������У�
            Dictionary<string, long> tagVersions = new();
            if (options.Tags.Any())
            {
                var versions = await _tagManager.GetTagVersionsAsync(options.Tags, cancellationToken);
                tagVersions = versions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            var serializedItems = new Dictionary<string, byte[]>();

            foreach (var kvp in items)
            {
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

            var count = await _provider.RemoveManyAsync(fullKeys, cancellationToken);

            // ������������ʧЧ֪ͨ
            if (count > 0 && _invalidationBus != null)
            {
                foreach (var key in fullKeys)
                {
                    await _invalidationBus.PublishInvalidationAsync(key);
                }
            }

            return count;
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
            // ��ձ����ڴ滺��
            _cache.Clear();
            _expirations.Clear();

            // ��շֲ�ʽ����
            await _provider.ClearAsync(cancellationToken);
        }

        // ========== ˽�и������� ==========

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
        /// ʹ�����ػ�ȡ���������ڴ�й©
        /// </summary>
        private SemaphoreSlim GetLockForKey(string key)
        {
            var hash = Math.Abs(key.GetHashCode());
            return _lockPool[hash % LockPoolSize];
        }

        /// <summary>
        /// ������֤Tag�汾
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
        /// ͬʱ����ֵ�ʹ���״̬������˫�ز�ѯ
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

            // ��֤Tag�汾
            if (cachedValue.TagVersions.Any())
            {
                bool isValid = await ValidateTagVersionsAsync(cachedValue.TagVersions, cancellationToken);
                if (!isValid)
                {
                    // �첽ɾ��ʧЧ����
                    _ = _provider.RemoveAsync(fullKey, cancellationToken);
                    return (default, false, false);
                }
            }

            return (cachedValue.Value, true, cachedValue.IsNull);
        }

    }
}
