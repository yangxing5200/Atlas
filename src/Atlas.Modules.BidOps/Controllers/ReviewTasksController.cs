using Atlas.BackgroundTasks.Operations;
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
    private readonly IBackgroundJobOperationsService _jobs;

    public ReviewTasksController(
        IBidOpsQueryService queries,
        IBidOpsReviewService review,
        IBackgroundJobOperationsService jobs)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _review = review ?? throw new ArgumentNullException(nameof(review));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewRead)]
    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] ReviewTaskSearchQuery query, CancellationToken ct)
    {
        return Ok(await _queries.SearchReviewTasksAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewRead)]
    [HttpGet("corrections/analysis")]
    public async Task<ActionResult<ReviewCorrectionAnalysisDto>> GetCorrectionAnalysisAsync(
        [FromQuery] ReviewCorrectionAnalysisQuery query,
        CancellationToken ct)
    {
        return Ok(await _queries.GetReviewCorrectionAnalysisAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewRead)]
    [HttpGet("efficiency-metrics")]
    public async Task<ActionResult<ReviewEfficiencyMetricsDto>> GetEfficiencyMetricsAsync(CancellationToken ct)
    {
        return Ok(await _queries.GetReviewEfficiencyMetricsAsync(ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("bulk-approve")]
    public async Task<ActionResult<BulkReviewTaskActionResultDto>> BulkApproveAsync(
        [FromBody] BulkApproveReviewTasksRequest request,
        CancellationToken ct)
    {
        return Ok(await _review.BulkApproveAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("bulk-approve/job")]
    public async Task<ActionResult<EnqueueJobDto>> EnqueueBulkApproveAsync(
        [FromBody] BulkApproveReviewTasksRequest request,
        CancellationToken ct)
    {
        return Accepted(await _review.EnqueueBulkApproveAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("batch-reparse")]
    public async Task<ActionResult<BulkReviewTaskActionResultDto>> BatchReparseAsync(
        [FromBody] BatchReviewTaskReparseRequest request,
        CancellationToken ct)
    {
        return Accepted(await _review.BatchReparseAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("quality-backfill")]
    public async Task<ActionResult<EnqueueJobDto>> EnqueueQualityBackfillAsync(
        [FromBody] ReviewQualityBackfillRequest request,
        CancellationToken ct)
    {
        return Accepted(await _review.EnqueueReviewQualityBackfillAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewRead)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ReviewTaskDetailDto>> GetAsync(long id, CancellationToken ct)
    {
        var detail = await _queries.GetReviewTaskDetailAsync(id, ct);
        return detail == null ? NotFound() : Ok(detail);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewRead)]
    [HttpGet("{id:long}/jobs")]
    public async Task<IActionResult> JobsAsync(
        long id,
        [FromQuery] BackgroundJobSearchQuery query,
        CancellationToken ct)
    {
        var jobIds = await _queries.GetReviewTaskBackgroundJobIdsAsync(id, ct);
        if (jobIds.Count == 0)
            return Ok(new Atlas.Models.Tenant.Responses.PagedResult<BackgroundJobListItemDto>(
                0,
                [],
                query.PageIndex < 1 ? 1 : query.PageIndex,
                query.PageSize < 1 ? 20 : Math.Min(query.PageSize, 200)));

        return Ok(await _jobs.SearchByIdsAsync(jobIds, query, bidOpsOnly: true, ct));
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

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("{id:long}/outcome-ai-reparse")]
    public async Task<ActionResult<EnqueueJobDto>> OutcomeAiReparseAsync(
        long id,
        [FromBody] ReviewOutcomeAiReparseRequest request,
        CancellationToken ct)
    {
        return Accepted(await _review.EnqueueOutcomeAiReparseAsync(id, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("{id:long}/outcome-suppliers")]
    public async Task<ActionResult<OutcomeSupplierRecordDto>> AddOutcomeSupplierAsync(
        long id,
        [FromBody] ReviewOutcomeSupplierRecordEditRequest request,
        CancellationToken ct)
    {
        return Ok(await _review.AddOutcomeSupplierRecordAsync(id, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPut("{id:long}/outcome-suppliers/{recordId:long}")]
    public async Task<ActionResult<OutcomeSupplierRecordDto>> UpdateOutcomeSupplierAsync(
        long id,
        long recordId,
        [FromBody] ReviewOutcomeSupplierRecordEditRequest request,
        CancellationToken ct)
    {
        return Ok(await _review.UpdateOutcomeSupplierRecordAsync(id, recordId, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpDelete("{id:long}/outcome-suppliers/{recordId:long}")]
    public async Task<IActionResult> DeleteOutcomeSupplierAsync(
        long id,
        long recordId,
        CancellationToken ct)
    {
        await _review.DeleteOutcomeSupplierRecordAsync(id, recordId, ct);
        return NoContent();
    }
}
