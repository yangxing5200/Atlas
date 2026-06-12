using Atlas.BackgroundTasks.Operations;
using Atlas.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/ops/workers")]
public sealed class BackgroundWorkersOperationsController : ControllerBase
{
    private readonly IBackgroundWorkerOperationsService _workers;

    public BackgroundWorkersOperationsController(IBackgroundWorkerOperationsService workers)
    {
        _workers = workers ?? throw new ArgumentNullException(nameof(workers));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] BackgroundWorkerSearchQuery query, CancellationToken ct)
    {
        return Ok(await _workers.SearchAsync(query, ct));
    }
}
