using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class StateGridEcpCrawlJobHandler : IBackgroundJobHandler
{
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IStateGridEcpCrawler _crawler;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<StateGridEcpCrawlJobHandler> _logger;

    public StateGridEcpCrawlJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IStateGridEcpCrawler crawler,
        IRepository<RawNotice> rawNotices,
        IBackgroundJobClient jobs,
        ILogger<StateGridEcpCrawlJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _crawler = crawler ?? throw new ArgumentNullException(nameof(crawler));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
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

        var result = await _crawler.CrawlAsync(
            new StateGridEcpCrawlRequest(
                payload.ChannelId,
                payload.Mode,
                payload.CheckpointId,
                payload.StartPage,
                payload.PageSize,
                payload.MaxPages,
                payload.RangeStartPublishTime,
                payload.RangeEndPublishTime),
            context.Job.Id,
            ct);
        foreach (var rawNoticeId in result.RawNoticeIds.Distinct())
        {
            var raw = await _rawNotices.GetByIdAsync(rawNoticeId, ct);
            await _jobs.EnqueueAsync(
                new EnqueueBackgroundJobRequest<AttachmentProcessJobPayload>
                {
                    JobType = BidOpsBackgroundJobTypes.AttachmentProcess,
                    Queue = BidOpsBackgroundJobQueues.BidOps,
                    JobName = "BidOps process State Grid notice attachments",
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
        }

        _logger.LogInformation(
            "State Grid ECP crawl completed for channel {ChannelId}. mode={Mode}, pages={StartPage}-{EndPage}, discovered={Discovered}, ingested={Ingested}, skipped={Skipped}, failed={Failed}.",
            result.ChannelId,
            payload.Mode,
            result.StartPage,
            result.EndPage,
            result.Discovered,
            result.Ingested,
            result.Skipped,
            result.Failed);

        return BackgroundJobExecutionResult.Success(
            $"channelId={result.ChannelId};mode={payload.Mode};pages={result.StartPage}-{result.EndPage};discovered={result.Discovered};created={result.Created};changed={result.Changed};skipped={result.Skipped};failed={result.Failed};remaining={result.RemainingEstimate?.ToString() ?? "unknown"}");
    }
}
