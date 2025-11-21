using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Atlas.Core.Entities;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// 工作单元实现
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ITenantDbContextFactory _factory;
        private readonly ICurrentIdentity _currentIdentity;
        private readonly IIdGenerator _idGenerator;
        private readonly Dictionary<Type, object> _repositories = new();

        private AtlasTenantDbContext? _context;
        private Task<AtlasTenantDbContext>? _contextTask;
        private IDbContextTransaction? _transaction;
        private bool _disposed;

        public UnitOfWork(
            ITenantDbContextFactory factory,
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
            _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        }

        /// <summary>
        /// 延迟初始化 DbContext
        /// </summary>
        private async Task<AtlasTenantDbContext> GetContextAsync()
        {
            if (_context != null)
                return _context;

            _contextTask ??= _factory.GetMasterDbContextAsync();
            _context = await _contextTask;
            return _context;
        }

        /// <summary>
        /// 获取仓储（所有仓储共享同一个 DbContext）
        /// </summary>
        public IRepository<TEntity> GetRepository<TEntity>()
            where TEntity : class, IBaseEntity<long>
        {
            var type = typeof(TEntity);

            if (_repositories.TryGetValue(type, out var cached))
            {
                return (IRepository<TEntity>)cached;
            }

            var repo = new UnitOfWorkRepository<TEntity>(this, _currentIdentity, _idGenerator);
            _repositories[type] = repo;
            return repo;
        }

        /// <summary>
        /// 保存所有更改
        /// </summary>
        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            var context = await GetContextAsync();

            try
            {
                return await context.SaveChangesAsync(ct);
            }
            catch (Exception)
            {
                if (_transaction != null)
                {
                    await RollbackAsync(ct);
                }
                throw;
            }
        }

        /// <summary>
        /// 在事务中执行操作（推荐使用此方法，支持重试策略）
        /// </summary>
        public async Task ExecuteInTransactionAsync(
            Func<Task> operation,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var strategy = context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(ct);
                try
                {
                    await operation();
                    await transaction.CommitAsync(ct);
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            });
        }

        /// <summary>
        /// 在事务中执行操作并返回结果（推荐使用此方法，支持重试策略）
        /// </summary>
        public async Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<Task<TResult>> operation,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var strategy = context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(ct);
                try
                {
                    var result = await operation();
                    await transaction.CommitAsync(ct);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            });
        }

        /// <summary>
        /// 开始事务（不推荐直接使用，请使用 ExecuteInTransactionAsync）
        /// 注意：此方法不支持重试策略
        /// </summary>
        [Obsolete("不推荐使用此方法，请使用 ExecuteInTransactionAsync 以获得重试策略支持")]
        public async Task BeginTransactionAsync(CancellationToken ct = default)
        {
            if (_transaction != null)
            {
                throw new InvalidOperationException("事务已经开始");
            }

            var context = await GetContextAsync();
            _transaction = await context.Database.BeginTransactionAsync(ct);
        }

        /// <summary>
        /// 提交事务（配合 BeginTransactionAsync 使用）
        /// </summary>
        [Obsolete("不推荐使用此方法，请使用 ExecuteInTransactionAsync")]
        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_transaction == null)
            {
                throw new InvalidOperationException("没有活动的事务");
            }

            try
            {
                await _transaction.CommitAsync(ct);
            }
            catch
            {
                await RollbackAsync(ct);
                throw;
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        /// <summary>
        /// 回滚事务（配合 BeginTransactionAsync 使用）
        /// </summary>
        [Obsolete("不推荐使用此方法，请使用 ExecuteInTransactionAsync")]
        public async Task RollbackAsync(CancellationToken ct = default)
        {
            if (_transaction == null)
            {
                return;
            }

            try
            {
                await _transaction.RollbackAsync(ct);
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        /// <summary>
        /// 内部方法：供 UnitOfWorkRepository 使用
        /// </summary>
        internal Task<AtlasTenantDbContext> GetDbContextAsync() => GetContextAsync();

        /// <summary>
        /// 异步释放资源
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
            }

            if (_context != null)
            {
                await _context.DisposeAsync();
            }

            _disposed = true;
        }

        /// <summary>
        /// 同步释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _transaction?.Dispose();
            _context?.Dispose();
            _disposed = true;
        }
    }
}