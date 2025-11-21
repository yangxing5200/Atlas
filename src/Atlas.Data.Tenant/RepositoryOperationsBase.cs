using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Entities;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Providers;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// Repository 操作的共享基类 - 包含所有通用的 CRUD 实现
    /// 子类只需实现 GetContextAsync 方法
    /// </summary>
    public abstract class RepositoryOperationsBase<TEntity, TKey> : IRepository<TEntity, TKey>
        where TEntity : class, IBaseEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        protected readonly ICurrentIdentity _currentIdentity;
        protected readonly IIdGenerator _idGenerator;
        protected readonly ITenantDbContextFactory _dbFactory;

        protected RepositoryOperationsBase(
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator,
            ITenantDbContextFactory dbFactory)
        {
            _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
            _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        #region Add

        public virtual async Task AddAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity is ISnowflakeId se && se.Id == 0)
            {
                se.Id = _idGenerator.NextId();
            }

            var db = await _dbFactory.GetMasterDbContextAsync(ct);
            await db.Set<TEntity>().AddAsync(entity, ct);
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            var list = entities.ToList();

            foreach (var entity in list)
            {
                if (entity is ISnowflakeId se && se.Id == 0)
                    se.Id = _idGenerator.NextId();
            }

            var db = await _dbFactory.GetMasterDbContextAsync(ct);
            await db.Set<TEntity>().AddRangeAsync(list, ct);
        }

        #endregion

        #region Query

        public IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> where)
        {
            return _dbFactory.GetReadonlyDbContext()
                             .Set<TEntity>()
                             .AsNoTracking()
                             .Where(where);
        }

        public async Task<TEntity?> GetByIdAsync(long id, CancellationToken ct = default)
        {
            var db = await _dbFactory.GetReadonlyDbContextAsync();
            return await db.Set<TEntity>()
                           .FirstOrDefaultAsync(x => EF.Property<long>(x, "Id") == id);
        }

        public IQueryable<TEntity> Tracking(Expression<Func<TEntity, bool>> where)
        {
            return _dbFactory.GetDbContext()
                             .Set<TEntity>()
                             .Where(where); // tracking enabled
        }

        #endregion

        #region Remove

        public async Task RemoveAsync(TEntity entity, CancellationToken ct = default)
        {
            var db = await _dbFactory.GetMasterDbContextAsync(ct);
            db.Set<TEntity>().Remove(entity);
        }

        public async Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            var db = await _dbFactory.GetMasterDbContextAsync(ct);
            db.Set<TEntity>().RemoveRange(entities);
        }

        #endregion
    }
}
