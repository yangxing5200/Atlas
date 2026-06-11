using Atlas.Infrastructure.Security;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Queries;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/crawl-sources")]
public sealed class CrawlSourcesController : ControllerBase
{
    private readonly IBidOpsCrawlService _crawl;
    private readonly IBidOpsQueryService _queries;

    public CrawlSourcesController(
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
        return Ok(await _queries.SearchSourcesAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlManage)]
    [HttpPost]
    public async Task<ActionResult<CrawlSourceDto>> CreateAsync(
        [FromBody] CreateCrawlSourceRequest request,
        CancellationToken ct)
    {
        var created = await _crawl.CreateSourceAsync(request, ct);
        return CreatedAtAction(nameof(SearchAsync), new { id = created.Id }, created);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlManage)]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync(
        long id,
        [FromBody] UpdateCrawlSourceRequest request,
        CancellationToken ct)
    {
        await _crawl.UpdateSourceAsync(id, request, ct);
        return NoContent();
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlManage)]
    [HttpPost("{id:long}/enable")]
    public async Task<IActionResult> EnableAsync(long id, CancellationToken ct)
    {
        await _crawl.SetSourceEnabledAsync(id, enabled: true, ct: ct);
        return NoContent();
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlManage)]
    [HttpPost("{id:long}/disable")]
    public async Task<IActionResult> DisableAsync(
        long id,
        [FromBody] ReviewDecisionRequest? request,
        CancellationToken ct)
    {
        await _crawl.SetSourceEnabledAsync(id, enabled: false, reason: request?.Remark, ct);
        return NoContent();
    }
}
