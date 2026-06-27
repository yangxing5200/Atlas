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

    public LifecycleDebugController(IBidOpsReverseLifecycleClosureService closure)
    {
        _closure = closure ?? throw new ArgumentNullException(nameof(closure));
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

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("links/{linkId:long}/field-enrichment/enqueue")]
    public async Task<ActionResult<EnqueueJobDto>> EnqueueFieldEnrichmentAsync(
        long linkId,
        [FromBody] LifecycleFieldEnrichmentRequest request,
        CancellationToken ct)
    {
        return Accepted(await _closure.EnqueueLifecycleFieldEnrichmentAsync(linkId, request, ct));
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
    [HttpPost("links/{linkId:long}/reject")]
    public async Task<ActionResult<LifecyclePackageLinkDto>> RejectLifecycleLinkAsync(
        long linkId,
        [FromBody] LifecyclePackageLinkDecisionRequest request,
        CancellationToken ct)
    {
        return Ok(await _closure.RejectLifecycleLinkAsync(linkId, request, ct));
    }
}
