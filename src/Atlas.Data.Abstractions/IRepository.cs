using Atlas.Core.Entities.Interfaces;
using Atlas.Models.Tenant.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Abstractions
{

    /// 最基础的仓储接口（不暴露 IQueryable）
    /// </summary>
    public interface IRepository<TEntity, TKey>
        where TEntity : class
    {
        Task AddAsync(TEntity entity, CancellationToken ct = default);
        Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);

        Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default);
        Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
        Task<List<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default);
   
        Task RemoveAsync(TEntity entity, CancellationToken ct = default);
        Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
        /// <summary>
        /// 获取不可追踪查询构建器（用于只读查询）
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<QueryBuilder<TEntity>> QueryAsync(CancellationToken ct = default);
        Task<QueryBuilder<TEntity>> QueryTrackingAsync(CancellationToken ct = default);

    }

    public interface IRepository<TEntity> : IRepository<TEntity, long>
        where TEntity : class
    {
    }



}
