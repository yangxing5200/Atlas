using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Services.Abstractions
{
    public abstract class ServiceBase
    {
        protected readonly IUnitOfWork UnitOfWork;

        protected ServiceBase(IUnitOfWork unitOfWork)
        {
            UnitOfWork = unitOfWork;
        }

        /// <summary>
        /// 在事务中执行操作
        /// </summary>
        protected async Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> action,
            CancellationToken ct = default)
        {
            await UnitOfWork.BeginTransactionAsync(ct);
            try
            {
                var result = await action();
                await UnitOfWork.CommitAsync(ct);
                return result;
            }
            catch
            {
                await UnitOfWork.RollbackAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// 在事务中执行操作（无返回值）
        /// </summary>
        protected async Task ExecuteInTransactionAsync(
            Func<Task> action,
            CancellationToken ct = default)
        {
            await UnitOfWork.BeginTransactionAsync(ct);
            try
            {
                await action();
                await UnitOfWork.CommitAsync(ct);
            }
            catch
            {
                await UnitOfWork.RollbackAsync(ct);
                throw;
            }
        }
    }

}
