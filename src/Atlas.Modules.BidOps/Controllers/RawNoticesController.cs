using Atlas.Infrastructure.Security;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Queries;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/raw-notices")]
public sealed class RawNoticesController : ControllerBase
{
    private readonly IBidOpsCrawlService _crawl;
    private readonly IBidOpsQueryService _queries;

    public RawNoticesController(
        IBidOpsCrawlService crawl,
        IBidOpsQueryService queries)
    {
        _crawl = crawl ?? throw new ArgumentNullException(nameof(crawl));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] RawNoticeSearchQuery query, CancellationToken ct)
    {
        return Ok(await _queries.SearchRawNoticesAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<RawNoticeDto>> GetAsync(long id, CancellationToken ct)
    {
        var raw = await _queries.GetRawNoticeAsync(id, ct);
        return raw == null ? NotFound() : Ok(raw);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlImport)]
    [HttpPost("import-url")]
    public async Task<ActionResult<EnqueueJobDto>> ImportUrlAsync(
        [FromBody] ImportPublicUrlRequest request,
        CancellationToken ct)
    {
        return Accepted(await _crawl.EnqueueManualUrlImportAsync(request, ct));
    }
}
