using Atlas.Infrastructure.Security;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/packages")]
public sealed class PackagesController : ControllerBase
{
    private readonly IBidOpsQueryService _queries;

    public PackagesController(IBidOpsQueryService queries)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessRead)]
    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] PackageSearchQuery query, CancellationToken ct)
    {
        return Ok(await _queries.SearchPackagesAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessRead)]
    [HttpGet("{id:long}/requirements")]
    public async Task<IActionResult> RequirementsAsync(long id, CancellationToken ct)
    {
        return Ok(await _queries.ListRequirementsAsync(id, ct));
    }
}
