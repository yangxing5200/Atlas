using Atlas.Infrastructure.Security;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Queries;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/crawl-channels")]
public sealed class CrawlChannelsController : ControllerBase
{
    private readonly IBidOpsCrawlService _crawl;
    private readonly IBidOpsQueryService _queries;

    public CrawlChannelsController(
        IBidOpsCrawlService crawl,
        IBidOpsQueryService queries)
    {
        _crawl = crawl ?? throw new ArgumentNullException(nameof(crawl));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] BidOpsPagedQuery query, CancellationToken ct)
    {
        return Ok(await _queries.SearchChannelsAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlManage)]
    [HttpPost]
    public async Task<ActionResult<CrawlChannelDto>> CreateAsync(
        [FromBody] CreateCrawlChannelRequest request,
        CancellationToken ct)
    {
        var created = await _crawl.CreateChannelAsync(request, ct);
        return CreatedAtAction(nameof(SearchAsync), new { id = created.Id }, created);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlManage)]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync(
        long id,
        [FromBody] UpdateCrawlChannelRequest request,
        CancellationToken ct)
    {
        await _crawl.UpdateChannelAsync(id, request, ct);
        return NoContent();
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlImport)]
    [HttpPost("{id:long}/scan-now")]
    public async Task<ActionResult<EnqueueJobDto>> ScanNowAsync(long id, CancellationToken ct)
    {
        return Accepted(await _crawl.EnqueueMockScanAsync(id, ct));
    }
}
