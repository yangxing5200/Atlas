using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class StateGridEcpCrawlJobHandler : IBackgroundJobHandler
{
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IStateGridEcpCrawler _crawler;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<StateGridEcpCrawlJobHandler> _logger;

    public StateGridEcpCrawlJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IStateGridEcpCrawler crawler,
        IBackgroundJobClient jobs,
        ILogger<StateGridEcpCrawlJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _crawler = crawler ?? throw new ArgumentNullException(nameof(crawler));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => BidOpsBackgroundJobTypes.StateGridEcpCrawl;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<StateGridEcpCrawlJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        var result = await _crawler.CrawlAsync(payload.ChannelId, context.Job.Id, ct);
        foreach (var rawNoticeId in result.RawNoticeIds.Distinct())
        {
            await _jobs.EnqueueAsync(
                new EnqueueBackgroundJobRequest<AttachmentProcessJobPayload>
                {
                    JobType = BidOpsBackgroundJobTypes.AttachmentProcess,
                    Queue = BidOpsBackgroundJobQueues.BidOps,
                    JobName = "BidOps process State Grid notice attachments",
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
        }

        _logger.LogInformation(
            "State Grid ECP crawl completed for channel {ChannelId}. discovered={Discovered}, ingested={Ingested}.",
            result.ChannelId,
            result.Discovered,
            result.Ingested);

        return BackgroundJobExecutionResult.Success(
            $"channelId={result.ChannelId};discovered={result.Discovered};ingested={result.Ingested}");
    }
}
