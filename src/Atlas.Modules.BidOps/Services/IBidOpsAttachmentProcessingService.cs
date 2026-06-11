namespace Atlas.Modules.BidOps.Services;

public sealed record BidOpsAttachmentProcessingResult(
    long RawNoticeId,
    int Total,
    int Downloaded,
    int Extracted,
    int Failed);

public interface IBidOpsAttachmentProcessingService
{
    Task<BidOpsAttachmentProcessingResult> ProcessRawNoticeAttachmentsAsync(
        long rawNoticeId,
        CancellationToken ct = default);
}
