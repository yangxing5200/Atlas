using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// Manages database transactions and change persistence for tenant operations.
    /// Implements Unit of Work pattern with thread-safe lazy DbContext initialization.
    /// </summary>
    /// <remarks>
    /// CONCURRENCY CONSTRAINTS:
    /// - DbContext is NOT thread-safe. This class should be scoped per request/operation.
    /// - Do NOT share instances across threads or concurrent tasks.
    /// - Use one instance per logical unit of work (typically per HTTP request).
    /// </remarks>
    public class TenantUnitOfWork : IUnitOfWork, IAsyncDisposable
    {
        private readonly ITenantDbContextFactory _dbFactory;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        private IDbContextTransaction? _transaction;
        private AtlasTenantDbContext? _dbContext;
        private bool _disposed;


        public TenantUnitOfWork(ITenantDbContextFactory dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public bool HasActiveTransaction => _transaction != null;

        /// <summary>
        /// Begins a new database transaction.
        /// </summary>
        /// <exception cref="InvalidOperationException">Transaction already active or thread safety violation.</exception>
        public async Task BeginTransactionAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
           
            if (_transaction != null)
                throw new InvalidOperationException("Transaction already in progress.");

            _dbContext = await EnsureDbContextAsync(ct);
            _transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        }

        /// <summary>
        /// Persists tracked changes to the database.
        /// Changes are committed immediately if no transaction is active,
        /// otherwise staged until CommitAsync is called.
        /// </summary>
        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
           
            _dbContext = await EnsureDbContextAsync(ct);
            return await _dbContext.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Persists tracked changes for explicit tenantId.
        /// Used in login scenarios where there is no identity context.
        /// </summary>
        public async Task<int> SaveChangesAsync(long tenantId, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            var db = await _dbFactory.GetDbContextAsync(tenantId, ct);
            return await db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Commits the active transaction and persists all staged changes.
        /// Automatically rolls back on failure.
        /// </summary>
        public async Task CommitAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
           
            if (_transaction == null)
                throw new InvalidOperationException("No active transaction to commit.");

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
                await DisposeTransactionAsync();
            }
        }

        /// <summary>
        /// Rolls back the active transaction and discards all uncommitted changes.
        /// </summary>
        public async Task RollbackAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
           
            if (_transaction != null)
            {
                try
                {
                    await _transaction.RollbackAsync(ct);
                }
                finally
                {
                    await DisposeTransactionAsync();
                }
            }
        }

        /// <summary>
        /// Ensures DbContext is initialized with thread-safe lazy loading.
        /// Uses double-checked locking pattern to prevent race conditions.
        /// </summary>
        private async Task<AtlasTenantDbContext> EnsureDbContextAsync(CancellationToken ct = default)
        {
            // Fast path: already initialized
            if (_dbContext != null)
                return _dbContext;

            // Acquire lock for initialization
            await _initLock.WaitAsync(ct);
            try
            {
                // Double-check after lock acquisition
                if (_dbContext != null)
                    return _dbContext;

                _dbContext = await _dbFactory.GetDbContextAsync(ct);
                return _dbContext;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task DisposeTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TenantUnitOfWork));
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (_transaction != null)
            {
                // 如果事务还活跃，先回滚
                if (HasActiveTransaction)
                {
                    try
                    {
                        _transaction.Rollback();
                    }
                    catch
                    {
                        // 记录日志但不抛出异常
                    }
                }
                _transaction.Dispose();
                _transaction = null;
            }

            _initLock.Dispose();
            _disposed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }

            // DbContext lifecycle is managed by factory
            _dbContext = null;

            _initLock.Dispose();
            _disposed = true;
        }
    }
}
