using System;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Abstractions
{
    /// <summary>
    /// Unit of Work interface for global database operations.
    /// Provides transaction management for global (tenant-agnostic) data operations.
    /// </summary>
    public interface IGlobalUnitOfWork : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Begins a new database transaction.
        /// </summary>
        Task BeginTransactionAsync(CancellationToken ct = default);

        /// <summary>
        /// Commits the active transaction and persists all staged changes.
        /// </summary>
        Task CommitAsync(CancellationToken ct = default);

        /// <summary>
        /// Rolls back the active transaction and discards all uncommitted changes.
        /// </summary>
        Task RollbackAsync(CancellationToken ct = default);

        /// <summary>
        /// Persists tracked changes to the database.
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken ct = default);

        /// <summary>
        /// Whether there is an active transaction.
        /// </summary>
        bool HasActiveTransaction { get; }
    }
}
