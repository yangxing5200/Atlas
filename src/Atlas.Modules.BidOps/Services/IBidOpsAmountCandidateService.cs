using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsAmountCandidateService
{
    Task<IReadOnlyList<AmountCandidateDto>> EnsureRawNoticeAmountCandidatesAsync(
        long rawNoticeId,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<long, IReadOnlyList<AmountCandidateDto>>> EnsureLifecycleAmountCandidatesAsync(
        IReadOnlyCollection<LifecyclePackageLinkDto> links,
        CancellationToken ct = default);

    Task<IReadOnlyList<AmountCandidateDto>> EnsureLifecycleAmountCandidatesAsync(
        long linkId,
        CancellationToken ct = default);

    Task<LifecycleAmountCandidateDebugDto> DiagnoseLifecycleAmountCandidatesAsync(
        long linkId,
        CancellationToken ct = default);

    Task<AmountCandidateOperationResultDto> SelectCandidateAsync(
        long linkId,
        long candidateId,
        AmountCandidateSelectRequest request,
        CancellationToken ct = default);

    Task<AmountCandidateOperationResultDto> MarkCandidateTypeAsync(
        long linkId,
        long candidateId,
        AmountCandidateMarkTypeRequest request,
        CancellationToken ct = default);

    Task<AmountCandidateOperationResultDto> RejectCandidateAsync(
        long linkId,
        long candidateId,
        AmountCandidateRejectRequest request,
        CancellationToken ct = default);

    Task<AmountCandidateOperationResultDto> RestoreCandidateAsync(
        long linkId,
        long candidateId,
        AmountCandidateRestoreRequest request,
        CancellationToken ct = default);

    Task<LifecycleFinalAwardAmountClearResultDto> ClearFinalAwardAmountsAsync(
        LifecycleFinalAwardAmountClearRequest request,
        CancellationToken ct = default);
}
