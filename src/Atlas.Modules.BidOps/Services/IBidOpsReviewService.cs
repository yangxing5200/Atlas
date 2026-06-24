using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsReviewService
{
    Task<NoticeDto> ApproveAsync(
        long reviewTaskId,
        ReviewDecisionRequest request,
        CancellationToken ct = default);

    Task<BulkReviewTaskActionResultDto> BulkApproveAsync(
        BulkApproveReviewTasksRequest request,
        CancellationToken ct = default);

    Task<BulkReviewTaskActionResultDto> BatchReparseAsync(
        BatchReviewTaskReparseRequest request,
        CancellationToken ct = default);

    Task<EnqueueJobDto> EnqueueReviewQualityBackfillAsync(
        ReviewQualityBackfillRequest request,
        CancellationToken ct = default);

    Task IgnoreAsync(
        long reviewTaskId,
        ReviewDecisionRequest request,
        CancellationToken ct = default);

    Task<EnqueueJobDto> EnqueueOutcomeAiReparseAsync(
        long reviewTaskId,
        ReviewOutcomeAiReparseRequest request,
        CancellationToken ct = default);

    Task<OutcomeSupplierRecordDto> AddOutcomeSupplierRecordAsync(
        long reviewTaskId,
        ReviewOutcomeSupplierRecordEditRequest request,
        CancellationToken ct = default);

    Task<OutcomeSupplierRecordDto> UpdateOutcomeSupplierRecordAsync(
        long reviewTaskId,
        long outcomeRecordId,
        ReviewOutcomeSupplierRecordEditRequest request,
        CancellationToken ct = default);

    Task DeleteOutcomeSupplierRecordAsync(
        long reviewTaskId,
        long outcomeRecordId,
        CancellationToken ct = default);

    Task<EnqueueJobDto> EnqueueRawNoticeReparseAsync(
        long rawNoticeId,
        ReparseRawNoticeRequest request,
        CancellationToken ct = default);
}
