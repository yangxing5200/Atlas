using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class AttachmentProcessJobHandler : IBackgroundJobHandler
{
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsAttachmentProcessingService _attachments;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<AttachmentProcessJobHandler> _logger;

    public AttachmentProcessJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsAttachmentProcessingService attachments,
        IRepository<RawNotice> rawNotices,
        IBackgroundJobClient jobs,
        ILogger<AttachmentProcessJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => BidOpsBackgroundJobTypes.AttachmentProcess;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<AttachmentProcessJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        var result = await _attachments.ProcessRawNoticeAttachmentsAsync(
            payload.RawNoticeId,
            forceTextExtraction: !string.IsNullOrWhiteSpace(payload.ForceParseRunId),
            ct);
        var raw = await _rawNotices.GetByIdAsync(payload.RawNoticeId, ct);
        var contentHash = raw?.ContentHash ?? "unknown";
        var parseDeduplicationKey = string.IsNullOrWhiteSpace(payload.ForceParseRunId)
            ? $"bidops:structured-parse:{BidOpsSystemValues.StructuredParserVersion}:{payload.TenantId}:{payload.RawNoticeId}:{contentHash}"
            : $"bidops:structured-parse:{BidOpsSystemValues.StructuredParserVersion}:{payload.TenantId}:{payload.RawNoticeId}:manual-reparse:{payload.ForceParseRunId}";

        await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<StructuredParseJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.StructuredParse,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = string.IsNullOrWhiteSpace(payload.ForceParseRunId)
                    ? "BidOps structured notice parse"
                    : "BidOps structured notice reparse",
                TenantId = payload.TenantId,
                StoreId = payload.StoreId,
                DeduplicationKey = parseDeduplicationKey,
                Payload = new StructuredParseJobPayload(
                    payload.TenantId,
                    payload.StoreId,
                    payload.UserId,
                    payload.UserName,
                    payload.RawNoticeId,
                    payload.ForceParseRunId)
            },
            ct);

        _logger.LogInformation(
            "BidOps attachment processing completed for raw notice {RawNoticeId}. total={Total}, downloaded={Downloaded}, extracted={Extracted}, failed={Failed}.",
            result.RawNoticeId,
            result.Total,
            result.Downloaded,
            result.Extracted,
            result.Failed);

        return BackgroundJobExecutionResult.Success(
            $"rawNoticeId={result.RawNoticeId};attachments={result.Total};downloaded={result.Downloaded};extracted={result.Extracted};failed={result.Failed}");
    }
}
