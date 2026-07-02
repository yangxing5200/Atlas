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
        var projectCode = BidOpsJobProjectCode.FirstMeaningful(
            payload.ProjectCode,
            BidOpsJobProjectCode.FromRawNotice(raw));
        var parseDeduplicationKey = string.IsNullOrWhiteSpace(payload.ForceParseRunId)
            ? $"bidops:structured-parse:{BidOpsSystemValues.StructuredParserVersion}:{payload.TenantId}:{payload.RawNoticeId}:{contentHash}"
            : $"bidops:structured-parse:{BidOpsSystemValues.StructuredParserVersion}:{payload.TenantId}:{payload.RawNoticeId}:manual-reparse:{payload.ForceParseRunId}";

        var parseJob = await _jobs.EnqueueAsync(
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
                Priority = context.Job.Priority,
                Payload = new StructuredParseJobPayload(
                    payload.TenantId,
                    payload.StoreId,
                    payload.UserId,
                    payload.UserName,
                    payload.RawNoticeId,
                    payload.ForceParseRunId,
                    payload.ReviewerPrompt,
                    projectCode)
            },
            ct);

        _logger.LogInformation(
            "BidOps attachment processing completed for raw notice {RawNoticeId}. total={Total}, downloaded={Downloaded}, extracted={Extracted}, failed={Failed}.",
            result.RawNoticeId,
            result.Total,
            result.Downloaded,
            result.Extracted,
            result.Failed);

        // 前置公告重解析从闭环页发起时，前端需要继续等待这里派生出的结构化解析子任务。
        return BackgroundJobExecutionResult.Success(
            $"rawNoticeId={result.RawNoticeId};projectCode={projectCode};attachments={result.Total};downloaded={result.Downloaded};extracted={result.Extracted};failed={result.Failed};structuredParseJobId={parseJob.JobId};structuredParseAlreadyExists={parseJob.AlreadyExists}");
    }
}
