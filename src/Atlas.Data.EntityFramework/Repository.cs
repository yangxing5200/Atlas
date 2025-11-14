using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Entities;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Common
{
    /// <summary>
    /// 仓储基类实现
    /// </summary>
    public class Repository<TEntity> : IRepository<TEntity> where TEntity : class, IBaseEntity
    {
        protected readonly DbContext _context;
        protected readonly DbSet<TEntity> _dbSet;
        protected readonly ICurrentUserService _currentUserService;
        protected readonly IStoreService _storeService;

        public Repository(
            DbContext context,
            ICurrentUserService currentUserService,
            IStoreService storeService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = _context.Set<TEntity>();
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _storeService = storeService ?? throw new ArgumentNullException(nameof(storeService));
        }

        #region 查询基础方法

        /// <summary>
        /// 获取查询基础（自动应用过滤）
        /// </summary>
        public virtual IQueryable<TEntity> Query()
        {
            var query = _dbSet.AsQueryable();
            return ApplyFilters(query);
        }

        /// <summary>
        /// 获取只读查询基础（自动应用过滤）
        /// </summary>
        public virtual IQueryable<TEntity> QueryNoTracking()
        {
            var query = _dbSet.AsNoTracking();
            return ApplyFilters(query);
        }

        /// <summary>
        /// 应用自动过滤规则
        /// </summary>
        protected virtual IQueryable<TEntity> ApplyFilters(IQueryable<TEntity> query)
        {
            // 1. 软删除过滤
            if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
            {
                query = query.Where(e => !((ISoftDelete)e).IsDeleted);
            }

            // 2. 租户隔离
            if (typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity)) && _currentUserService.TenantId.HasValue)
            {
                var tenantId = _currentUserService.TenantId.Value;
                query = query.Where(e => ((ITenantEntity)e).TenantId == tenantId);
            }

            // 3. 门店数据隔离
            if (_currentUserService.StoreId.HasValue)
            {
                query = ApplyStoreFilter(query);
            }

            return query;
        }

        /// <summary>
        /// 应用门店过滤规则
        /// </summary>
        protected virtual IQueryable<TEntity> ApplyStoreFilter(IQueryable<TEntity> query)
        {
            var currentStoreId = _currentUserService.StoreId.Value;

            // 门店独享数据：只能看到自己门店的数据
            if (typeof(IStoreOnlyEntity).IsAssignableFrom(typeof(TEntity)))
            {
                return query.Where(e => ((IStoreOnlyEntity)e).StoreId == currentStoreId);
            }

            // 门店共享数据：根据门店类型决定共享范围
            if (typeof(ISharedEntity).IsAssignableFrom(typeof(TEntity)))
            {
                var shareStoreIds = _storeService.GetShareStoreIds(currentStoreId);
                return query.Where(e => shareStoreIds.Contains(((ISharedEntity)e).StoreId));
            }

            return query;
        }

        #endregion

        #region 查询方法

        public virtual async Task<TEntity> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            return await Query().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        }

        public virtual async Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await QueryNoTracking().ToListAsync(cancellationToken);
        }

        public virtual async Task<List<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await QueryNoTracking().Where(predicate).ToListAsync(cancellationToken);
        }

        public virtual async Task<TEntity> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await Query().FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public virtual async Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await QueryNoTracking().AnyAsync(predicate, cancellationToken);
        }

        public virtual async Task<int> CountAsync(
            Expression<Func<TEntity, bool>> predicate = null,
            CancellationToken cancellationToken = default)
        {
            var query = QueryNoTracking();
            if (predicate != null)
            {
                query = query.Where(predicate);
            }
            return await query.CountAsync(cancellationToken);
        }

        public virtual async Task<(List<TEntity> Items, int TotalCount)> GetPagedAsync(
            int pageIndex,
            int pageSize,
            Expression<Func<TEntity, bool>> predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
            CancellationToken cancellationToken = default)
        {
            var query = QueryNoTracking();

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            if (orderBy != null)
            {
                query = orderBy(query);
            }

            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        #endregion

        #region 新增方法

        public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            SetCreateAudit(entity);
            await _dbSet.AddAsync(entity, cancellationToken);
            return entity;
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            foreach (var entity in entities)
            {
                SetCreateAudit(entity);
            }
            await _dbSet.AddRangeAsync(entities, cancellationToken);
        }

        #endregion

        #region 更新方法

        public virtual void Update(TEntity entity)
        {
            SetUpdateAudit(entity);
            _dbSet.Update(entity);
        }

        public virtual void UpdateRange(IEnumerable<TEntity> entities)
        {
            foreach (var entity in entities)
            {
                SetUpdateAudit(entity);
            }
            _dbSet.UpdateRange(entities);
        }

        #endregion

        #region 删除方法

        public virtual void Delete(TEntity entity)
        {
            // 如果支持软删除，使用软删除
            if (entity is ISoftDelete softDeleteEntity)
            {
                softDeleteEntity.IsDeleted = true;
                softDeleteEntity.DeletedAt = DateTime.UtcNow;
                softDeleteEntity.DeletedBy = _currentUserService.UserId;
                Update(entity);
            }
            else
            {
                _dbSet.Remove(entity);
            }
        }

        public virtual void DeleteRange(IEnumerable<TEntity> entities)
        {
            foreach (var entity in entities)
            {
                Delete(entity);
            }
        }

        public virtual async Task<bool> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity == null)
            {
                return false;
            }

            Delete(entity);
            return true;
        }

        #endregion

        #region 保存

        public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        #endregion

        #region 审计信息设置

        protected virtual void SetCreateAudit(TEntity entity)
        {
            entity.CreatedAt = DateTime.UtcNow;

            if (entity is IAuditable auditable && _currentUserService.UserId.HasValue)
            {
                auditable.CreatedBy = _currentUserService.UserId.Value;
            }

            if (entity is ITenantEntity tenantEntity && _currentUserService.TenantId.HasValue)
            {
                tenantEntity.TenantId = _currentUserService.TenantId.Value;
            }

            if (entity is IStoreEntity storeEntity && _currentUserService.StoreId.HasValue)
            {
                storeEntity.StoreId = _currentUserService.StoreId.Value;
            }
        }

        protected virtual void SetUpdateAudit(TEntity entity)
        {
            entity.UpdatedAt = DateTime.UtcNow;

            if (entity is IAuditable auditable && _currentUserService.UserId.HasValue)
            {
                auditable.UpdatedBy = _currentUserService.UserId.Value;
            }

            if (entity is IVersioned versioned)
            {
                versioned.Version++;
            }
        }

        #endregion
    }
}
