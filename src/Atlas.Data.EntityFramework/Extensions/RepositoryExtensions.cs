using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Data.Abstractions;

namespace Atlas.Data.Common.Extensions
{
    public static class RepositoryExtensions
    {
        public static async Task<List<TEntity>> ReadonlyQueryAsync<TEntity, TKey>(
            this IRepository<TEntity, TKey> repository,
            Func<IQueryBuilder<TEntity>, IQueryBuilder<TEntity>> buildQuery,
            CancellationToken ct = default)
            where TEntity : class
        {
            var builder = await repository.QueryAsync(ct: ct);
            return await buildQuery(builder).ToListAsync(ct);
        }

        public static async Task<List<TEntity>> QueryWithTrackingAsync<TEntity, TKey>(
          this IRepository<TEntity, TKey> repository,
          Func<IQueryBuilder<TEntity>, IQueryBuilder<TEntity>> buildQuery,
          CancellationToken ct = default)
          where TEntity : class
        {
            var builder = await repository.QueryTrackingAsync(ct);
            return await buildQuery(builder).ToListAsync(ct);
        }

        public static async Task<TEntity?> TrackingFirstOrDefaultAsync<TEntity, TKey>(
          this IRepository<TEntity, TKey> repository,
          Func<IQueryBuilder<TEntity>, IQueryBuilder<TEntity>> buildQuery,
          CancellationToken ct = default)
          where TEntity : class
        {
            var builder = await repository.QueryTrackingAsync(ct);
            return await buildQuery(builder).FirstOrDefaultAsync(ct);
        }

        public static async Task<TEntity?> QueryFirstAsync<TEntity, TKey>(
            this IRepository<TEntity, TKey> repository,
            Func<IQueryBuilder<TEntity>, IQueryBuilder<TEntity>> buildQuery,
            CancellationToken ct = default)
            where TEntity : class
        {
            var builder = await repository.QueryAsync(ct: ct);
            return await buildQuery(builder).FirstOrDefaultAsync(ct);
        }

        public static async Task<long> QueryCountAsync<TEntity, TKey>(
            this IRepository<TEntity, TKey> repository,
            Func<IQueryBuilder<TEntity>, IQueryBuilder<TEntity>> buildQuery,
            CancellationToken ct = default)
            where TEntity : class
        {
            var builder = await repository.QueryAsync(ct: ct);
            return await buildQuery(builder).CountAsync(ct);
        }
    }
}
