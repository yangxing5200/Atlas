using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsMatchingService
{
    Task<StartSupplierMatchRunResponse> StartSupplierMatchRunAsync(
        long packageId,
        StartSupplierMatchRunRequest request,
        CancellationToken ct = default);

    Task<PagedResult<SupplierMatchRunDto>> SearchRunsAsync(
        SupplierMatchRunSearchQuery query,
        CancellationToken ct = default);

    Task<SupplierMatchRunDetailDto?> GetRunAsync(long runId, CancellationToken ct = default);

    Task<IReadOnlyList<SupplierMatchResultDto>> ListRunResultsAsync(long runId, CancellationToken ct = default);

    Task<BidOpsSupplierMatchExecutionResult> ExecuteSupplierMatchRunAsync(
        long runId,
        int maxSuppliers,
        CancellationToken ct = default);

    Task<GoNoGoDecisionDto> CreateDecisionAsync(
        long packageId,
        CreateGoNoGoDecisionRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<GoNoGoDecisionDto>> ListDecisionsAsync(
        long packageId,
        CancellationToken ct = default);
}

public sealed record BidOpsSupplierMatchExecutionResult(
    int SupplierCount,
    int MatchedCount,
    int MissingEvidenceCount,
    string Summary)
{
    public string ToJobResult()
    {
        return $"supplierCount={SupplierCount};matched={MatchedCount};missingEvidence={MissingEvidenceCount};summary={Summary}";
    }
}
