using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsOutcomeSupplierExtractionService
{
    Task<OutcomeSupplierExtractionResultDto> ExtractRawNoticeAsync(long rawNoticeId, CancellationToken ct = default);
}
