using Atlas.Infrastructure.Security;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/dashboard")]
public sealed class BidOpsDashboardController : ControllerBase
{
    private readonly IBidOpsOperationsQueryService _operations;

    public BidOpsDashboardController(IBidOpsOperationsQueryService operations)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessRead)]
    [HttpGet("summary")]
    public async Task<ActionResult<BidOpsDashboardSummaryDto>> SummaryAsync(CancellationToken ct)
    {
        return Ok(await _operations.GetBusinessDashboardAsync(ct));
    }
}
