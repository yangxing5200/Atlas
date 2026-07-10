using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsOutcomeSupplierExtractionService
{
    Task<OutcomeSupplierExtractionResultDto> ExtractRawNoticeAsync(long rawNoticeId, CancellationToken ct = default);

    Task<OutcomeSupplierExtractionResultDto> ExtractRawNoticeAsync(
        long rawNoticeId,
        string? reviewerPrompt,
        CancellationToken ct = default);

    Task<OutcomeSupplierRebuildDryRunResultDto> DryRunRawNoticeAsync(
        long rawNoticeId,
        string? reviewerPrompt,
        CancellationToken ct = default);
}
