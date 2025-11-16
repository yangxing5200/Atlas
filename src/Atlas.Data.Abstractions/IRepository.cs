using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Entities;

namespace Atlas.Data.Abstractions
{
    /// <summary>
    /// 仓储基础接口（非泛型）
    /// </summary>
    public interface IRepository : IDisposable
    {
    }

    /// <summary>
    /// 仓储接口（使用默认主键类型）
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public interface IRepository<TEntity> : IRepository<TEntity, long>
        where TEntity : class, IBaseEntity<long>
    {
    }

    /// <summary>
    /// 仓储接口
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TKey"></typeparam>
    public interface IRepository<TEntity, TKey> : IRepository
       where TEntity : class, IBaseEntity<TKey>
    {
        // ========== 基本查询 ==========

        Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default);

        Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

        Task<List<TEntity>> GetAllAsync(CancellationToken ct = default);

        Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

        // ========== 分页查询 ==========

        Task<(List<TEntity> Items, int Total)> GetPagedAsync(int pageIndex, int pageSize, CancellationToken ct = default);

        Task<(List<TEntity> Items, int Total)> GetPagedAsync(Expression<Func<TEntity, bool>> predicate, int pageIndex, int pageSize, CancellationToken ct = default);

        Task<(List<TEntity> Items, int Total)> GetPagedAsync<TProperty>(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TProperty>> orderBy,
            bool ascending,
            int pageIndex,
            int pageSize,
            CancellationToken ct = default);

        // ========== 统计 ==========

        Task<int> CountAsync(CancellationToken ct = default);

        Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

        Task<long> LongCountAsync(CancellationToken ct = default);

        Task<long> LongCountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

        Task<bool> ExistsAsync(TKey id, CancellationToken ct = default);

        Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

        // ========== 聚合 ==========

        Task<TResult?> MaxAsync<TResult>(Expression<Func<TEntity, TResult>> selector, CancellationToken ct = default);

        Task<TResult?> MinAsync<TResult>(Expression<Func<TEntity, TResult>> selector, CancellationToken ct = default);

        Task<decimal> SumAsync(Expression<Func<TEntity, decimal>> selector, CancellationToken ct = default);

        Task<decimal> AverageAsync(Expression<Func<TEntity, decimal>> selector, CancellationToken ct = default);

        // ========== 添加 ==========

        Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);

        Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);

        // ========== 更新 ==========

        Task UpdateAsync(TEntity entity, CancellationToken ct = default);

        Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);

        // ========== 删除 ==========

        Task<bool> DeleteAsync(TKey id, CancellationToken ct = default);

        Task DeleteAsync(TEntity entity, CancellationToken ct = default);

        Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);

        // ========== 高级查询 ==========

        Task<List<TEntity>> FindAsync<TProperty>(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TProperty>> orderBy,
            bool ascending = true,
            CancellationToken ct = default);

        Task<List<TEntity>> GetTopAsync(int count, CancellationToken ct = default);

        Task<List<TEntity>> GetTopAsync(Expression<Func<TEntity, bool>> predicate, int count, CancellationToken ct = default);

        Task<List<TEntity>> GetTopAsync<TProperty>(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TProperty>> orderBy,
            bool ascending,
            int count,
            CancellationToken ct = default);

        // ========== 原始查询访问 ==========

        /// <summary>
        /// 只读查询（自动应用门店过滤）
        /// </summary>
        IQueryable<TEntity> AsReadonlyQueryable();

        /// <summary>
        /// 只读查询（忽略门店过滤，系统级查询）
        /// </summary>
        IQueryable<TEntity> AsReadonlyQueryableUnfiltered();


        // ========== 保存 ==========

        Task<int> SaveChangesAsync(CancellationToken ct = default);

        /// <summary>
        /// 获取用于修改的实体（从主库查询，带跟踪）
        /// </summary>
        Task<TEntity?> GetForUpdateAsync(TKey id, CancellationToken ct = default);
    }
}
