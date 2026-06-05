using Atlas.Infrastructure.Security;
using Atlas.Exporting;
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
    private readonly IExportJobService _exports;

    public TenantRecordsController(
        ITenantRecordService records,
        ITenantRecordQueryService queries,
        IExportJobService exports)
    {
        _records = records ?? throw new ArgumentNullException(nameof(records));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _exports = exports ?? throw new ArgumentNullException(nameof(exports));
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

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + ModuleTemplatePermissionCodes.TenantRecordsExport)]
    [HttpPost("exports")]
    public async Task<ActionResult<ExportEnqueueResult>> ExportAsync(
        [FromBody] ExportTenantRecordsRequest request,
        CancellationToken ct)
    {
        var result = await _exports.EnqueueAsync(
            new ExportEnqueueRequest<ExportTenantRecordsRequest>
            {
                ExportTaskType = ModuleTemplateExportTaskTypes.TenantRecordList,
                Query = request
            },
            ct);

        return Accepted($"/api/tenant-records/exports/{result.ExportJobId}", result);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + ModuleTemplatePermissionCodes.TenantRecordsExport)]
    [HttpGet("exports/{exportJobId:long}")]
    public async Task<ActionResult<ExportJobStatusDto>> GetExportAsync(
        long exportJobId,
        CancellationToken ct)
    {
        var result = await _exports.GetAsync(exportJobId, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + ModuleTemplatePermissionCodes.TenantRecordsExport)]
    [HttpGet("exports/{exportJobId:long}/download")]
    public async Task<IActionResult> DownloadExportAsync(
        long exportJobId,
        CancellationToken ct)
    {
        var result = await _exports.OpenDownloadAsync(exportJobId, ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    private async Task<IActionResult> ExecuteSearchAsync(TenantRecordSearchQuery query, CancellationToken ct)
    {
        var result = await _queries.SearchAsync(query, ct);
        return Ok(result);
    }
}
