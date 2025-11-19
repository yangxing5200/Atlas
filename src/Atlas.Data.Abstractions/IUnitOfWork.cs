using Atlas.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Abstractions
{
    /// <summary>
    /// 工作单元接口
    /// 仅在需要跨多个 Repository 的事务操作时使用
    /// </summary>
    public interface IUnitOfWork : IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// 获取仓储实例（所有仓储共享同一个 DbContext）
        /// </summary>
        IRepository<TEntity> GetRepository<TEntity>()
            where TEntity : class, IBaseEntity<long>;

        /// <summary>
        /// 保存所有更改
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken ct = default);

        /// <summary>
        /// 开始事务
        /// </summary>
        Task ExecuteInTransactionAsync(
            Func<Task> operation,
            CancellationToken ct = default);

        /// <summary>
        /// 提交事务
        /// </summary>
        Task CommitAsync(CancellationToken ct = default);

        /// <summary>
        /// 回滚事务
        /// </summary>
        Task RollbackAsync(CancellationToken ct = default);
    }
}
