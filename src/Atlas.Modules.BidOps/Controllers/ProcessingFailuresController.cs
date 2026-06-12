using Atlas.Infrastructure.Security;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/processing/failures")]
public sealed class ProcessingFailuresController : ControllerBase
{
    private readonly IBidOpsQueryService _queries;

    public ProcessingFailuresController(IBidOpsQueryService queries)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewRead)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProcessingFailureDto>>> SearchAsync(
        [FromQuery] ProcessingFailureSearchQuery query,
        CancellationToken ct)
    {
        return Ok(await _queries.SearchProcessingFailuresAsync(query, ct));
    }
}
