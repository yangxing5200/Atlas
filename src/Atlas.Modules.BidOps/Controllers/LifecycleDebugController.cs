using Atlas.Infrastructure.Security;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/lifecycle/debug")]
public sealed class LifecycleDebugController : ControllerBase
{
    private readonly IBidOpsReverseLifecycleClosureService _closure;
    private readonly IBidOpsAmountCandidateService _amountCandidates;

    public LifecycleDebugController(
        IBidOpsReverseLifecycleClosureService closure,
        IBidOpsAmountCandidateService amountCandidates)
    {
        _closure = closure ?? throw new ArgumentNullException(nameof(closure));
        _amountCandidates = amountCandidates ?? throw new ArgumentNullException(nameof(amountCandidates));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewRead)]
    [HttpGet("links")]
    public async Task<ActionResult<PagedResult<LifecyclePackageLinkDto>>> SearchLifecycleLinksAsync(
        [FromQuery] LifecyclePackageLinkSearchQuery query,
        CancellationToken ct)
    {
        return Ok(await _closure.SearchLifecycleLinksAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewRead)]
    [HttpGet("links/{linkId:long}/amount-candidates")]
    public async Task<ActionResult<IReadOnlyList<AmountCandidateDto>>> ListAmountCandidatesAsync(
        long linkId,
        CancellationToken ct)
    {
        return Ok(await _amountCandidates.EnsureLifecycleAmountCandidatesAsync(linkId, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewRead)]
    [HttpGet("links/{linkId:long}/amount-candidates/debug")]
    public async Task<ActionResult<LifecycleAmountCandidateDebugDto>> DiagnoseAmountCandidatesAsync(
        long linkId,
        CancellationToken ct)
    {
        return Ok(await _amountCandidates.DiagnoseLifecycleAmountCandidatesAsync(linkId, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("links/{linkId:long}/amount-candidates/{candidateId:long}/select")]
    public async Task<ActionResult<AmountCandidateOperationResultDto>> SelectAmountCandidateAsync(
        long linkId,
        long candidateId,
        [FromBody] AmountCandidateSelectRequest? request,
        CancellationToken ct)
    {
        return Ok(await _amountCandidates.SelectCandidateAsync(
            linkId,
            candidateId,
            request ?? new AmountCandidateSelectRequest(),
            ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("links/{linkId:long}/amount-candidates/{candidateId:long}/mark-type")]
    public async Task<ActionResult<AmountCandidateOperationResultDto>> MarkAmountCandidateTypeAsync(
        long linkId,
        long candidateId,
        [FromBody] AmountCandidateMarkTypeRequest request,
        CancellationToken ct)
    {
        return Ok(await _amountCandidates.MarkCandidateTypeAsync(linkId, candidateId, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("links/{linkId:long}/amount-candidates/{candidateId:long}/reject")]
    public async Task<ActionResult<AmountCandidateOperationResultDto>> RejectAmountCandidateAsync(
        long linkId,
        long candidateId,
        [FromBody] AmountCandidateRejectRequest request,
        CancellationToken ct)
    {
        return Ok(await _amountCandidates.RejectCandidateAsync(linkId, candidateId, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("links/{linkId:long}/amount-candidates/{candidateId:long}/restore")]
    public async Task<ActionResult<AmountCandidateOperationResultDto>> RestoreAmountCandidateAsync(
        long linkId,
        long candidateId,
        [FromBody] AmountCandidateRestoreRequest? request,
        CancellationToken ct)
    {
        return Ok(await _amountCandidates.RestoreCandidateAsync(
            linkId,
            candidateId,
            request ?? new AmountCandidateRestoreRequest(),
            ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("links/final-award-amount/clear")]
    public async Task<ActionResult<LifecycleFinalAwardAmountClearResultDto>> ClearFinalAwardAmountsAsync(
        [FromBody] LifecycleFinalAwardAmountClearRequest request,
        CancellationToken ct)
    {
        return Ok(await _amountCandidates.ClearFinalAwardAmountsAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewRead)]
    [HttpGet("links/{linkId:long}/procurement-candidates")]
    public async Task<ActionResult<IReadOnlyList<LifecycleProcurementNoticeCandidateDto>>> SearchProcurementNoticeCandidatesAsync(
        long linkId,
        CancellationToken ct)
    {
        return Ok(await _closure.SearchProcurementNoticeCandidatesAsync(linkId, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlImport)]
    [HttpPost("links/{linkId:long}/procurement-candidates/import")]
    public async Task<ActionResult<LifecycleProcurementNoticeImportResultDto>> ImportProcurementNoticeCandidateAsync(
        long linkId,
        [FromBody] LifecycleProcurementNoticeImportRequest request,
        CancellationToken ct)
    {
        return Accepted(await _closure.ImportProcurementNoticeCandidateAsync(linkId, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlImport)]
    [HttpPost("award-notices/{rawNoticeId:long}/procurement-auto-collect")]
    public async Task<ActionResult<LifecycleProcurementAutoCollectResultDto>> AutoCollectProcurementNoticeAsync(
        long rawNoticeId,
        [FromBody] LifecycleProcurementAutoCollectRequest? request,
        CancellationToken ct)
    {
        return Ok(await _closure.AutoCollectProcurementNoticesForAwardAsync(
            rawNoticeId,
            request ?? new LifecycleProcurementAutoCollectRequest(),
            null,
            ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("links/{linkId:long}/project-code")]
    public async Task<ActionResult<LifecycleProjectCodeUpdateResultDto>> UpdateLifecycleProjectCodeAsync(
        long linkId,
        [FromBody] LifecycleProjectCodeUpdateRequest request,
        CancellationToken ct)
    {
        return Ok(await _closure.UpdateLifecycleProjectCodeAsync(linkId, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("links/{linkId:long}/field-enrichment/enqueue")]
    public async Task<ActionResult<EnqueueJobDto>> EnqueueFieldEnrichmentAsync(
        long linkId,
        [FromBody] LifecycleFieldEnrichmentRequest request,
        CancellationToken ct)
    {
        return Accepted(await _closure.EnqueueLifecycleFieldEnrichmentAsync(linkId, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("award-notices/{rawNoticeId:long}/outcome-supplier-reparse/enqueue")]
    public async Task<ActionResult<EnqueueJobDto>> EnqueueOutcomeSupplierReparseAsync(
        long rawNoticeId,
        [FromBody] LifecycleOutcomeSupplierReparseRequest? request,
        CancellationToken ct)
    {
        return Accepted(await _closure.EnqueueOutcomeSupplierReparseAsync(
            rawNoticeId,
            request ?? new LifecycleOutcomeSupplierReparseRequest(),
            ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlImport)]
    [HttpPost("reverse-close-url")]
    public async Task<ActionResult<BidOpsReverseClosureDebugResult>> ReverseCloseUrlAsync(
        [FromBody] BidOpsReverseCloseUrlRequest request,
        CancellationToken ct)
    {
        return Ok(await _closure.ReverseCloseUrlAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlImport)]
    [HttpPost("reverse-close/enqueue")]
    public async Task<ActionResult<EnqueueJobDto>> EnqueueReverseCloseAsync(
        [FromBody] BidOpsReverseCloseJobRequest request,
        CancellationToken ct)
    {
        return Accepted(await _closure.EnqueueReverseClosureAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpPost("reverse-close-raw-notice/{rawNoticeId:long}")]
    public async Task<ActionResult<BidOpsReverseClosureDebugResult>> ReverseCloseRawNoticeAsync(
        long rawNoticeId,
        CancellationToken ct)
    {
        return Ok(await _closure.ReverseCloseRawNoticeAsync(rawNoticeId, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("reverse-close-raw-notice/{rawNoticeId:long}/persist")]
    public async Task<ActionResult<BidOpsReverseClosureDebugResult>> ReverseCloseRawNoticeAndPersistAsync(
        long rawNoticeId,
        CancellationToken ct)
    {
        return Ok(await _closure.ReverseCloseRawNoticeAndPersistAsync(rawNoticeId, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("links/{linkId:long}/confirm")]
    public async Task<ActionResult<LifecyclePackageLinkDto>> ConfirmLifecycleLinkAsync(
        long linkId,
        [FromBody] LifecyclePackageLinkDecisionRequest request,
        CancellationToken ct)
    {
        return Ok(await _closure.ConfirmLifecycleLinkAsync(linkId, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("links/batch-review")]
    public async Task<ActionResult<LifecyclePackageLinkBatchReviewResultDto>> BatchReviewLifecycleLinksAsync(
        [FromBody] LifecyclePackageLinkBatchReviewRequest request,
        CancellationToken ct)
    {
        return Ok(await _closure.BatchReviewLifecycleLinksAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("award-notices/{rawNoticeId:long}/auto-review")]
    public async Task<ActionResult<LifecyclePackageLinkBatchReviewResultDto>> AutoReviewLifecycleLinksAsync(
        long rawNoticeId,
        CancellationToken ct)
    {
        return Ok(await _closure.AutoReviewLifecycleLinksForAwardAsync(rawNoticeId, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("links/{linkId:long}/reject")]
    public async Task<ActionResult<LifecyclePackageLinkDto>> RejectLifecycleLinkAsync(
        long linkId,
        [FromBody] LifecyclePackageLinkDecisionRequest request,
        CancellationToken ct)
    {
        return Ok(await _closure.RejectLifecycleLinkAsync(linkId, request, ct));
    }
}
