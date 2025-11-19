using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Core.Entities;
using Atlas.Core.Services;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// 门店范围过滤辅助类 - 封装共享的过滤逻辑
    /// </summary>
    internal static class EntityScopeFilter<TEntity>
        where TEntity : class
    {
        // 静态缓存：避免重复反射
        private static readonly bool _isStoreOnlyEntity = typeof(IStoreOnlyEntity).IsAssignableFrom(typeof(TEntity));
        private static readonly bool _isSharedEntity = typeof(ISharedEntity).IsAssignableFrom(typeof(TEntity));
        private static readonly bool _isTenantEntity = typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity));
        private static readonly bool _isStoreScopedEntity = _isStoreOnlyEntity || _isSharedEntity;

        public static bool IsStoreOnlyEntity => _isStoreOnlyEntity;
        public static bool IsSharedEntity => _isSharedEntity;
        public static bool IsTenantEntity => _isTenantEntity;
        public static bool IsStoreScopedEntity => _isStoreScopedEntity;

        /// <summary>
        /// 异步应用门店范围过滤
        /// </summary>
        public static async Task<IQueryable<TEntity>> ApplyAsync(
            IQueryable<TEntity> query,
            ICurrentIdentity currentIdentity,
            List<long>? cachedShareStoreIds = null)
        {
            // 租户过滤
            if (_isTenantEntity)
            {
                if (!currentIdentity.TenantId.HasValue)
                {
                    return query.Where(_ => false);
                }

                var currentTenantId = currentIdentity.TenantId.Value;
                query = ((IQueryable<ITenantEntity>)query)
                    .Where(e => e.TenantId == currentTenantId)
                    .Cast<TEntity>();
            }

            // 快速路径：非门店相关实体
            if (!_isStoreScopedEntity)
            {
                return query;
            }

            // 无门店ID时返回空结果
            if (!currentIdentity.StoreId.HasValue)
            {
                return query.Where(_ => false);
            }

            var currentStoreId = currentIdentity.StoreId.Value;

            // IStoreOnlyEntity：仅当前门店
            if (_isStoreOnlyEntity)
            {
                return ((IQueryable<IStoreOnlyEntity>)query)
                    .Where(e => e.StoreId == currentStoreId)
                    .Cast<TEntity>();
            }

            // ISharedEntity：共享范围门店
            if (_isSharedEntity)
            {
                var shareStoreIds = cachedShareStoreIds
                    ?? await currentIdentity.GetShareStoreIdsAsync();

                if (shareStoreIds.Count == 0)
                {
                    return query.Where(_ => false);
                }

                return ((IQueryable<ISharedEntity>)query)
                    .Where(e => shareStoreIds.Contains(e.StoreId))
                    .Cast<TEntity>();
            }

            return query;
        }

        /// <summary>
        /// 同步应用门店范围过滤（仅在门店ID已缓存时使用）
        /// </summary>
        public static IQueryable<TEntity> Apply(
            IQueryable<TEntity> query,
            ICurrentIdentity currentIdentity,
            List<long>? cachedShareStoreIds)
        {
            // 租户过滤
            if (_isTenantEntity)
            {
                if (!currentIdentity.TenantId.HasValue)
                {
                    return query.Where(_ => false);
                }

                var currentTenantId = currentIdentity.TenantId.Value;
                query = ((IQueryable<ITenantEntity>)query)
                    .Where(e => e.TenantId == currentTenantId)
                    .Cast<TEntity>();
            }

            // 快速路径：非门店相关实体
            if (!_isStoreScopedEntity || !currentIdentity.StoreId.HasValue)
            {
                return query;
            }

            var currentStoreId = currentIdentity.StoreId.Value;

            // IStoreOnlyEntity：仅当前门店
            if (_isStoreOnlyEntity)
            {
                return ((IQueryable<IStoreOnlyEntity>)query)
                    .Where(e => e.StoreId == currentStoreId)
                    .Cast<TEntity>();
            }

            // ISharedEntity：共享范围门店（必须已缓存）
            if (_isSharedEntity && cachedShareStoreIds != null && cachedShareStoreIds.Count > 0)
            {
                return ((IQueryable<ISharedEntity>)query)
                    .Where(e => cachedShareStoreIds.Contains(e.StoreId))
                    .Cast<TEntity>();
            }

            return query;
        }
    }

    /// <summary>
    /// 可访问门店ID缓存管理器
    /// </summary>
    internal class AccessibleStoreIdsCache
    {
        private readonly ICurrentIdentity _currentIdentity;
        private List<long>? _shareStoreIds;
        private Task<List<long>>? _shareStoreIdsTask;
        private long? _cachedForStoreId;

        public AccessibleStoreIdsCache(ICurrentIdentity currentIdentity)
        {
            _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
        }

        /// <summary>
        /// 获取缓存的门店ID（如果已加载）
        /// </summary>
        public List<long>? GetCached() => _shareStoreIds;

        /// <summary>
        /// 异步获取可访问的门店ID列表（带请求级缓存）
        /// </summary>
        public async Task<List<long>> GetAsync()
        {
            var currentStoreId = _currentIdentity.StoreId;

            // 检测 storeId 是否变化，变化则清除缓存
            if (_cachedForStoreId != currentStoreId)
            {
                _shareStoreIds = null;
                _shareStoreIdsTask = null;
                _cachedForStoreId = currentStoreId;
            }

            if (_shareStoreIds != null)
            {
                return _shareStoreIds;
            }

            // 避免并发调用时重复请求
            _shareStoreIdsTask ??= _currentIdentity.GetShareStoreIdsAsync();
            _shareStoreIds = await _shareStoreIdsTask;
            return _shareStoreIds;
        }
    }
}