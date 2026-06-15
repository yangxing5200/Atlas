using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsOrganizationMasterDataService
{
    Task<BidOpsOrganizationMasterDataSyncResult> SyncOutcomeOrganizationsAsync(
        long tenantId,
        IReadOnlyList<OutcomeSupplierRecord> records,
        CancellationToken ct = default);

    Task<BidOpsOrganizationMasterDataSyncResult> SyncApprovedNoticeOrganizationsAsync(
        long tenantId,
        Notice notice,
        string sourceUrl,
        IReadOnlyList<TenderPackage> packages,
        IReadOnlyList<OutcomeSupplierRecord> outcomeRecords,
        CancellationToken ct = default);
}
