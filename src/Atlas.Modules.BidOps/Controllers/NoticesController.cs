using Atlas.Infrastructure.Security;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Queries;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Modules.BidOps.Controllers;

[ApiController]
[Route("api/bidops/notices")]
public sealed class NoticesController : ControllerBase
{
    private readonly IBidOpsQueryService _queries;
    private readonly IBidOpsNoticeService _notices;

    public NoticesController(IBidOpsQueryService queries, IBidOpsNoticeService notices)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessRead)]
    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] NoticeSearchQuery query, CancellationToken ct)
    {
        return Ok(await _queries.SearchNoticesAsync(query, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessRead)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<NoticeDto>> GetAsync(long id, CancellationToken ct)
    {
        var notice = await _queries.GetNoticeAsync(id, ct);
        return notice == null ? NotFound() : Ok(notice);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.BusinessManage)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<NoticeDto>> UpdateAsync(
        long id,
        [FromBody] UpdateNoticeRequest request,
        CancellationToken ct)
    {
        await _notices.UpdateAsync(id, request, ct);
        var notice = await _queries.GetNoticeAsync(id, ct);
        return notice == null ? NotFound() : Ok(notice);
    }
}
