using Atlas.Infrastructure.Security;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/notices")]
public sealed class NoticesController : ControllerBase
{
    private readonly IBidOpsQueryService _queries;

    public NoticesController(IBidOpsQueryService queries)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessRead)]
    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] NoticeSearchQuery query, CancellationToken ct)
    {
        return Ok(await _queries.SearchNoticesAsync(query, ct));
    }
}
