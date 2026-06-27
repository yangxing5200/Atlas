using Atlas.BackgroundTasks.Operations;
using Atlas.Infrastructure.Security;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Queries;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/operations")]
public sealed class BidOpsOperationsController : ControllerBase
{
    private readonly IBidOpsOperationsQueryService _operations;
    private readonly IBidOpsQueryService _bidOpsQueries;
    private readonly IBidOpsAiSettingsService _aiSettings;
    private readonly IBidOpsRuntimeControlService _runtimeControl;
    private readonly IBackgroundJobOperationsService _jobs;

    public BidOpsOperationsController(
        IBidOpsOperationsQueryService operations,
        IBidOpsQueryService bidOpsQueries,
        IBidOpsAiSettingsService aiSettings,
        IBidOpsRuntimeControlService runtimeControl,
        IBackgroundJobOperationsService jobs)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        _bidOpsQueries = bidOpsQueries ?? throw new ArgumentNullException(nameof(bidOpsQueries));
        _aiSettings = aiSettings ?? throw new ArgumentNullException(nameof(aiSettings));
        _runtimeControl = runtimeControl ?? throw new ArgumentNullException(nameof(runtimeControl));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("dashboard")]
    public async Task<ActionResult<BidOpsOperationsDashboardDto>> DashboardAsync(CancellationToken ct)
    {
        return Ok(await _operations.GetDashboardAsync(ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("jobs")]
    public async Task<IActionResult> JobsAsync([FromQuery] BackgroundJobSearchQuery query, CancellationToken ct)
    {
        return Ok(await _operations.SearchJobsAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("config-check")]
    public async Task<ActionResult<BidOpsConfigCheckDto>> ConfigCheckAsync(CancellationToken ct)
    {
        return Ok(await _operations.GetConfigCheckAsync(ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpsRead)]
    [HttpGet("ai-settings")]
    public async Task<ActionResult<BidOpsAiProviderSettingsDto>> AiSettingsAsync(CancellationToken ct)
    {
        return Ok(await _aiSettings.GetSettingsAsync(ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpsManage)]
    [HttpPut("ai-settings/provider")]
    public async Task<ActionResult<BidOpsAiProviderSettingsDto>> UpdateAiProviderAsync(
        [FromBody] UpdateBidOpsAiProviderRequest request,
        CancellationToken ct)
    {
        return Ok(await _aiSettings.SetProviderAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpsManage)]
    [HttpPut("ai-settings/codex-cli")]
    public async Task<ActionResult<BidOpsAiProviderSettingsDto>> UpdateCodexCliSettingsAsync(
        [FromBody] UpdateBidOpsCodexCliSettingsRequest request,
        CancellationToken ct)
    {
        return Ok(await _aiSettings.SetCodexCliSettingsAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpsManage)]
    [HttpPut("ai-settings/codex-cli/scenario")]
    public async Task<ActionResult<BidOpsAiProviderSettingsDto>> UpdateCodexCliScenarioSettingsAsync(
        [FromBody] UpdateBidOpsCodexCliScenarioSettingsRequest request,
        CancellationToken ct)
    {
        return Ok(await _aiSettings.SetCodexCliScenarioSettingsAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpsManage)]
    [HttpPut("ai-settings/deepseek-token")]
    public async Task<ActionResult<BidOpsAiProviderSettingsDto>> UpdateDeepSeekTokenAsync(
        [FromBody] UpdateBidOpsDeepSeekTokenRequest request,
        CancellationToken ct)
    {
        return Ok(await _aiSettings.SetDeepSeekTokenAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpsManage)]
    [HttpPost("ai-settings/deepseek-token/test")]
    public async Task<ActionResult<BidOpsDeepSeekTokenTestResultDto>> TestDeepSeekTokenAsync(
        [FromBody] TestBidOpsDeepSeekTokenRequest request,
        CancellationToken ct)
    {
        return Ok(await _aiSettings.TestDeepSeekTokenAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpsManage)]
    [HttpPut("ai-settings/mimo-token")]
    public async Task<ActionResult<BidOpsAiProviderSettingsDto>> UpdateMimoTokenAsync(
        [FromBody] UpdateBidOpsMimoTokenRequest request,
        CancellationToken ct)
    {
        return Ok(await _aiSettings.SetMimoTokenAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpsManage)]
    [HttpPost("ai-settings/mimo-token/test")]
    public async Task<ActionResult<BidOpsMimoTokenTestResultDto>> TestMimoTokenAsync(
        [FromBody] TestBidOpsMimoTokenRequest request,
        CancellationToken ct)
    {
        return Ok(await _aiSettings.TestMimoTokenAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpsManage)]
    [HttpPut("runtime/task-pause")]
    public async Task<ActionResult<BidOpsRuntimeStatusDto>> UpdateTaskPauseAsync(
        [FromBody] UpdateBidOpsTaskPauseRequest request,
        CancellationToken ct)
    {
        return Ok(await _runtimeControl.SetTaskPauseAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("channels/health")]
    public async Task<ActionResult<IReadOnlyList<BidOpsChannelHealthDto>>> ChannelHealthAsync(CancellationToken ct)
    {
        return Ok(await _operations.GetChannelHealthAsync(ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("crawl-progress")]
    public async Task<ActionResult<IReadOnlyList<BidOpsCrawlProgressDto>>> CrawlProgressAsync(CancellationToken ct)
    {
        return Ok(await _operations.GetCrawlProgressAsync(ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("raw-notices/{id:long}/pipeline")]
    public async Task<ActionResult<RawNoticePipelineDto>> RawNoticePipelineAsync(long id, CancellationToken ct)
    {
        var pipeline = await _bidOpsQueries.GetRawNoticePipelineAsync(id, ct);
        return pipeline == null ? NotFound() : Ok(pipeline);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlManage)]
    [HttpPost("jobs/{id:long}/retry")]
    public async Task<ActionResult<BackgroundJobRetryResultDto>> RetryJobAsync(long id, CancellationToken ct)
    {
        try
        {
            var result = await _jobs.RetryAsync(id, bidOpsOnly: true, ct: ct);
            return result == null ? NotFound() : Accepted(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlManage)]
    [HttpPost("jobs/{id:long}/cancel")]
    public async Task<ActionResult<BackgroundJobCancelResultDto>> CancelJobAsync(
        long id,
        [FromBody] BackgroundJobCancelRequest? request,
        CancellationToken ct)
    {
        try
        {
            var result = await _jobs.CancelAsync(id, request ?? new BackgroundJobCancelRequest(), bidOpsOnly: true, ct: ct);
            return result == null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
