using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class BidOpsScheduledScanTask : IRecurringTask
{
    private readonly IConfiguration _configuration;
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IRepository<CrawlSource> _sources;
    private readonly IRepository<CrawlChannel> _channels;
    private readonly IRepository<CrawlCheckpoint> _checkpoints;
    private readonly IBackgroundJobClient _jobs;
    private readonly IBidOpsRuntimeControlService _runtimeControl;
    private readonly ILogger<BidOpsScheduledScanTask> _logger;

    public BidOpsScheduledScanTask(
        IConfiguration configuration,
        IExecutionIdentityAccessor identityAccessor,
        IRepository<CrawlSource> sources,
        IRepository<CrawlChannel> channels,
        IRepository<CrawlCheckpoint> checkpoints,
        IBackgroundJobClient jobs,
        IBidOpsRuntimeControlService runtimeControl,
        ILogger<BidOpsScheduledScanTask> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _runtimeControl = runtimeControl ?? throw new ArgumentNullException(nameof(runtimeControl));
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
        var now = DateTime.Now;

        foreach (var tenantId in tenantIds)
        {
            if (await _runtimeControl.IsTaskPausedAsync(tenantId, ct))
            {
                _logger.LogInformation("BidOps scheduled scan skipped for tenant {TenantId} because global task pause is enabled.", tenantId);
                continue;
            }

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
            var channelIds = channels.Select(x => x.Id).ToList();
            var checkpointQuery = await _checkpoints.QueryAsync(ct);
            var backfillCheckpoints = channelIds.Count == 0
                ? new List<CrawlCheckpoint>()
                : await checkpointQuery
                    .Where(x => channelIds.Contains(x.ChannelId) &&
                                x.Mode == BidOpsCrawlModes.Backfill &&
                                x.Status != BidOpsCrawlCheckpointStatuses.Completed &&
                                x.Status != BidOpsCrawlCheckpointStatuses.Paused &&
                                x.Status != BidOpsCrawlCheckpointStatuses.Running)
                    .ToListAsync(ct);
            var backfillMap = backfillCheckpoints
                .GroupBy(x => x.ChannelId)
                .ToDictionary(x => x.Key, x => x.OrderBy(y => y.LastRunAt ?? DateTime.MinValue).First());

            foreach (var channel in channels)
            {
                if (!sourceMap.TryGetValue(channel.SourceId, out var source))
                    continue;

                if (backfillMap.TryGetValue(channel.Id, out var checkpoint))
                {
                    var backfillEnqueued = await EnqueueScanAsync(
                        tenantId,
                        userId,
                        userName,
                        source,
                        channel,
                        checkpoint,
                        ct);
                    if (backfillEnqueued)
                    {
                        _logger.LogInformation(
                            "BidOps scheduled backfill enqueued channel {ChannelId} for tenant {TenantId}.",
                            channel.Id,
                            tenantId);
                    }

                    continue;
                }

                if (!IsDue(source, channel, now))
                    continue;

                var enqueued = await EnqueueScanAsync(tenantId, userId, userName, source, channel, checkpoint: null, ct);
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
        CrawlCheckpoint? checkpoint,
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
                    DeduplicationKey = BidOpsBackgroundJobDeduplicationKeys.ScheduledScan(
                        "state-grid-ecp",
                        tenantId,
                        channel,
                        checkpoint),
                    MaxAttempts = Math.Max(1, source.MaxRetryCount),
                    Payload = new StateGridEcpCrawlJobPayload(
                        tenantId,
                        StoreId: null,
                        userId,
                        userName,
                        channel.Id,
                        checkpoint?.Mode ?? BidOpsCrawlModes.Incremental,
                        checkpoint?.Id,
                        ResolveCheckpointPage(checkpoint),
                        PageSize: null,
                        MaxPages: checkpoint == null
                            ? _configuration.GetValue<int?>("BidOps:ScheduledScan:IncrementalMaxPages")
                            : _configuration.GetValue<int?>("BidOps:ScheduledScan:BackfillMaxPagesPerCycle"),
                        RangeStartPublishTime: checkpoint?.RangeStartPublishTime,
                        RangeEndPublishTime: checkpoint?.RangeEndPublishTime)
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
                    DeduplicationKey = BidOpsBackgroundJobDeduplicationKeys.ScheduledScan(
                        "mock",
                        tenantId,
                        channel,
                        checkpoint),
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

    private static int? ResolveCheckpointPage(CrawlCheckpoint? checkpoint)
    {
        if (checkpoint == null)
            return null;

        return int.TryParse(checkpoint.NextCursor, out var page) && page > 0
            ? page
            : 1;
    }

    private static bool IsDue(
        CrawlSource source,
        CrawlChannel channel,
        DateTime now)
    {
        if (string.Equals(channel.ScheduleMode, BidOpsCrawlScheduleModes.Daily, StringComparison.OrdinalIgnoreCase))
            return IsDailyDue(channel, now);

        if (!channel.LastScanTime.HasValue)
            return true;

        var intervalMinutes = channel.ScanIntervalMinutes.HasValue && channel.ScanIntervalMinutes.Value > 0
            ? channel.ScanIntervalMinutes.Value
            : source.CrawlIntervalMinutes;
        var interval = TimeSpan.FromMinutes(Math.Max(1, intervalMinutes));
        return channel.LastScanTime.Value.Add(interval) <= now;
    }

    private static bool IsDailyDue(CrawlChannel channel, DateTime now)
    {
        if (!TryParseDailyScanTime(channel.DailyScanTime, out var time))
            return false;

        var scheduledAt = now.Date.Add(time);
        if (now < scheduledAt)
            return false;

        return !channel.LastScanTime.HasValue ||
               channel.LastScanTime.Value.Date != now.Date ||
               channel.LastScanTime.Value < scheduledAt;
    }

    private static bool TryParseDailyScanTime(string value, out TimeSpan time)
    {
        return TimeSpan.TryParseExact(value, @"hh\:mm", null, out time) ||
               TimeSpan.TryParseExact(value, @"h\:mm", null, out time);
    }
}
