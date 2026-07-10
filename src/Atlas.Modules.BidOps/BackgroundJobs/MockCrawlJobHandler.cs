using Atlas.BackgroundTasks;
using Atlas.Core.Entities.Global;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class MockCrawlJobHandler : IBackgroundJobHandler
{
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsRawIngestionService _ingestion;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<MockCrawlJobHandler> _logger;

    public MockCrawlJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsRawIngestionService ingestion,
        IRepository<RawNotice> rawNotices,
        IBackgroundJobClient jobs,
        ILogger<MockCrawlJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _ingestion = ingestion ?? throw new ArgumentNullException(nameof(ingestion));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => BidOpsBackgroundJobTypes.MockCrawl;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<MockCrawlJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        var rawNoticeId = await _ingestion.CreateMockRawNoticeAsync(
            payload.ChannelId,
            context.Job.Id,
            ct);
        var raw = await _rawNotices.GetByIdAsync(rawNoticeId, ct);
        context.Job.SourceModule = BackgroundJobBusinessConstants.BidOpsSourceModule;
        context.Job.BusinessType = BackgroundJobBusinessConstants.RawNoticeBusinessType;
        context.Job.BusinessId = rawNoticeId;
        context.Job.CorrelationId = rawNoticeId.ToString(CultureInfo.InvariantCulture);

        await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<AttachmentProcessJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.AttachmentProcess,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = "BidOps process mock notice attachments",
                TenantId = payload.TenantId,
                StoreId = payload.StoreId,
                DeduplicationKey = BidOpsBackgroundJobDeduplicationKeys.AttachmentProcess(
                    payload.TenantId,
                    rawNoticeId,
                    raw?.ContentHash),
                Priority = context.Job.Priority,
                Payload = new AttachmentProcessJobPayload(
                    payload.TenantId,
                    payload.StoreId,
                    payload.UserId,
                    payload.UserName,
                    rawNoticeId)
            },
            ct);

        _logger.LogInformation("BidOps mock crawl created raw notice {RawNoticeId}.", rawNoticeId);
        return BackgroundJobExecutionResult.Success($"rawNoticeId={rawNoticeId}");
    }
}
