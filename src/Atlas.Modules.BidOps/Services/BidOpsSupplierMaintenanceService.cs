using Atlas.Core.Authorization;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsSupplierMaintenanceService : IBidOpsSupplierMaintenanceService
{
    private readonly IRepository<SupplierEvidenceDocument> _evidenceDocuments;
    private readonly IUnitOfWork _unitOfWork;

    public BidOpsSupplierMaintenanceService(
        IRepository<SupplierEvidenceDocument> evidenceDocuments,
        IUnitOfWork unitOfWork)
    {
        _evidenceDocuments = evidenceDocuments ?? throw new ArgumentNullException(nameof(evidenceDocuments));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<BidOpsSupplierMaintenanceResult> RunEvidenceExpiryScanAsync(
        int maxItems,
        int warningDays,
        CancellationToken ct = default)
    {
        maxItems = Math.Clamp(maxItems <= 0 ? 100 : maxItems, 1, 1000);
        warningDays = Math.Clamp(warningDays <= 0 ? 30 : warningDays, 1, 365);
        var now = DateTime.UtcNow;
        var warningUntil = now.AddDays(warningDays);
        var query = await _evidenceDocuments.QueryDataScopeTrackingAsync(
            BidOpsDataResources.SupplierEvidence,
            AtlasDataScopeType.AllTenant,
            ct);
        var documents = await query
            .Where(x =>
                x.Status != BidOpsSupplierEvidenceStatuses.Archived &&
                x.ValidTo.HasValue &&
                x.ValidTo.Value <= warningUntil)
            .OrderBy(x => x.ValidTo)
            .Take(maxItems)
            .ToListAsync(ct);

        var updated = 0;
        foreach (var document in documents)
        {
            var targetStatus = document.ValidTo!.Value < now
                ? BidOpsSupplierEvidenceStatuses.Expired
                : BidOpsSupplierEvidenceStatuses.ExpiringSoon;
            if (document.Status == targetStatus)
                continue;

            document.Status = targetStatus;
            document.UpdatedAt = now;
            updated++;
        }

        if (updated > 0)
            await _unitOfWork.SaveChangesAsync(ct);

        return new BidOpsSupplierMaintenanceResult(
            documents.Count(),
            documents.Count(),
            updated,
            $"supplier evidence expiry scan completed;warningDays={warningDays}");
    }
}
