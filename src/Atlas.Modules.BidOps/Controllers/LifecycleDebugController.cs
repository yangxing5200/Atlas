using Atlas.Infrastructure.Security;
using Atlas.Modules.BidOps.Ai.Evidence;
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

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlImport)]
    [HttpPost("reverse-close-url")]
    public async Task<ActionResult<BidOpsReverseClosureDebugResult>> ReverseCloseUrlAsync(
        [FromBody] BidOpsReverseCloseUrlRequest request,
        CancellationToken ct)
    {
        return Ok(await _closure.ReverseCloseUrlAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpPost("reverse-close-raw-notice/{rawNoticeId:long}")]
    public async Task<ActionResult<BidOpsReverseClosureDebugResult>> ReverseCloseRawNoticeAsync(
        long rawNoticeId,
        CancellationToken ct)
    {
        return Ok(await _closure.ReverseCloseRawNoticeAsync(rawNoticeId, ct));
    }
}
