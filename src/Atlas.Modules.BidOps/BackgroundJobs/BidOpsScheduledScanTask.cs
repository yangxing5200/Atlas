using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class BidOpsScheduledScanTask : IRecurringTask
{
    private readonly IConfiguration _configuration;
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IRepository<CrawlSource> _sources;
    private readonly IRepository<CrawlChannel> _channels;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<BidOpsScheduledScanTask> _logger;

    public BidOpsScheduledScanTask(
        IConfiguration configuration,
        IExecutionIdentityAccessor identityAccessor,
        IRepository<CrawlSource> sources,
        IRepository<CrawlChannel> channels,
        IBackgroundJobClient jobs,
        ILogger<BidOpsScheduledScanTask> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "bidops.scheduled-scan";

    public TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(
        1,
        _configuration.GetValue<int?>("BidOps:ScheduledScan:IntervalMinutes") ?? 5));

    public bool RunOnStartup => _configuration.GetValue<bool?>("BidOps:ScheduledScan:RunOnStartup") ?? false;

    public async Task ExecuteAsync(
        RecurringTaskContext context,
        CancellationToken ct = default)
    {
        if (!_configuration.GetValue<bool>("BidOps:ScheduledScan:Enabled"))
            return;

        var tenantIds = BidOpsBackgroundTenantConfiguration.GetTenantIds(_configuration, "ScheduledScan");
        if (tenantIds.Count == 0)
        {
            _logger.LogDebug("BidOps scheduled scan skipped because no tenant ids are configured.");
            return;
        }

        var maxChannels = Math.Clamp(
            _configuration.GetValue<int?>("BidOps:ScheduledScan:MaxChannelsPerCycle") ?? 20,
            1,
            200);
        var userId = BidOpsBackgroundTenantConfiguration.GetUserId(_configuration, "ScheduledScan");
        var userName = BidOpsBackgroundTenantConfiguration.GetUserName(_configuration, "ScheduledScan", "BidOps Scheduler");
        var now = DateTime.UtcNow;

        foreach (var tenantId in tenantIds)
        {
            using var identity = _identityAccessor.Begin(new ExecutionIdentitySnapshot(
                tenantId,
                StoreId: null,
                userId,
                userName,
                SessionId: null,
                IsAuthenticated: true));

            var sourceQuery = await _sources.QueryAsync(ct);
            var sources = await sourceQuery
                .Where(x => x.Enabled && !x.NeedLogin)
                .ToListAsync(ct);
            var sourceMap = sources.ToDictionary(x => x.Id);

            var channelQuery = await _channels.QueryAsync(ct);
            var channels = await channelQuery
                .Where(x => x.Enabled)
                .OrderBy(x => x.LastScanTime)
                .Take(maxChannels)
                .ToListAsync(ct);

            foreach (var channel in channels)
            {
                if (!sourceMap.TryGetValue(channel.SourceId, out var source) || !IsDue(source, channel, now))
                    continue;

                var enqueued = await EnqueueScanAsync(tenantId, userId, userName, source, channel, now, ct);
                if (enqueued)
                {
                    _logger.LogInformation(
                        "BidOps scheduled scan enqueued channel {ChannelId} for tenant {TenantId}.",
                        channel.Id,
                        tenantId);
                }
            }
        }
    }

    private async Task<bool> EnqueueScanAsync(
        long tenantId,
        long userId,
        string userName,
        CrawlSource source,
        CrawlChannel channel,
        DateTime now,
        CancellationToken ct)
    {
        if (string.Equals(source.SourceType, BidOpsCrawlSourceTypes.StateGridEcp, StringComparison.OrdinalIgnoreCase))
        {
            var result = await _jobs.EnqueueAsync(
                new EnqueueBackgroundJobRequest<StateGridEcpCrawlJobPayload>
                {
                    JobType = BidOpsBackgroundJobTypes.StateGridEcpCrawl,
                    Queue = BidOpsBackgroundJobQueues.BidOps,
                    JobName = "BidOps scheduled State Grid ECP public notice scan",
                    TenantId = tenantId,
                    DeduplicationKey = $"bidops:scheduled:state-grid-ecp:{tenantId}:{channel.Id}:{now:yyyyMMddHHmm}",
                    MaxAttempts = Math.Max(1, source.MaxRetryCount),
                    Payload = new StateGridEcpCrawlJobPayload(
                        tenantId,
                        StoreId: null,
                        userId,
                        userName,
                        channel.Id)
                },
                ct);
            return !result.AlreadyExists;
        }

        if (string.Equals(source.SourceType, BidOpsCrawlSourceTypes.Mock, StringComparison.OrdinalIgnoreCase))
        {
            var result = await _jobs.EnqueueAsync(
                new EnqueueBackgroundJobRequest<MockCrawlJobPayload>
                {
                    JobType = BidOpsBackgroundJobTypes.MockCrawl,
                    Queue = BidOpsBackgroundJobQueues.BidOps,
                    JobName = "BidOps scheduled mock public notice scan",
                    TenantId = tenantId,
                    DeduplicationKey = $"bidops:scheduled:mock:{tenantId}:{channel.Id}:{now:yyyyMMddHHmm}",
                    MaxAttempts = Math.Max(1, source.MaxRetryCount),
                    Payload = new MockCrawlJobPayload(
                        tenantId,
                        StoreId: null,
                        userId,
                        userName,
                        channel.Id)
                },
                ct);
            return !result.AlreadyExists;
        }

        return false;
    }

    private static bool IsDue(
        CrawlSource source,
        CrawlChannel channel,
        DateTime now)
    {
        if (!channel.LastScanTime.HasValue)
            return true;

        var interval = TimeSpan.FromMinutes(Math.Max(1, source.CrawlIntervalMinutes));
        return channel.LastScanTime.Value.Add(interval) <= now;
    }
}
