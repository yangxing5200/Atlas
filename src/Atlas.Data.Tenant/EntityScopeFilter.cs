using Atlas.Core.Entities.Interfaces;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

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

        static EntityScopeFilter()
        {
            // 验证接口互斥性
            if (IsStoreOnly && IsShared)
            {
                throw new InvalidOperationException(
                    $"Entity {typeof(TEntity).Name} cannot implement both IStoreOnlyEntity and ISharedEntity. " +
                    "These interfaces are mutually exclusive.");
            }
        }

        public static IQueryable<TEntity> Apply(IQueryable<TEntity> query, IDataScope scope)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            // ----------------------------
            // 租户过滤
            // ----------------------------
            if (IsTenantScoped)
            {
                if (!scope.TenantId.HasValue)
                {
                    // 需要租户过滤但没有 TenantId，安全返回空结果
                    return query.Where(_ => false);
                }

                var tenantId = scope.TenantId.Value;

                // 使用表达式树避免 Cast 问题
                query = ApplyTenantFilter(query, tenantId);
            }

            // ----------------------------
            // 非门店相关实体
            // ----------------------------
            if (!IsStoreScoped)
            {
                return query;
            }

            // 需要门店过滤但没有 StoreId，安全返回空结果
            if (!scope.StoreId.HasValue)
            {
                return query.Where(_ => false);
            }

            var storeId = scope.StoreId.Value;

            // ----------------------------
            // IStoreOnlyEntity：只能当前门店
            // ----------------------------
            if (IsStoreOnly)
            {
                return ApplyStoreOnlyFilter(query, storeId);
            }

            // ----------------------------
            // ISharedEntity：多个共享门店
            // ----------------------------
            if (IsShared)
            {
                var shareIds = scope.GetShareStoreIds();

                // 空列表表示没有可访问门店，返回空结果（安全策略）
                if (shareIds == null || shareIds.Count == 0)
                {
                    return query.Where(_ => false);
                }

                return ApplySharedFilter(query, shareIds);
            }

            return query;
        }

        /// <summary>
        /// 应用租户过滤（使用表达式树避免 EF Core Cast 问题）
        /// </summary>
        private static IQueryable<TEntity> ApplyTenantFilter(IQueryable<TEntity> query, long tenantId)
        {
            // 构建表达式: e => e.TenantId == tenantId
            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var property = Expression.Property(parameter, "TenantId");
            var constant = Expression.Constant(tenantId);
            var equals = Expression.Equal(property, constant);
            var lambda = Expression.Lambda<Func<TEntity, bool>>(equals, parameter);

            return query.Where(lambda);
        }

        /// <summary>
        /// 应用单门店过滤
        /// </summary>
        private static IQueryable<TEntity> ApplyStoreOnlyFilter(IQueryable<TEntity> query, long storeId)
        {
            // 构建表达式: e => e.StoreId == storeId
            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var property = Expression.Property(parameter, "StoreId");
            var constant = Expression.Constant(storeId);
            var equals = Expression.Equal(property, constant);
            var lambda = Expression.Lambda<Func<TEntity, bool>>(equals, parameter);

            return query.Where(lambda);
        }

        /// <summary>
        /// 应用共享门店过滤
        /// </summary>
        private static IQueryable<TEntity> ApplySharedFilter(IQueryable<TEntity> query, List<long> shareIds)
        {
            // 构建表达式: e => shareIds.Contains(e.StoreId)
            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var property = Expression.Property(parameter, "StoreId");

            // 使用 Enumerable.Contains 方法
            var containsMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(long));

            var containsCall = Expression.Call(
                null,
                containsMethod,
                Expression.Constant(shareIds),
                property);

            var lambda = Expression.Lambda<Func<TEntity, bool>>(containsCall, parameter);

            return query.Where(lambda);
        }
    }

    public static class QueryableScopeExtensions
    {
        /// <summary>
        /// 应用租户/门店范围过滤的扩展方法
        /// </summary>
        /// <remarks>
        /// 过滤规则：
        /// - ITenantEntity: 按 TenantId 过滤
        /// - IStoreOnlyEntity: 仅当前门店
        /// - ISharedEntity: 当前用户可访问的所有门店
        /// - 缺少必要的 scope 信息时返回空结果（安全策略）
        /// </remarks>
        public static IQueryable<TEntity> ApplyScope<TEntity>(
            this IQueryable<TEntity> query,
            IDataScope scope)
            where TEntity : class
        {
            return EntityScopeFilter<TEntity>.Apply(query, scope);
        }
    }
}