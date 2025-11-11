// EntityFramework/Extensions/DbContextExtensions.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.EntityFramework.Extensions
{
    public static class DbContextExtensions
    {
        public static async Task<T?> FindCachedAsync<T>(
            this DbContext context,
            ICacheService cacheService,
            object id,
            CancellationToken cancellationToken = default) where T : class
        {
            var entityType = typeof(T).Name;
            var key = $"{entityType}:{id}";

            var result = await cacheService.GetOrSetAsync(
                key,
                async () => await context.Set<T>().FindAsync(new object[] { id }, cancellationToken),
                CacheOptions.WithTags($"entity:{entityType}", $"entity:{entityType}:{id}"),
                cancellationToken);

            return result.Value; // 使用 .Value 属性获取实际值
        }

        public static IQueryable<T> AsCached<T>(
            this IQueryable<T> query,
            ICacheService cacheService,
            string cacheKey,
            CacheOptions? options = null) where T : class
        {
            // This is a placeholder - actual implementation would require query interception
            return query;
        }
    }
}