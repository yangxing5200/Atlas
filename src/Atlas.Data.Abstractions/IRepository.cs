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
        IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> where);
        Task<TEntity?> GetByIdAsync(long id, CancellationToken ct = default);
        IQueryable<TEntity> Tracking(Expression<Func<TEntity, bool>> where);
        Task AddAsync(TEntity entity, CancellationToken ct = default);
        Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
        Task RemoveAsync(TEntity entity, CancellationToken ct = default);
        Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
    }
}
