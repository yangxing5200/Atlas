using Atlas.Infrastructure.Security;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/crawl-run-logs")]
public sealed class CrawlRunLogsController : ControllerBase
{
    private readonly IBidOpsQueryService _queries;

    public CrawlRunLogsController(IBidOpsQueryService queries)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<CrawlRunLogDto>>> SearchAsync(
        [FromQuery] CrawlRunLogSearchQuery query,
        CancellationToken ct)
    {
        return Ok(await _queries.SearchCrawlRunLogsAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<CrawlRunLogDto>> GetAsync(long id, CancellationToken ct)
    {
        var log = await _queries.GetCrawlRunLogAsync(id, ct);
        return log == null ? NotFound() : Ok(log);
    }
}
