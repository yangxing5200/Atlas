using Atlas.Infrastructure.Security;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/matching/runs")]
public sealed class MatchingController : ControllerBase
{
    private readonly IBidOpsMatchingService _matching;

    public MatchingController(IBidOpsMatchingService matching)
    {
        _matching = matching ?? throw new ArgumentNullException(nameof(matching));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.MatchingRead)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<SupplierMatchRunDto>>> SearchAsync(
        [FromQuery] SupplierMatchRunSearchQuery query,
        CancellationToken ct)
    {
        return Ok(await _matching.SearchRunsAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.MatchingRead)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<SupplierMatchRunDetailDto>> GetAsync(long id, CancellationToken ct)
    {
        var run = await _matching.GetRunAsync(id, ct);
        return run == null ? NotFound() : Ok(run);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.MatchingRead)]
    [HttpGet("{id:long}/results")]
    public async Task<ActionResult<IReadOnlyList<SupplierMatchResultDto>>> ResultsAsync(
        long id,
        CancellationToken ct)
    {
        return Ok(await _matching.ListRunResultsAsync(id, ct));
    }
}
