using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Abstractions
{
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
        /// Gets accessible store IDs based on current user's store type and permissions.
        /// Returns cached result if available.
        /// </summary>
        /// <remarks>
        /// Cache miss returns conservative fallback (current store only).
        /// Ensure PreloadShareStoreIdsAsync is called during request initialization.
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