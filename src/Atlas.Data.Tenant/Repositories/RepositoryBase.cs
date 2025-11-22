using Atlas.Core.Entities;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Providers;
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
    /// Repository 操作的共享基类 - 封装了通用 CRUD 操作
    /// 子类只需保证 DbContext 工厂正常工作
    /// </summary>
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

        public IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> where)
        {
            try
            {
                return _dbFactory.GetReadonlyDbContext()
                    .Set<TEntity>()
                    .Where(where)
                    .ApplyScope(_dataScope);
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "连接字符串未预加载。请确保: " +
                    "1) 使用了 TenantConnectionPreloadMiddleware, " +
                    "2) 或先调用异步方法如 GetReadonlyDbContextAsync()");
            }
        }

        public async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default)
        {
            var db = await _dbFactory.GetDbContextAsync(ct);

            return await db.Set<TEntity>().AsTracking()
                           .ApplyScope(_dataScope)
                           .FirstOrDefaultAsync(x => x.Id.Equals(id), ct);
        }

        public IQueryable<TEntity> Tracking(Expression<Func<TEntity, bool>> where)
        {
            try
            {
                return _dbFactory.GetDbContext()
               .Set<TEntity>()
               .ApplyScope(_dataScope)
               .Where(where);
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "连接字符串未预加载。请确保: " +
                    "1) 使用了 TenantConnectionPreloadMiddleware, " +
                    "2) 或先调用异步方法如 GetReadonlyDbContextAsync()");
            }
        }
        #endregion

        #region Remove

        public async Task RemoveAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var db = await _dbFactory.GetDbContextAsync(ct);
            db.Set<TEntity>().Remove(entity);
        }

        public async Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var db = await _dbFactory.GetDbContextAsync(ct);
            db.Set<TEntity>().RemoveRange(entities);
        }

        #endregion
    }


    /// <summary>
    /// 默认 long 主键实现
    /// </summary>
    public class RepositoryBase<TEntity> : RepositoryBase<TEntity, long>, IRepository<TEntity>
        where TEntity : class, IBaseEntity<long>
    {
        public RepositoryBase(
            ITenantDbContextFactory dbFactory,
            IDataScope dataScope)
            : base(dbFactory, dataScope)
        {
        }
    }
}
