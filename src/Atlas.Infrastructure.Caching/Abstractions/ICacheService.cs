using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.Abstractions
{
    /// <summary>
    /// 缓存服务主接口
    /// </summary>
    public interface ICacheService
    {
        // 基本操作
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
        Task<CacheResult<T>> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CacheOptions? options = null, CancellationToken cancellationToken = default);
        Task SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken cancellationToken = default);
        Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        // 批量操作
        Task<IDictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default);
        Task SetManyAsync<T>(IDictionary<string, T> items, CacheOptions? options = null, CancellationToken cancellationToken = default);
        Task<int> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

        // Tag操作
        Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default);
        Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);

        // Scope操作
        Task InvalidateScopeAsync(CacheScope scope, CancellationToken cancellationToken = default);
        Task InvalidateTenantAsync(string tenantId, CancellationToken cancellationToken = default);
        Task InvalidateStoreAsync(string tenantId, string storeId, CancellationToken cancellationToken = default);
        Task InvalidateUserAsync(string tenantId, string userId, CancellationToken cancellationToken = default);

        // 统计
        Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
        Task ClearAsync(CancellationToken cancellationToken = default);
    }
}