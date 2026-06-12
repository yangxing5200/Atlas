using Atlas.Infrastructure.Security;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/pursuits")]
public sealed class PursuitsController : ControllerBase
{
    private readonly IBidOpsPursuitService _pursuits;

    public PursuitsController(IBidOpsPursuitService pursuits)
    {
        _pursuits = pursuits ?? throw new ArgumentNullException(nameof(pursuits));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.PursuitRead)]
    [HttpGet]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] PursuitSearchQuery query,
        CancellationToken ct)
    {
        return Ok(await _pursuits.SearchAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.PursuitManage)]
    [HttpPost]
    public async Task<ActionResult<PursuitDto>> CreateAsync(
        [FromBody] CreatePursuitRequest request,
        CancellationToken ct)
    {
        var created = await _pursuits.CreateAsync(request, ct);
        return Created($"/api/bidops/pursuits/{created.Id}", created);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.PursuitRead)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<PursuitDetailDto>> GetAsync(
        long id,
        CancellationToken ct)
    {
        var pursuit = await _pursuits.GetAsync(id, ct);
        return pursuit == null ? NotFound() : Ok(pursuit);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.PursuitManage)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<PursuitDto>> UpdateAsync(
        long id,
        [FromBody] UpdatePursuitRequest request,
        CancellationToken ct)
    {
        return Ok(await _pursuits.UpdateAsync(id, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.PursuitManage)]
    [HttpPost("{id:long}/status")]
    public async Task<ActionResult<PursuitDto>> ChangeStatusAsync(
        long id,
        [FromBody] ChangePursuitStatusRequest request,
        CancellationToken ct)
    {
        return Ok(await _pursuits.ChangeStatusAsync(id, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.PursuitRead)]
    [HttpGet("{id:long}/tasks")]
    public async Task<ActionResult<IReadOnlyList<PursuitTaskDto>>> TasksAsync(
        long id,
        CancellationToken ct)
    {
        return Ok(await _pursuits.ListTasksAsync(id, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.PursuitTaskManage)]
    [HttpPost("{id:long}/tasks")]
    public async Task<ActionResult<PursuitTaskDto>> CreateTaskAsync(
        long id,
        [FromBody] CreatePursuitTaskRequest request,
        CancellationToken ct)
    {
        var task = await _pursuits.CreateTaskAsync(id, request, ct);
        return Created($"/api/bidops/pursuits/{id}/tasks/{task.Id}", task);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.PursuitTaskManage)]
    [HttpPut("{id:long}/tasks/{taskId:long}")]
    public async Task<ActionResult<PursuitTaskDto>> UpdateTaskAsync(
        long id,
        long taskId,
        [FromBody] UpdatePursuitTaskRequest request,
        CancellationToken ct)
    {
        return Ok(await _pursuits.UpdateTaskAsync(id, taskId, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.PursuitRead)]
    [HttpGet("{id:long}/follow-records")]
    public async Task<ActionResult<IReadOnlyList<PursuitFollowRecordDto>>> FollowRecordsAsync(
        long id,
        CancellationToken ct)
    {
        return Ok(await _pursuits.ListFollowRecordsAsync(id, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.PursuitFollowRecordManage)]
    [HttpPost("{id:long}/follow-records")]
    public async Task<ActionResult<PursuitFollowRecordDto>> CreateFollowRecordAsync(
        long id,
        [FromBody] CreatePursuitFollowRecordRequest request,
        CancellationToken ct)
    {
        var record = await _pursuits.CreateFollowRecordAsync(id, request, ct);
        return Created($"/api/bidops/pursuits/{id}/follow-records/{record.Id}", record);
    }
}
