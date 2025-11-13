// Core/CacheService.cs
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Caching.Core
{
    /// <summary>
    /// 缓存服务实现（强制使用 CacheKeyDefinition）
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly ICacheProvider _provider;
        private readonly ICacheSerializer _serializer;
        private readonly ITagManager _tagManager;
        private readonly ICacheKeyGenerator _keyGenerator;
        private readonly IScopeContextAccessor _scopeAccessor;
        private readonly ICacheInvalidator _invalidator;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

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
                    cancellationToken);

                foreach (var kvp in cachedValue.TagVersions)
                {
                    if (!currentVersions.TryGetValue(kvp.Key, out var currentVersion) ||
                        currentVersion != kvp.Value)
                    {
                        // Tag 版本已改变，缓存失效
                        Interlocked.Increment(ref _totalMisses);
                        await _provider.RemoveAsync(fullKey, cancellationToken);
                        return default;
                    }
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

            // 如果不允许 null 且值为 null，则不缓存
            if (!definition.AllowNull && value == null)
                return;

            Interlocked.Increment(ref _totalSets);

            var baseKey = definition.BuildKey(instanceValue);
            var fullKey = GenerateScopedKey(baseKey, definition.Scope);

            // 使用定义的选项，或使用覆盖的选项
            var options = optionsOverride ?? definition.CreateOptions(_scopeAccessor.Current, instanceValue);

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
                Value = value!,
                TagVersions = tagVersions,
                CachedAt = DateTime.UtcNow
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

            // 先尝试读取缓存
            var value = await GetAsync<T>(definition, instanceValue, cancellationToken);
            if (value != null || (definition.AllowNull && await ExistsAsync(definition, instanceValue, cancellationToken)))
            {
                return CacheResult<T>.Hit(value, CacheSource.Cache, sw.ElapsedMilliseconds);
            }

            // 生成锁的键
            var baseKey = definition.BuildKey(instanceValue);
            var lockKey = GenerateScopedKey(baseKey, definition.Scope);
            var semaphore = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // 双重检查：再次尝试读取缓存（可能已被其他线程写入）
                value = await GetAsync<T>(definition, instanceValue, cancellationToken);
                if (value != null || (definition.AllowNull && await ExistsAsync(definition, instanceValue, cancellationToken)))
                {
                    return CacheResult<T>.Hit(value, CacheSource.Cache, sw.ElapsedMilliseconds);
                }

                // 调用 factory 获取数据
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

                // 清理锁
                if (semaphore.CurrentCount == 1)
                {
                    _locks.TryRemove(lockKey, out _);
                }
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
            var keyMapping = new Dictionary<string, object>();

            foreach (var value in valueList)
            {
                var baseKey = definition.BuildKey(value);
                var fullKey = GenerateScopedKey(baseKey, definition.Scope);
                keyMapping[fullKey] = value;
            }

            var results = await _provider.GetManyAsync(keyMapping.Keys, cancellationToken);
            var output = new Dictionary<object, T?>();

            foreach (var kvp in keyMapping)
            {
                var fullKey = kvp.Key;
                var instanceValue = kvp.Value;

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
                                cancellationToken);

                            foreach (var tagKvp in cachedValue.TagVersions)
                            {
                                if (!currentVersions.TryGetValue(tagKvp.Key, out var currentVersion) ||
                                    currentVersion != tagKvp.Value)
                                {
                                    isValid = false;
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
                else
                {
                    output[instanceValue] = default;
                }
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

            var options = optionsOverride ?? definition.CreateOptions(_scopeAccessor.Current);

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
                var baseKey = definition.BuildKey(kvp.Key);
                var fullKey = GenerateScopedKey(baseKey, definition.Scope);

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

            return await _provider.RemoveManyAsync(fullKeys, cancellationToken);
        }

        public async Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            await _invalidator.InvalidateByTagAsync(tag, cancellationToken);
            Interlocked.Increment(ref _totalInvalidations);
        }

        public async Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
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
    }
}