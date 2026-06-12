using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsSupplierService
{
    Task<PagedResult<SupplierDto>> SearchAsync(SupplierSearchQuery query, CancellationToken ct = default);

    Task<SupplierDetailDto?> GetAsync(long id, CancellationToken ct = default);

    Task<SupplierAnalysisSummaryDto> GetAnalysisSummaryAsync(CancellationToken ct = default);

    Task<PagedResult<OutcomeSupplierRecordDto>> SearchOutcomeRecordsAsync(
        OutcomeSupplierSearchQuery query,
        CancellationToken ct = default);

    Task<SupplierOutcomeSummaryDto> GetOutcomeSummaryAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PackageHistoricalSupplierLeadDto>> ListHistoricalSupplierLeadsAsync(
        long packageId,
        CancellationToken ct = default);

    Task<OutcomeSupplierBackfillEnqueueDto> EnqueueOutcomeSupplierBackfillAsync(
        int maxItems,
        CancellationToken ct = default);

    Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken ct = default);

    Task<SupplierDto> UpdateAsync(long id, UpdateSupplierRequest request, CancellationToken ct = default);

    Task<SupplierContactDto> AddContactAsync(long supplierId, CreateSupplierContactRequest request, CancellationToken ct = default);

    Task<SupplierCapabilityDto> AddCapabilityAsync(long supplierId, CreateSupplierCapabilityRequest request, CancellationToken ct = default);

    Task<SupplierEvidenceDocumentDto> AddEvidenceDocumentAsync(
        long supplierId,
        CreateSupplierEvidenceDocumentRequest request,
        CancellationToken ct = default);
}
