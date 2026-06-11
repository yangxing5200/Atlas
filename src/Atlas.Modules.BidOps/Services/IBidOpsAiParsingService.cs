namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsAiParsingService
{
    Task<long> ParseRawNoticeAsync(long rawNoticeId, CancellationToken ct = default);
}
