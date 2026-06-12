using Atlas.Infrastructure.Security;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/suppliers")]
public sealed class SuppliersController : ControllerBase
{
    private readonly IBidOpsSupplierService _suppliers;

    public SuppliersController(IBidOpsSupplierService suppliers)
    {
        _suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.SupplierRead)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<SupplierDto>>> SearchAsync(
        [FromQuery] SupplierSearchQuery query,
        CancellationToken ct)
    {
        return Ok(await _suppliers.SearchAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.SupplierRead)]
    [HttpGet("analysis/summary")]
    public async Task<ActionResult<SupplierAnalysisSummaryDto>> AnalysisSummaryAsync(CancellationToken ct)
    {
        return Ok(await _suppliers.GetAnalysisSummaryAsync(ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.SupplierRead)]
    [HttpGet("outcome-records")]
    public async Task<ActionResult<PagedResult<OutcomeSupplierRecordDto>>> OutcomeRecordsAsync(
        [FromQuery] OutcomeSupplierSearchQuery query,
        CancellationToken ct)
    {
        return Ok(await _suppliers.SearchOutcomeRecordsAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.SupplierRead)]
    [HttpGet("outcome-summary")]
    public async Task<ActionResult<SupplierOutcomeSummaryDto>> OutcomeSummaryAsync(CancellationToken ct)
    {
        return Ok(await _suppliers.GetOutcomeSummaryAsync(ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.SupplierManage)]
    [HttpPost("outcome-records/backfill")]
    public async Task<ActionResult<OutcomeSupplierBackfillEnqueueDto>> BackfillOutcomeRecordsAsync(
        [FromQuery] int maxItems,
        CancellationToken ct)
    {
        return Accepted(await _suppliers.EnqueueOutcomeSupplierBackfillAsync(maxItems, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.SupplierRead)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<SupplierDetailDto>> GetAsync(long id, CancellationToken ct)
    {
        var supplier = await _suppliers.GetAsync(id, ct);
        return supplier == null ? NotFound() : Ok(supplier);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.SupplierManage)]
    [HttpPost]
    public async Task<ActionResult<SupplierDto>> CreateAsync(
        [FromBody] CreateSupplierRequest request,
        CancellationToken ct)
    {
        var supplier = await _suppliers.CreateAsync(request, ct);
        return Created($"/api/bidops/suppliers/{supplier.Id}", supplier);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.SupplierManage)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<SupplierDto>> UpdateAsync(
        long id,
        [FromBody] UpdateSupplierRequest request,
        CancellationToken ct)
    {
        return Ok(await _suppliers.UpdateAsync(id, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.SupplierManage)]
    [HttpPost("{id:long}/contacts")]
    public async Task<ActionResult<SupplierContactDto>> AddContactAsync(
        long id,
        [FromBody] CreateSupplierContactRequest request,
        CancellationToken ct)
    {
        return Ok(await _suppliers.AddContactAsync(id, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.SupplierManage)]
    [HttpPost("{id:long}/capabilities")]
    public async Task<ActionResult<SupplierCapabilityDto>> AddCapabilityAsync(
        long id,
        [FromBody] CreateSupplierCapabilityRequest request,
        CancellationToken ct)
    {
        return Ok(await _suppliers.AddCapabilityAsync(id, request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.SupplierEvidenceManage)]
    [HttpPost("{id:long}/evidence-documents")]
    public async Task<ActionResult<SupplierEvidenceDocumentDto>> AddEvidenceDocumentAsync(
        long id,
        [FromBody] CreateSupplierEvidenceDocumentRequest request,
        CancellationToken ct)
    {
        return Ok(await _suppliers.AddEvidenceDocumentAsync(id, request, ct));
    }
}
