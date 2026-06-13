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
    private readonly IBidOpsReviewService _review;

    public RawNoticesController(
        IBidOpsCrawlService crawl,
        IBidOpsQueryService queries,
        IBidOpsReviewService review)
    {
        _crawl = crawl ?? throw new ArgumentNullException(nameof(crawl));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _review = review ?? throw new ArgumentNullException(nameof(review));
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

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("{id:long}/pipeline")]
    public async Task<ActionResult<RawNoticePipelineDto>> PipelineAsync(long id, CancellationToken ct)
    {
        var pipeline = await _queries.GetRawNoticePipelineAsync(id, ct);
        return pipeline == null ? NotFound() : Ok(pipeline);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.ReviewApprove)]
    [HttpPost("{id:long}/reparse")]
    public async Task<ActionResult<EnqueueJobDto>> ReparseAsync(
        long id,
        [FromBody] ReparseRawNoticeRequest? request,
        CancellationToken ct)
    {
        return Accepted(await _review.EnqueueRawNoticeReparseAsync(id, request ?? new ReparseRawNoticeRequest(), ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("{id:long}/attachments")]
    public async Task<ActionResult<IReadOnlyList<RawAttachmentDto>>> ListAttachmentsAsync(long id, CancellationToken ct)
    {
        var raw = await _queries.GetRawNoticeAsync(id, ct);
        if (raw == null)
            return NotFound();

        return Ok(await _queries.ListRawAttachmentsAsync(id, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("{id:long}/attachments/{attachmentId:long}/text")]
    public async Task<ActionResult<RawAttachmentTextDto>> GetAttachmentTextAsync(
        long id,
        long attachmentId,
        CancellationToken ct)
    {
        var text = await _queries.GetRawAttachmentTextAsync(id, attachmentId, ct);
        return text == null ? NotFound() : Ok(text);
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlRead)]
    [HttpGet("{id:long}/attachments/{attachmentId:long}/file")]
    public async Task<IActionResult> GetAttachmentFileAsync(
        long id,
        long attachmentId,
        [FromQuery] bool download,
        CancellationToken ct)
    {
        var attachment = await _queries.OpenRawAttachmentFileAsync(id, attachmentId, ct);
        if (attachment == null)
            return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";

        var result = new FileStreamResult(attachment.Content, attachment.ContentType)
        {
            EnableRangeProcessing = true
        };
        if (download)
        {
            result.FileDownloadName = attachment.FileName;
        }
        else
        {
            Response.Headers["Content-Disposition"] = BuildInlineContentDisposition(attachment.FileName);
        }

        return result;
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlImport)]
    [HttpPost("import-url")]
    public async Task<ActionResult<EnqueueJobDto>> ImportUrlAsync(
        [FromBody] ImportPublicUrlRequest request,
        CancellationToken ct)
    {
        return Accepted(await _crawl.EnqueueManualUrlImportAsync(request, ct));
    }

    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + BidOpsPermissionCodes.CrawlImport)]
    [HttpPost("backfill-attachments")]
    public async Task<ActionResult<EnqueueJobDto>> BackfillAttachmentsAsync(
        [FromBody] BackfillRawNoticeAttachmentsRequest? request,
        CancellationToken ct)
    {
        return Accepted(await _crawl.EnqueueRawAttachmentBackfillAsync(
            request ?? new BackfillRawNoticeAttachmentsRequest(),
            ct));
    }

    private static string BuildInlineContentDisposition(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "inline";

        var safeAsciiName = new string(fileName
            .Select(c => c < 32 || c > 126 || c is '"' or '\\' or ';' ? '_' : c)
            .ToArray());
        var encodedName = Uri.EscapeDataString(fileName);
        return $"inline; filename=\"{safeAsciiName}\"; filename*=UTF-8''{encodedName}";
    }
}
