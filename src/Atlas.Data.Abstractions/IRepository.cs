using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Entities;

namespace Atlas.Data.Abstractions
{
    // 基础仓储接口
    public interface IRepository
    {
    }

    /// <summary>
    /// 仓储基础接口
    /// </summary>
    public interface IRepository<TEntity> where TEntity : class, IBaseEntity
    {
        // 查询
        IQueryable<TEntity> Query();
        IQueryable<TEntity> QueryNoTracking();
        Task<TEntity> GetByIdAsync(long id, CancellationToken cancellationToken = default);
        Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
        Task<TEntity> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
        Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
        Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default);

        // 分页
        Task<(List<TEntity> Items, int TotalCount)> GetPagedAsync(
            int pageIndex,
            int pageSize,
            Expression<Func<TEntity, bool>> predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
            CancellationToken cancellationToken = default);

        // 新增
        Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
        Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        // 更新
        void Update(TEntity entity);
        void UpdateRange(IEnumerable<TEntity> entities);

        // 删除
        void Delete(TEntity entity);
        void DeleteRange(IEnumerable<TEntity> entities);
        Task<bool> DeleteByIdAsync(long id, CancellationToken cancellationToken = default);

        // 保存
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }

}
