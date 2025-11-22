using Atlas.Core.Entities;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using System.Collections.Generic;
using System.Linq;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// 统一封装门店/租户范围过滤
    /// </summary>
    internal static class EntityScopeFilter<TEntity>
        where TEntity : class
    {
        // 用于优化性能（避免每次反射）
        private static readonly bool IsStoreOnly = typeof(IStoreOnlyEntity).IsAssignableFrom(typeof(TEntity));
        private static readonly bool IsShared = typeof(ISharedEntity).IsAssignableFrom(typeof(TEntity));
        private static readonly bool IsTenantScoped = typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity));
        private static readonly bool IsStoreScoped = IsStoreOnly || IsShared;

        public static IQueryable<TEntity> Apply(IQueryable<TEntity> query, IDataScope scope)
        {
            if (query == null) return query;

            // ----------------------------
            // 租户过滤
            // ----------------------------
            if (IsTenantScoped)
            {
                if (!scope.TenantId.HasValue)
                {
                    return query.Where(_ => false); // safety return
                }

                var tenantId = scope.TenantId.Value;

                query = query
                    .Cast<ITenantEntity>()
                    .Where(e => e.TenantId == tenantId)
                    .Cast<TEntity>();
            }

            // ----------------------------
            // 非门店相关实体
            // ----------------------------
            if (!IsStoreScoped || !scope.StoreId.HasValue)
            {
                return query;
            }

            var storeId = scope.StoreId.Value;

            // ----------------------------
            // IStoreOnlyEntity：只能当前门店
            // ----------------------------
            if (IsStoreOnly)
            {
                return query
                    .Cast<IStoreOnlyEntity>()
                    .Where(e => e.StoreId == storeId)
                    .Cast<TEntity>();
            }

            // ----------------------------
            // ISharedEntity：多个共享门店
            // ----------------------------
            if (IsShared)
            {
                var shareIds = scope.GetShareStoreIds();
                if (shareIds is { Count: > 0 })
                {
                    return query
                        .Cast<ISharedEntity>()
                        .Where(e => shareIds.Contains(e.StoreId))
                        .Cast<TEntity>();
                }
            }

            return query;
        }
    }
    public static class QueryableScopeExtensions
    {
        /// <summary>
        /// 应用租户/门店范围过滤的扩展方法
        /// </summary>
        public static IQueryable<TEntity> ApplyScope<TEntity>(
            this IQueryable<TEntity> query,
            IDataScope scope)
            where TEntity : class
        {
            return EntityScopeFilter<TEntity>.Apply(query, scope);
        }
    }
}
