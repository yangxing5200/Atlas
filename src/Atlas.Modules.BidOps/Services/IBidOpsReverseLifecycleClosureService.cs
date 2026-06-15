using Atlas.Modules.BidOps.Ai.Evidence;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsReverseLifecycleClosureService
{
    Task<BidOpsReverseClosureDebugResult> ReverseCloseUrlAsync(
        BidOpsReverseCloseUrlRequest request,
        CancellationToken ct = default);

    Task<BidOpsReverseClosureDebugResult> ReverseCloseRawNoticeAsync(
        long rawNoticeId,
        CancellationToken ct = default);
}
