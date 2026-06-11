using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class MockCrawlJobHandler : IBackgroundJobHandler
{
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsRawIngestionService _ingestion;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<MockCrawlJobHandler> _logger;

    public MockCrawlJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsRawIngestionService ingestion,
        IBackgroundJobClient jobs,
        ILogger<MockCrawlJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _ingestion = ingestion ?? throw new ArgumentNullException(nameof(ingestion));
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

        await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<AttachmentProcessJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.AttachmentProcess,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = "BidOps process mock notice attachments",
                TenantId = payload.TenantId,
                StoreId = payload.StoreId,
                DeduplicationKey = $"bidops:attachment-process:{payload.TenantId}:{rawNoticeId}:{DateTime.UtcNow:yyyyMMddHHmm}",
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
