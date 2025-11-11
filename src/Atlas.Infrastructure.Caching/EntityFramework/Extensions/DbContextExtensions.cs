// EntityFramework/Extensions/DbContextExtensions.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.EntityFramework.Extensions
{
    /// <summary>
    /// DbContext 缓存扩展方法（使用 CacheKeyDefinition）
    /// </summary>
    public static class DbContextExtensions
    {
        /// <summary>
        /// 通过缓存查找实体（使用定义）
        /// </summary>
        public static async Task<T?> FindCachedAsync<T>(
            this DbContext context,
            ICacheService cacheService,
            CacheKeyDefinition definition,
            object id,
            CancellationToken cancellationToken = default) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var result = await cacheService.GetOrSetAsync(
                definition,
                async () => await context.Set<T>().FindAsync(new object[] { id }, cancellationToken),
                instanceValue: id,
                cancellationToken: cancellationToken);

            return result.Value;
        }

        /// <summary>
        /// 查询列表并缓存（使用定义）
        /// </summary>
        public static async Task<List<T>> ToListCachedAsync<T>(
            this IQueryable<T> query,
            ICacheService cacheService,
            CacheKeyDefinition definition,
            object? instanceValue = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var result = await cacheService.GetOrSetAsync(
                definition,
                async () => await query.ToListAsync(cancellationToken),
                instanceValue: instanceValue,
                cancellationToken: cancellationToken);

            return result.Value ?? new List<T>();
        }

        /// <summary>
        /// 查询单个实体并缓存（使用定义）
        /// </summary>
        public static async Task<T?> FirstOrDefaultCachedAsync<T>(
            this IQueryable<T> query,
            ICacheService cacheService,
            CacheKeyDefinition definition,
            object? instanceValue = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var result = await cacheService.GetOrSetAsync(
                definition,
                async () => await query.FirstOrDefaultAsync(cancellationToken),
                instanceValue: instanceValue,
                cancellationToken: cancellationToken);

            return result.Value;
        }

        /// <summary>
        /// 批量通过 ID 查找实体（带缓存）
        /// </summary>
        public static async Task<IDictionary<object, T?>> FindManyCachedAsync<T>(
            this DbContext context,
            ICacheService cacheService,
            CacheKeyDefinition definition,
            IEnumerable<object> ids,
            CancellationToken cancellationToken = default) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var idList = ids.ToList();

            // 先从缓存批量获取
            var cached = await cacheService.GetManyAsync<T>(
                definition,
                idList,
                cancellationToken);

            // 找出缓存未命中的 ID
            var missedIds = cached
                .Where(kvp => kvp.Value == null)
                .Select(kvp => kvp.Key)
                .ToList();

            // 如果有未命中的，从数据库加载
            if (missedIds.Any())
            {
                var entities = await context.Set<T>()
                    .Where(e => missedIds.Contains(EF.Property<object>(e, "Id")))
                    .ToListAsync(cancellationToken);

                // 将加载的实体放回缓存
                var entitiesToCache = new Dictionary<object, T>();
                foreach (var entity in entities)
                {
                    var id = EF.Property<object>(entity, "Id");
                    entitiesToCache[id] = entity;
                }

                if (entitiesToCache.Any())
                {
                    await cacheService.SetManyAsync(
                        definition,
                        entitiesToCache,
                        cancellationToken: cancellationToken);

                    // 更新结果
                    foreach (var kvp in entitiesToCache)
                    {
                        cached[kvp.Key] = kvp.Value;
                    }
                }
            }

            return cached;
        }

        /// <summary>
        /// 计数查询并缓存（使用定义）
        /// </summary>
        public static async Task<int> CountCachedAsync<T>(
            this IQueryable<T> query,
            ICacheService cacheService,
            CacheKeyDefinition definition,
            object? instanceValue = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var result = await cacheService.GetOrSetAsync(
                definition,
                async () => await query.CountAsync(cancellationToken),
                instanceValue: instanceValue,
                cancellationToken: cancellationToken);

            return result.Value;
        }

        /// <summary>
        /// 分页查询并缓存（使用定义）
        /// </summary>
        public static async Task<PagedResult<T>> ToPagedListCachedAsync<T>(
            this IQueryable<T> query,
            ICacheService cacheService,
            CacheKeyDefinition definition,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            if (pageNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be >= 1");

            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be >= 1");

            // 使用组合的实例值（包含页码和页大小）
            var instanceValue = $"{pageNumber}:{pageSize}";

            var result = await cacheService.GetOrSetAsync(
                definition,
                async () =>
                {
                    var totalCount = await query.CountAsync(cancellationToken);
                    var items = await query
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync(cancellationToken);

                    return new PagedResult<T>
                    {
                        Items = items,
                        TotalCount = totalCount,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    };
                },
                instanceValue: instanceValue,
                cancellationToken: cancellationToken);

            return result.Value ?? new PagedResult<T>
            {
                Items = new List<T>(),
                TotalCount = 0,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
    }

    /// <summary>
    /// 分页结果
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}