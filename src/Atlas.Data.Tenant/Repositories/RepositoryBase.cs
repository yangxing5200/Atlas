using Atlas.Core.Entities.Interfaces;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Atlas.Models.Tenant.Responses;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Repositories
{

    /// <summary>
    /// 一个轻量的 QueryBuilder，允许链式构建，但最终由内部执行 ToList/First/Count
    /// 不会泄露 IQueryable 或 DbContext 给调用方
    /// </summary>
    public interface IQueryBuilder<TEntity>
    {
        IQueryBuilder<TEntity> Where(Expression<Func<TEntity, bool>> predicate);
        IQueryBuilder<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector, bool descending = false);

        Task<List<TEntity>> ToListAsync(CancellationToken ct = default);
        Task<TEntity?> FirstOrDefaultAsync(CancellationToken ct = default);
        Task<long> CountAsync(CancellationToken ct = default);
        Task<PagedResult<TEntity>> ToPagedResultAsync(int pageIndex, int pageSize, CancellationToken ct = default);
    }
    #region RepositoryBase Implementation

    public abstract class RepositoryBase<TEntity, TKey> : IRepository<TEntity, TKey>
        where TEntity : class, IBaseEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        protected readonly ITenantDbContextFactory _dbFactory;
        protected readonly IDataScope _dataScope;

        protected RepositoryBase(ITenantDbContextFactory dbFactory, IDataScope dataScope)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _dataScope = dataScope ?? throw new ArgumentNullException(nameof(dataScope));
        }

        #region Add

        public virtual async Task AddAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            var db = await _dbFactory.GetDbContextAsync(ct);
            await db.Set<TEntity>().AddAsync(entity, ct);
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var db = await _dbFactory.GetDbContextAsync(ct);
            await db.Set<TEntity>().AddRangeAsync(entities, ct);
        }

        #endregion

        #region Query

        public virtual async Task<QueryBuilder<TEntity>> QueryBuilderAsync(bool useReadonly = true, CancellationToken ct = default)
        {
            var db = useReadonly ? await _dbFactory.GetReadonlyDbContextAsync(ct)
                                 : await _dbFactory.GetDbContextAsync(ct);
            var query = db.Set<TEntity>().AsNoTracking().ApplyScope(_dataScope);
            return new QueryBuilder<TEntity>(query);
        }

        public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default)
        {
            var db = await _dbFactory.GetReportDbContextAsync(ct);
            return await db.Set<TEntity>()
                .AsNoTracking()
                .ApplyScope(_dataScope)
                .FirstOrDefaultAsync(x => x.Id.Equals(id), ct);
        }

        public virtual async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        {
            var builder = await QueryBuilderAsync(useReadonly: true, ct);
            return await builder.Where(predicate).FirstOrDefaultAsync(ct);
        }

        public virtual async Task<List<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default)
        {
            var builder = await QueryBuilderAsync(useReadonly: true, ct);
            if (predicate != null) builder.Where(predicate);
            return await builder.ToListAsync(ct);
        }

     
        #endregion

        #region Remove

        public virtual async Task RemoveAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            var db = await _dbFactory.GetDbContextAsync(ct);
            db.Set<TEntity>().Remove(entity);
        }

        public virtual async Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var db = await _dbFactory.GetDbContextAsync(ct);
            db.Set<TEntity>().RemoveRange(entities);
        }

        #endregion
    }

    public class RepositoryBase<TEntity> : RepositoryBase<TEntity, long>, IRepository<TEntity>
        where TEntity : class, IBaseEntity<long>
    {
        public RepositoryBase(ITenantDbContextFactory dbFactory, IDataScope dataScope) : base(dbFactory, dataScope)
        {
        }
    }

    #endregion
}
