using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Models;
using Atlas.Models.Tenant.Responses;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsReverseLifecycleClosureService
{
    Task<PagedResult<LifecyclePackageLinkDto>> SearchLifecycleLinksAsync(
        LifecyclePackageLinkSearchQuery query,
        CancellationToken ct = default);

    Task<IReadOnlyList<LifecycleProcurementNoticeCandidateDto>> SearchProcurementNoticeCandidatesAsync(
        long linkId,
        CancellationToken ct = default);

    Task<LifecycleProcurementNoticeImportResultDto> ImportProcurementNoticeCandidateAsync(
        long linkId,
        LifecycleProcurementNoticeImportRequest request,
        CancellationToken ct = default);

    Task<LifecycleProcurementAutoCollectResultDto> AutoCollectProcurementNoticesForAwardAsync(
        long awardRawNoticeId,
        LifecycleProcurementAutoCollectRequest request,
        long? backgroundJobId = null,
        CancellationToken ct = default);

    Task<LifecycleProjectCodeUpdateResultDto> UpdateLifecycleProjectCodeAsync(
        long linkId,
        LifecycleProjectCodeUpdateRequest request,
        CancellationToken ct = default);

    Task<EnqueueJobDto> EnqueueReverseClosureAsync(
        BidOpsReverseCloseJobRequest request,
        CancellationToken ct = default);

    Task<EnqueueJobDto> EnqueueLifecycleFieldEnrichmentAsync(
        long linkId,
        LifecycleFieldEnrichmentRequest request,
        CancellationToken ct = default);

    Task<EnqueueJobDto> EnqueueOutcomeSupplierReparseAsync(
        long rawNoticeId,
        LifecycleOutcomeSupplierReparseRequest request,
        CancellationToken ct = default);

    Task<LifecyclePackageLinkDto> EnrichLifecycleLinkFieldsAsync(
        long linkId,
        string? reviewerPrompt,
        CancellationToken ct = default);

    Task<BidOpsReverseClosureDebugResult> ReverseCloseUrlAsync(
        BidOpsReverseCloseUrlRequest request,
        CancellationToken ct = default);

    Task<BidOpsReverseClosureDebugResult> ReverseCloseRawNoticeAsync(
        long rawNoticeId,
        CancellationToken ct = default);

    Task<BidOpsReverseClosureDebugResult> ReverseCloseRawNoticeAndPersistAsync(
        long rawNoticeId,
        CancellationToken ct = default);

    Task<LifecyclePackageLinkDto> ConfirmLifecycleLinkAsync(
        long linkId,
        LifecyclePackageLinkDecisionRequest request,
        CancellationToken ct = default);

    Task<LifecyclePackageLinkBatchReviewResultDto> BatchReviewLifecycleLinksAsync(
        LifecyclePackageLinkBatchReviewRequest request,
        CancellationToken ct = default);

    Task<LifecyclePackageLinkBatchReviewResultDto> AutoReviewLifecycleLinksForAwardAsync(
        long awardRawNoticeId,
        CancellationToken ct = default);

    Task<LifecyclePackageLinkDto> RejectLifecycleLinkAsync(
        long linkId,
        LifecyclePackageLinkDecisionRequest request,
        CancellationToken ct = default);
}
