namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsAiParsingService
{
    Task<long> ParseRawNoticeAsync(long rawNoticeId, CancellationToken ct = default);

    Task<long> ParseRawNoticeAsync(long rawNoticeId, string? reviewerPrompt, CancellationToken ct = default);
}
