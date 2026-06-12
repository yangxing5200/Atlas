using Atlas.BackgroundTasks.Operations;
using Atlas.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/ops/background-jobs")]
public sealed class BackgroundJobsOperationsController : ControllerBase
{
    private readonly IBackgroundJobOperationsService _jobs;

    public BackgroundJobsOperationsController(IBackgroundJobOperationsService jobs)
    {
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] BackgroundJobSearchQuery query, CancellationToken ct)
    {
        return Ok(await _jobs.SearchAsync(query, ct: ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("summary")]
    public async Task<ActionResult<BackgroundJobSummaryDto>> SummaryAsync(
        [FromQuery] BackgroundJobSearchQuery query,
        CancellationToken ct)
    {
        return Ok(await _jobs.GetSummaryAsync(query, ct: ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<BackgroundJobDetailDto>> GetAsync(long id, CancellationToken ct)
    {
        var job = await _jobs.GetAsync(id, ct: ct);
        return job == null ? NotFound() : Ok(job);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlManage)]
    [HttpPost("{id:long}/retry")]
    public async Task<ActionResult<BackgroundJobRetryResultDto>> RetryAsync(long id, CancellationToken ct)
    {
        try
        {
            var result = await _jobs.RetryAsync(id, ct: ct);
            return result == null ? NotFound() : Accepted(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlManage)]
    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult<BackgroundJobCancelResultDto>> CancelAsync(
        long id,
        [FromBody] BackgroundJobCancelRequest? request,
        CancellationToken ct)
    {
        try
        {
            var result = await _jobs.CancelAsync(id, request ?? new BackgroundJobCancelRequest(), ct: ct);
            return result == null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
