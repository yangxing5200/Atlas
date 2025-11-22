using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Abstractions
{
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// 开启事务
        /// </summary>
        Task BeginTransactionAsync(CancellationToken ct = default);

        /// <summary>
        /// 提交事务（将所有 SaveChanges 的更改持久化到数据库）
        /// </summary>
        Task CommitAsync(CancellationToken ct = default);

        /// <summary>
        /// 回滚事务（撤销所有未提交的更改）
        /// </summary>
        Task RollbackAsync(CancellationToken ct = default);

        /// <summary>
        /// 保存更改到 DbContext 的 ChangeTracker（但不提交事务）
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken ct = default);

        /// <summary>
        /// 是否在事务中
        /// </summary>
        bool HasActiveTransaction { get; }
    }
}
