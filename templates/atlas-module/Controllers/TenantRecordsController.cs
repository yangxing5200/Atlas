using Atlas.Infrastructure.Security;
using Atlas.ModuleTemplate;
using Atlas.ModuleTemplate.Models;
using Atlas.ModuleTemplate.Queries;
using Atlas.ModuleTemplate.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.ModuleTemplate.Controllers;

[ApiController]
[Route("api/tenant-records")]
public sealed class TenantRecordsController : ControllerBase
{
    private readonly ITenantRecordService _records;
    private readonly ITenantRecordQueryService _queries;

    public TenantRecordsController(
        ITenantRecordService records,
        ITenantRecordQueryService queries)
    {
        _records = records ?? throw new ArgumentNullException(nameof(records));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + ModuleTemplatePermissionCodes.TenantRecordsRead)]
    [HttpGet]
    public Task<IActionResult> SearchAsync([FromQuery] TenantRecordSearchQuery query, CancellationToken ct)
    {
        return ExecuteSearchAsync(query, ct);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + ModuleTemplatePermissionCodes.TenantRecordsCreate)]
    [HttpPost]
    public async Task<ActionResult<TenantRecordDto>> CreateAsync(
        [FromBody] CreateTenantRecordRequest request,
        CancellationToken ct)
    {
        var created = await _records.CreateAsync(request, ct);
        return CreatedAtAction(nameof(SearchAsync), new { id = created.Id }, created);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + ModuleTemplatePermissionCodes.TenantRecordsUpdate)]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync(
        long id,
        [FromBody] UpdateTenantRecordRequest request,
        CancellationToken ct)
    {
        await _records.UpdateAsync(id, request, ct);
        return NoContent();
    }

    private async Task<IActionResult> ExecuteSearchAsync(TenantRecordSearchQuery query, CancellationToken ct)
    {
        var result = await _queries.SearchAsync(query, ct);
        return Ok(result);
    }
}
