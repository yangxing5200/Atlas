using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Abstractions
{
    public sealed record DataScopeSnapshot(
        long? TenantId,
        long? StoreId,
        IReadOnlyList<long> ShareStoreIds)
    {
        public static DataScopeSnapshot Empty { get; } = new(null, null, Array.Empty<long>());
    }

    /// <summary>
    /// Provides data access scope control based on tenant and store context.
    /// </summary>
    public interface IDataScope
    {
        /// <summary>
        /// Current tenant identifier from authentication context.
        /// </summary>
        long? TenantId { get; }

        /// <summary>
        /// Current store identifier from authentication context.
        /// </summary>
        long? StoreId { get; }

        /// <summary>
        /// Resolves the current request's tenant/store data scope asynchronously.
        /// </summary>
        /// <remarks>
        /// Repository query creation must call this before building IQueryable filters,
        /// because shared store ids can require database access.
        /// </remarks>
        Task<DataScopeSnapshot> ResolveAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets accessible store IDs based on current user's store type and permissions.
        /// Resolves from cache or database asynchronously.
        /// </summary>
        Task<List<long>> GetShareStoreIdsAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets accessible store IDs based on current user's store type and permissions.
        /// Returns cached result if available.
        /// </summary>
        /// <remarks>
        /// Legacy synchronous path. Cache miss returns conservative fallback.
        /// Prefer ResolveAsync or GetShareStoreIdsAsync for request handling.
        /// </remarks>
        List<long> GetShareStoreIds();

        /// <summary>
        /// Preloads accessible store IDs into cache for current user context.
        /// Should be called by middleware during request initialization.
        /// </summary>
        /// <remarks>
        /// Access rules:
        /// - Franchised: Self only
        /// - Headquarters: Self + all direct-operated children
        /// - Direct-operated: Parent headquarters + all sibling stores
        /// </remarks>
        Task PreloadShareStoreIdsAsync(CancellationToken ct = default);
    }
}
