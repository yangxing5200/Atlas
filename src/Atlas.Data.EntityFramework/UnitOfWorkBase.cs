using System;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Atlas.Data.Common
{
    /// <summary>
    /// 工作单元基类
    /// </summary>
    public abstract class UnitOfWorkBase : IUnitOfWork
    {
        private readonly DbContext _context;
        private IDbContextTransaction? _currentTransaction;
        private bool _disposed;

        protected UnitOfWorkBase(DbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 获取 DbContext
        /// </summary>
        protected DbContext Context => _context;

        /// <summary>
        /// 检查是否有活动事务
        /// </summary>
        public bool HasActiveTransaction => _currentTransaction != null;

        /// <summary>
        /// 保存所有更改
        /// </summary>
        public virtual async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // 处理并发冲突
                throw new InvalidOperationException("数据已被其他用户修改，请刷新后重试", ex);
            }
            catch (DbUpdateException ex)
            {
                // 处理数据库更新异常
                throw new InvalidOperationException("保存数据失败", ex);
            }
        }

        /// <summary>
        /// 开始事务
        /// </summary>
        public virtual async Task BeginTransactionAsync(
            CancellationToken cancellationToken = default)
        {
            if (_currentTransaction != null)
            {
                throw new InvalidOperationException("已存在活动事务");
            }

            _currentTransaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        public virtual async Task CommitTransactionAsync(
            CancellationToken cancellationToken = default)
        {
            if (_currentTransaction == null)
            {
                throw new InvalidOperationException("没有活动事务可以提交");
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await _currentTransaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await RollbackTransactionAsync(cancellationToken);
                throw;
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    _currentTransaction.Dispose();
                    _currentTransaction = null;
                }
            }
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        public virtual async Task RollbackTransactionAsync(
            CancellationToken cancellationToken = default)
        {
            if (_currentTransaction == null)
            {
                return;
            }

            try
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    _currentTransaction.Dispose();
                    _currentTransaction = null;
                }
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    _currentTransaction?.Dispose();
                    _context?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}