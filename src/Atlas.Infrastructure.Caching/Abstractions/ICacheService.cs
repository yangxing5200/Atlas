// Abstractions/ICacheService.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.Abstractions
{
    /// <summary>
    /// 缓存服务接口（仅支持通过 CacheKeyDefinition 访问）
    /// </summary>
    public interface ICacheService
    {
        // ========== 基本操作（使用 Definition） ==========

        /// <summary>
        /// 获取缓存值
        /// </summary>
        Task<T?> GetAsync<T>(
            CacheKeyDefinition definition,
            object? instanceValue = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 设置缓存值
        /// </summary>
        Task SetAsync<T>(
            CacheKeyDefinition definition,
            T value,
            object? instanceValue = null,
            CacheOptions? optionsOverride = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取或设置缓存值
        /// </summary>
        Task<CacheResult<T>> GetOrSetAsync<T>(
            CacheKeyDefinition definition,
            Func<Task<T>> factory,
            object? instanceValue = null,
            CacheOptions? optionsOverride = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 移除缓存
        /// </summary>
        Task<bool> RemoveAsync(
            CacheKeyDefinition definition,
            object? instanceValue = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查缓存是否存在
        /// </summary>
        Task<bool> ExistsAsync(
            CacheKeyDefinition definition,
            object? instanceValue = null,
            CancellationToken cancellationToken = default);

        // ========== 批量操作 ==========

        /// <summary>
        /// 批量获取（使用相同的定义，不同的实例值）
        /// </summary>
        Task<IDictionary<object, T?>> GetManyAsync<T>(
            CacheKeyDefinition definition,
            IEnumerable<object> instanceValues,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量设置（使用相同的定义，不同的实例值）
        /// </summary>
        Task SetManyAsync<T>(
            CacheKeyDefinition definition,
            IDictionary<object, T> items,
            CacheOptions? optionsOverride = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量移除（使用相同的定义，不同的实例值）
        /// </summary>
        Task<int> RemoveManyAsync(
            CacheKeyDefinition definition,
            IEnumerable<object> instanceValues,
            CancellationToken cancellationToken = default);

        // ========== Tag 失效 ==========

        /// <summary>
        /// 按标签失效缓存
        /// </summary>
        Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default);

        /// <summary>
        /// 按多个标签失效缓存
        /// </summary>
        Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);

        // ========== Scope 失效 ==========

        /// <summary>
        /// 失效整个作用域的缓存
        /// </summary>
        Task InvalidateScopeAsync(CacheScope scope, CancellationToken cancellationToken = default);

        /// <summary>
        /// 失效指定租户的所有缓存
        /// </summary>
        Task InvalidateTenantAsync(string tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 失效指定门店的所有缓存
        /// </summary>
        Task InvalidateStoreAsync(string tenantId, string storeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 失效指定用户的所有缓存
        /// </summary>
        Task InvalidateUserAsync(string tenantId, string userId, CancellationToken cancellationToken = default);

        // ========== 统计和管理 ==========

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        Task ClearAsync(CancellationToken cancellationToken = default);
    }
}