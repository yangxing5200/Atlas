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
    /// Implements Unit of Work pattern with lazy DbContext initialization.
    /// </summary>
    public class TenantUnitOfWork : IUnitOfWork, IAsyncDisposable
    {
        private readonly ITenantDbContextFactory _dbFactory;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private IDbContextTransaction? _transaction;
        private AtlasTenantDbContext? _dbContext;
        private bool _disposed;

        public TenantUnitOfWork(ITenantDbContextFactory dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public bool HasActiveTransaction => _transaction != null;

        /// <summary>
        /// Begins a new database transaction. Throws if transaction already exists.
        /// </summary>
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
        /// Returns cached instance if available.
        /// </summary>
        private async Task<AtlasTenantDbContext> EnsureDbContextAsync(CancellationToken ct = default)
        {
            if (_dbContext != null)
                return _dbContext;

            await _lock.WaitAsync(ct);
            try
            {
                // Double-check pattern to prevent race conditions
                if (_dbContext != null)
                    return _dbContext;

                _dbContext = await _dbFactory.GetDbContextAsync(ct);
                return _dbContext;
            }
            finally
            {
                _lock.Release();
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

            _transaction?.Dispose();
            _transaction = null;
            _lock.Dispose();
            
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

            if (_dbContext != null)
            {
                await _dbContext.DisposeAsync();
                _dbContext = null;
            }

            _lock.Dispose();
            _disposed = true;
        }
    }
}