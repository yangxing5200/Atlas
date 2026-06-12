using Atlas.Infrastructure.Security;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Queries;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/packages")]
public sealed class PackagesController : ControllerBase
{
    private readonly IBidOpsQueryService _queries;
    private readonly IBidOpsMatchingService _matching;
    private readonly IBidOpsSupplierService _suppliers;

    public PackagesController(
        IBidOpsQueryService queries,
        IBidOpsMatchingService matching,
        IBidOpsSupplierService suppliers)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _matching = matching ?? throw new ArgumentNullException(nameof(matching));
        _suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessRead)]
    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] PackageSearchQuery query, CancellationToken ct)
    {
        return Ok(await _queries.SearchPackagesAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessRead)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<TenderPackageDto>> GetAsync(long id, CancellationToken ct)
    {
        var package = await _queries.GetPackageAsync(id, ct);
        return package == null ? NotFound() : Ok(package);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessRead)]
    [HttpGet("{id:long}/timeline")]
    public async Task<ActionResult<IReadOnlyList<PackageTimelineItemDto>>> TimelineAsync(long id, CancellationToken ct)
    {
        var package = await _queries.GetPackageAsync(id, ct);
        if (package == null)
            return NotFound();

        return Ok(await _queries.GetPackageTimelineAsync(id, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessRead)]
    [HttpGet("{id:long}/requirements")]
    public async Task<IActionResult> RequirementsAsync(long id, CancellationToken ct)
    {
        return Ok(await _queries.ListRequirementsAsync(id, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessRead)]
    [HttpGet("{id:long}/historical-suppliers")]
    public async Task<ActionResult<IReadOnlyList<PackageHistoricalSupplierLeadDto>>> HistoricalSuppliersAsync(
        long id,
        CancellationToken ct)
    {
        var package = await _queries.GetPackageAsync(id, ct);
        if (package == null)
            return NotFound();

        return Ok(await _suppliers.ListHistoricalSupplierLeadsAsync(id, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.MatchingRun)]
    [HttpPost("{id:long}/match-suppliers")]
    public async Task<ActionResult<StartSupplierMatchRunResponse>> MatchSuppliersAsync(
        long id,
        [FromBody] StartSupplierMatchRunRequest request,
        CancellationToken ct)
    {
        var response = await _matching.StartSupplierMatchRunAsync(id, request, ct);
        return Accepted($"/api/bidops/matching/runs/{response.Run.Id}", response);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.MatchingRead)]
    [HttpGet("{id:long}/decisions")]
    public async Task<ActionResult<IReadOnlyList<GoNoGoDecisionDto>>> DecisionsAsync(
        long id,
        CancellationToken ct)
    {
        return Ok(await _matching.ListDecisionsAsync(id, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.MatchingDecide)]
    [HttpPost("{id:long}/decisions")]
    public async Task<ActionResult<GoNoGoDecisionDto>> CreateDecisionAsync(
        long id,
        [FromBody] CreateGoNoGoDecisionRequest request,
        CancellationToken ct)
    {
        var decision = await _matching.CreateDecisionAsync(id, request, ct);
        return Created($"/api/bidops/packages/{id}/decisions/{decision.Id}", decision);
    }
}
