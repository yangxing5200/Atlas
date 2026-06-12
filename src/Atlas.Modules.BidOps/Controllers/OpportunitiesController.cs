using Atlas.Infrastructure.Security;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/opportunities")]
public sealed class OpportunitiesController : ControllerBase
{
    private readonly IBidOpsOpportunityService _opportunities;

    public OpportunitiesController(IBidOpsOpportunityService opportunities)
    {
        _opportunities = opportunities ?? throw new ArgumentNullException(nameof(opportunities));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessRead)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<OpportunityDto>>> SearchAsync(
        [FromQuery] OpportunitySearchQuery query,
        CancellationToken ct)
    {
        return Ok(await _opportunities.SearchAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessRead)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<OpportunityDetailDto>> GetAsync(long id, CancellationToken ct)
    {
        var opportunity = await _opportunities.GetAsync(id, ct);
        return opportunity == null ? NotFound() : Ok(opportunity);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpportunityManage)]
    [HttpPost]
    public async Task<ActionResult<OpportunityDto>> CreateAsync(
        [FromBody] CreateOpportunityRequest request,
        CancellationToken ct)
    {
        var created = await _opportunities.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = created.Id }, created);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpportunityManage)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<OpportunityDto>> UpdateAsync(
        long id,
        [FromBody] UpdateOpportunityRequest request,
        CancellationToken ct)
    {
        return Ok(await _opportunities.UpdateAsync(id, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpportunityWatch)]
    [HttpPost("{id:long}/watch")]
    public async Task<ActionResult<OpportunityDto>> WatchAsync(
        long id,
        [FromBody] WatchOpportunityRequest request,
        CancellationToken ct)
    {
        return Ok(await _opportunities.WatchAsync(id, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpportunityAssess)]
    [HttpPost("{id:long}/assess")]
    public async Task<ActionResult<OpportunityDto>> AssessAsync(
        long id,
        [FromBody] AssessOpportunityRequest request,
        CancellationToken ct)
    {
        return Ok(await _opportunities.AssessAsync(id, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.OpportunityManage)]
    [HttpPost("{id:long}/stage")]
    public async Task<ActionResult<OpportunityDto>> ChangeStageAsync(
        long id,
        [FromBody] ChangeOpportunityStageRequest request,
        CancellationToken ct)
    {
        return Ok(await _opportunities.ChangeStageAsync(id, request, ct));
    }
}
