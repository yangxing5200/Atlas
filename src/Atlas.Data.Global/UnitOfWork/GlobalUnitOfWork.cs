using Atlas.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Global.UnitOfWork
{
    /// <summary>
    /// Manages database transactions and change persistence for global operations.
    /// Implements Unit of Work pattern with thread-safe DbContext management.
    /// </summary>
    /// <remarks>
    /// CONCURRENCY CONSTRAINTS:
    /// - DbContext is NOT thread-safe. This class should be scoped per request/operation.
    /// - Do NOT share instances across threads or concurrent tasks.
    /// - Use one instance per logical unit of work (typically per HTTP request).
    /// </remarks>
    public class GlobalUnitOfWork : IGlobalUnitOfWork
    {
        private readonly AtlasGlobalDbContext _dbContext;
        private IDbContextTransaction? _transaction;
        private bool _disposed;

        public GlobalUnitOfWork(AtlasGlobalDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public bool HasActiveTransaction => _transaction != null;

        /// <summary>
        /// Begins a new database transaction.
        /// </summary>
        /// <exception cref="InvalidOperationException">Transaction already active.</exception>
        public async Task BeginTransactionAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (_transaction != null)
                throw new InvalidOperationException("Transaction already in progress.");

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
                throw new ObjectDisposedException(nameof(GlobalUnitOfWork));
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (_transaction != null)
            {
                // If transaction is still active, roll back first
                if (HasActiveTransaction)
                {
                    try
                    {
                        _transaction.Rollback();
                    }
                    catch (Exception)
                    {
                        // Rollback may fail if connection is already closed.
                        // This is acceptable during dispose - we're cleaning up resources.
                    }
                }
                _transaction.Dispose();
                _transaction = null;
            }

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

            _disposed = true;
        }
    }
}
