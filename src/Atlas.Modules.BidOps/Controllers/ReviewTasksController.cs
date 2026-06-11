using Atlas.Infrastructure.Security;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Queries;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/review-tasks")]
public sealed class ReviewTasksController : ControllerBase
{
    private readonly IBidOpsQueryService _queries;
    private readonly IBidOpsReviewService _review;

    public ReviewTasksController(
        IBidOpsQueryService queries,
        IBidOpsReviewService review)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _review = review ?? throw new ArgumentNullException(nameof(review));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewRead)]
    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] ReviewTaskSearchQuery query, CancellationToken ct)
    {
        return Ok(await _queries.SearchReviewTasksAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewRead)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ReviewTaskDetailDto>> GetAsync(long id, CancellationToken ct)
    {
        var detail = await _queries.GetReviewTaskDetailAsync(id, ct);
        return detail == null ? NotFound() : Ok(detail);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("{id:long}/approve")]
    public async Task<ActionResult<NoticeDto>> ApproveAsync(
        long id,
        [FromBody] ReviewDecisionRequest request,
        CancellationToken ct)
    {
        return Ok(await _review.ApproveAsync(id, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("{id:long}/ignore")]
    public async Task<IActionResult> IgnoreAsync(
        long id,
        [FromBody] ReviewDecisionRequest request,
        CancellationToken ct)
    {
        await _review.IgnoreAsync(id, request, ct);
        return NoContent();
    }
}
