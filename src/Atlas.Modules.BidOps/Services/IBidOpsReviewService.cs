using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsReviewService
{
    Task<NoticeDto> ApproveAsync(
        long reviewTaskId,
        ReviewDecisionRequest request,
        CancellationToken ct = default);

    Task IgnoreAsync(
        long reviewTaskId,
        ReviewDecisionRequest request,
        CancellationToken ct = default);
}
