using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class ManualUrlImportJobHandler : IBackgroundJobHandler
{
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsRawIngestionService _ingestion;
    private readonly IStateGridEcpCrawler _stateGridCrawler;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<ManualUrlImportJobHandler> _logger;

    public ManualUrlImportJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsRawIngestionService ingestion,
        IStateGridEcpCrawler stateGridCrawler,
        IBackgroundJobClient jobs,
        ILogger<ManualUrlImportJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _ingestion = ingestion ?? throw new ArgumentNullException(nameof(ingestion));
        _stateGridCrawler = stateGridCrawler ?? throw new ArgumentNullException(nameof(stateGridCrawler));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => BidOpsBackgroundJobTypes.ManualUrlImport;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<ManualUrlImportJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        var rawNoticeId = await _stateGridCrawler.ImportPublicDetailAsync(
            payload.DetailUrl,
            payload.SourceId,
            payload.ChannelId,
            payload.NoticeType,
            context.Job.Id,
            ct);

        rawNoticeId ??= await _ingestion.ImportManualUrlAsync(
            new RawIngestionCommand(
                payload.SourceId,
                payload.ChannelId,
                payload.DetailUrl,
                payload.Title ?? string.Empty,
                payload.NoticeType ?? "TenderAnnouncement",
                payload.TextContent ?? string.Empty,
                HtmlContent: string.Empty,
                PublishTime: null),
            context.Job.Id,
            ct);
        var createdRawNoticeId = rawNoticeId.Value;

        await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<AttachmentProcessJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.AttachmentProcess,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = "BidOps process manual notice attachments",
                TenantId = payload.TenantId,
                StoreId = payload.StoreId,
                DeduplicationKey = $"bidops:attachment-process:{payload.TenantId}:{createdRawNoticeId}:{DateTime.UtcNow:yyyyMMddHHmm}",
                Payload = new AttachmentProcessJobPayload(
                    payload.TenantId,
                    payload.StoreId,
                    payload.UserId,
                    payload.UserName,
                    createdRawNoticeId)
            },
            ct);

        _logger.LogInformation("BidOps manual URL import created raw notice {RawNoticeId}.", createdRawNoticeId);
        return BackgroundJobExecutionResult.Success($"rawNoticeId={createdRawNoticeId}");
    }
}
