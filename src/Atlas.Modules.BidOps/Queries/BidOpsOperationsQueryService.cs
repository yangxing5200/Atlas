using Atlas.BackgroundTasks;
using Atlas.BackgroundTasks.Operations;
using Atlas.Core.Authorization;
using Atlas.Core.Enums;
using Atlas.Data.Abstractions;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Opportunities;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Atlas.Modules.BidOps.Queries;

public sealed class BidOpsOperationsQueryService : IBidOpsOperationsQueryService
{
    private readonly IRepository<CrawlSource> _sources;
    private readonly IRepository<CrawlChannel> _channels;
    private readonly IRepository<CrawlCheckpoint> _checkpoints;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<RawAttachment> _rawAttachments;
    private readonly IRepository<ReviewTask> _reviewTasks;
    private readonly IRepository<Notice> _notices;
    private readonly IRepository<TenderPackage> _packages;
    private readonly IRepository<RequirementItem> _requirements;
    private readonly IRepository<Opportunity> _opportunities;
    private readonly IBackgroundJobOperationsService _jobs;
    private readonly BackgroundJobWorkerOptions _workerOptions;
    private readonly RecurringTaskRunnerOptions _recurringOptions;
    private readonly IConfiguration _configuration;
    private readonly IBidOpsAiSettingsService _aiSettings;
    private readonly IBidOpsRuntimeControlService _runtimeControl;

    public BidOpsOperationsQueryService(
        IRepository<CrawlSource> sources,
        IRepository<CrawlChannel> channels,
        IRepository<CrawlCheckpoint> checkpoints,
        IRepository<RawNotice> rawNotices,
        IRepository<RawAttachment> rawAttachments,
        IRepository<ReviewTask> reviewTasks,
        IRepository<Notice> notices,
        IRepository<TenderPackage> packages,
        IRepository<RequirementItem> requirements,
        IRepository<Opportunity> opportunities,
        IBackgroundJobOperationsService jobs,
        IOptions<BackgroundJobWorkerOptions> workerOptions,
        IOptions<RecurringTaskRunnerOptions> recurringOptions,
        IConfiguration configuration,
        IBidOpsAiSettingsService aiSettings,
        IBidOpsRuntimeControlService runtimeControl)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _rawAttachments = rawAttachments ?? throw new ArgumentNullException(nameof(rawAttachments));
        _reviewTasks = reviewTasks ?? throw new ArgumentNullException(nameof(reviewTasks));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        _opportunities = opportunities ?? throw new ArgumentNullException(nameof(opportunities));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _workerOptions = workerOptions?.Value ?? new BackgroundJobWorkerOptions();
        _recurringOptions = recurringOptions?.Value ?? new RecurringTaskRunnerOptions();
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _aiSettings = aiSettings ?? throw new ArgumentNullException(nameof(aiSettings));
        _runtimeControl = runtimeControl ?? throw new ArgumentNullException(nameof(runtimeControl));
    }

    public async Task<BidOpsOperationsDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var today = now.Date;
        var config = await GetConfigCheckAsync(ct);
        var summary = await _jobs.GetSummaryAsync(new BackgroundJobSearchQuery(), bidOpsOnly: true, ct: ct);
        var failedJobs = await _jobs.SearchAsync(
            new BackgroundJobSearchQuery
            {
                Status = BackgroundJobStatus.Failed,
                PageIndex = 1,
                PageSize = 8
            },
            bidOpsOnly: true,
            ct: ct);
        var deadJobs = await _jobs.SearchAsync(
            new BackgroundJobSearchQuery
            {
                Status = BackgroundJobStatus.Dead,
                PageIndex = 1,
                PageSize = 8
            },
            bidOpsOnly: true,
            ct: ct);

        var rawQuery = await _rawNotices.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        var attachmentQuery = await _rawAttachments.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        var reviewQuery = await _reviewTasks.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);

        return new BidOpsOperationsDashboardDto
        {
            BackgroundJobWorkerEnabled = _workerOptions.Enabled,
            RecurringTaskRunnerEnabled = _recurringOptions.Enabled,
            BidOpsQueueConfigured = HasBidOpsQueue(),
            RuntimeStatus = await _runtimeControl.GetStatusAsync(ct),
            AiSettings = await _aiSettings.GetSettingsAsync(ct),
            Jobs = summary,
            RawNoticeCreatedToday = ToInt(await rawQuery.Where(x => x.FetchTime >= today).CountAsync(ct)),
            ReviewTaskCreatedToday = ToInt(await reviewQuery.Where(x => x.CreatedAt >= today).CountAsync(ct)),
            ParseQueuedRawNotices = ToInt(await rawQuery.Where(x => x.Status == RawNoticeStatus.ParseQueued).CountAsync(ct)),
            FailedRawNotices = ToInt(await rawQuery.Where(x => x.Status == RawNoticeStatus.Failed).CountAsync(ct)),
            PendingAttachments = ToInt(await attachmentQuery.Where(x =>
                x.DownloadStatus == DownloadStatus.Pending ||
                x.TextExtractStatus == TextExtractStatus.Pending).CountAsync(ct)),
            FailedAttachments = ToInt(await attachmentQuery.Where(x =>
                x.DownloadStatus == DownloadStatus.Failed ||
                x.TextExtractStatus == TextExtractStatus.Failed).CountAsync(ct)),
            ConfigWarnings = config.Items
                .Where(x => !x.Severity.Equals("Info", StringComparison.OrdinalIgnoreCase))
                .ToList(),
            RecentFailedJobs = failedJobs.Items
                .Concat(deadJobs.Items)
                .OrderByDescending(x => x.CreatedAt)
                .Take(8)
                .ToList()
        };
    }

    public async Task<BidOpsDashboardSummaryDto> GetBusinessDashboardAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var today = now.Date;
        var deadlineUntil = now.AddDays(7);

        var rawQuery = await _rawNotices.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        var reviewQuery = await _reviewTasks.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        var noticeQuery = await _notices.QueryDataScopeAsync(BidOpsDataResources.Notice, AtlasDataScopeType.AllTenant, ct);
        var packageQuery = await _packages.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        var requirementQuery = await _requirements.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        var opportunityQuery = await _opportunities.QueryDataScopeAsync(BidOpsDataResources.Opportunity, AtlasDataScopeType.AllTenant, ct);

        var pendingReviews = (await reviewQuery
            .Where(x => x.Status == ReviewTaskStatus.Pending)
            .ToListAsync(ct))
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .Take(6)
            .ToList();

        var activeOpportunities = await opportunityQuery
            .Where(x => x.Status == BidOpsOpportunityStatuses.Active)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var activeNoticeIds = activeOpportunities
            .Select(x => x.NoticeId)
            .Distinct()
            .ToList();
        List<Notice> activeNotices = activeNoticeIds.Count == 0
            ? []
            : await noticeQuery
                .Where(x => activeNoticeIds.Contains(x.Id))
                .ToListAsync(ct);
        var noticeMap = activeNotices.ToDictionary(x => x.Id);

        var dueOpportunityTodos = activeOpportunities
            .Where(x => x.NextActionAtUtc.HasValue && x.NextActionAtUtc.Value <= now)
            .OrderBy(x => x.NextActionAtUtc)
            .ThenByDescending(x => x.Priority)
            .ToList();

        var deadlineRisks = activeOpportunities
            .Select(x => (Opportunity: x, Notice: noticeMap.GetValueOrDefault(x.NoticeId)))
            .Where(x => x.Notice?.BidDeadline != null && x.Notice.BidDeadline.Value <= deadlineUntil)
            .OrderBy(x => x.Notice!.BidDeadline)
            .ThenByDescending(x => x.Opportunity.ValueScore ?? 0)
            .ToList();

        var highValueOpportunities = activeOpportunities
            .Where(IsHighValue)
            .OrderByDescending(x => x.ValueScore ?? 0)
            .ThenByDescending(x => x.EstimatedAmount ?? 0)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();

        return new BidOpsDashboardSummaryDto
        {
            GeneratedAtUtc = now,
            RawNoticeCreatedToday = ToInt(await rawQuery.Where(x => x.FetchTime >= today).CountAsync(ct)),
            ReviewTaskCreatedToday = ToInt(await reviewQuery.Where(x => x.CreatedAt >= today).CountAsync(ct)),
            PendingReviewTasks = ToInt(await reviewQuery.Where(x => x.Status == ReviewTaskStatus.Pending).CountAsync(ct)),
            FormalNoticeCreatedToday = ToInt(await noticeQuery.Where(x => x.CreatedAt >= today).CountAsync(ct)),
            PackageCreatedToday = ToInt(await packageQuery.Where(x => x.CreatedAt >= today).CountAsync(ct)),
            ActivePackageCount = ToInt(await packageQuery.Where(x => x.Status != "Closed" && x.Status != "Archived").CountAsync(ct)),
            RejectRiskRequirementCount = ToInt(await requirementQuery.Where(x => x.IsRejectRisk).CountAsync(ct)),
            OpportunityCreatedToday = ToInt(await opportunityQuery.Where(x => x.CreatedAt >= today).CountAsync(ct)),
            ActiveOpportunityCount = activeOpportunities.Count,
            HighValueOpportunityCount = highValueOpportunities.Count,
            OpportunityTodoCount = dueOpportunityTodos.Count,
            DeadlineRiskCount = deadlineRisks.Count,
            OpportunityStageFunnel = BuildStageFunnel(activeOpportunities),
            OpportunityValueDistribution = BuildValueDistribution(activeOpportunities),
            Todos = BuildTodos(pendingReviews, dueOpportunityTodos),
            DeadlineRisks = deadlineRisks
                .Take(8)
                .Select(x => MapDeadlineRisk(x.Opportunity, x.Notice!, now))
                .ToList(),
            HighValueOpportunities = highValueOpportunities
                .Take(8)
                .Select(MapOpportunity)
                .ToList()
        };
    }

    public Task<PagedResult<BackgroundJobListItemDto>> SearchJobsAsync(
        BackgroundJobSearchQuery query,
        CancellationToken ct = default)
    {
        return _jobs.SearchAsync(query, bidOpsOnly: true, ct: ct);
    }

    public async Task<BidOpsConfigCheckDto> GetConfigCheckAsync(CancellationToken ct = default)
    {
        var items = new List<BidOpsConfigCheckItemDto>();
        var runtimeStatus = await _runtimeControl.GetStatusAsync(ct);
        var sourceQuery = await _sources.QueryDataScopeAsync(BidOpsDataResources.CrawlSource, AtlasDataScopeType.AllTenant, ct);
        var channelQuery = await _channels.QueryDataScopeAsync(BidOpsDataResources.CrawlSource, AtlasDataScopeType.AllTenant, ct);
        var enabledSourceCount = await sourceQuery.Where(x => x.Enabled).CountAsync(ct);
        var enabledChannelCount = await channelQuery.Where(x => x.Enabled).CountAsync(ct);
        var needLoginSourceCount = await sourceQuery.Where(x => x.Enabled && x.NeedLogin).CountAsync(ct);

        if (!_workerOptions.Enabled)
        {
            items.Add(Error(
                "BackgroundJobWorkerDisabled",
                "后台任务 Worker 未启用",
                "BackgroundTasks:OneTimeJobs:Enabled=false，已入队的一次性任务不会被消费。"));
        }

        if (runtimeStatus.TaskPaused)
        {
            items.Add(Warning(
                "BidOpsTasksPaused",
                "BidOps 全局任务已暂停",
                string.IsNullOrWhiteSpace(runtimeStatus.PauseReason)
                    ? "暂停期间不会执行或新建 BidOps 后台任务，恢复后已排队任务会继续处理。"
                    : $"暂停原因：{runtimeStatus.PauseReason}"));
        }

        if (!HasBidOpsQueue())
        {
            items.Add(Error(
                "BidOpsQueueMissing",
                "bidops 队列未配置",
                "BackgroundTasks:OneTimeJobs:Queues 必须包含 bidops，否则 BidOps 抓取、附件处理和结构化解析任务会积压。"));
        }

        if (!_recurringOptions.Enabled)
        {
            items.Add(Warning(
                "RecurringTaskRunnerDisabled",
                "周期任务 Runner 未启用",
                "BackgroundTasks:Recurring:Enabled=false，ScheduledScan 和 Recovery 不会按周期触发。"));
        }

        if (!ReadBool("BidOps:ScheduledScan:Enabled", defaultValue: false))
        {
            items.Add(Warning(
                "ScheduledScanDisabled",
                "BidOps 定时扫描未启用",
                "BidOps:ScheduledScan:Enabled=false，系统不会自动扫描启用的采集栏目。"));
        }

        if (!HasConfiguredArray("BidOps:ScheduledScan:TenantIds"))
        {
            items.Add(Warning(
                "ScheduledScanTenantIdsMissing",
                "定时扫描租户未配置",
                "BidOps:ScheduledScan:TenantIds 为空，定时扫描不知道要为哪些租户入队。"));
        }

        if (!ReadBool("BidOps:Recovery:Enabled", defaultValue: false))
        {
            items.Add(Warning(
                "RecoveryDisabled",
                "BidOps 恢复任务未启用",
                "BidOps:Recovery:Enabled=false，失败或卡住的 RawNotice / 附件不会被周期恢复。"));
        }

        if (!HasConfiguredArray("BidOps:Recovery:TenantIds"))
        {
            items.Add(Warning(
                "RecoveryTenantIdsMissing",
                "恢复任务租户未配置",
                "BidOps:Recovery:TenantIds 为空，恢复任务不知道要为哪些租户扫描积压数据。"));
        }

        if (enabledSourceCount == 0)
        {
            items.Add(Warning(
                "NoEnabledSources",
                "没有启用的采集来源",
                "当前租户没有启用的 CrawlSource，自动采集不会产生新公告。"));
        }

        if (enabledChannelCount == 0)
        {
            items.Add(Warning(
                "NoEnabledChannels",
                "没有启用的采集栏目",
                "当前租户没有启用的 CrawlChannel，定时扫描不会入队抓取任务。"));
        }

        if (needLoginSourceCount > 0)
        {
            items.Add(Warning(
                "NeedLoginSources",
                "存在需要登录的采集来源",
                $"当前有 {needLoginSourceCount} 个启用来源标记为 NeedLogin，ScheduledScan 会跳过这类来源。"));
        }

        if (items.Count == 0)
        {
            items.Add(Info(
                "ConfigHealthy",
                "后台配置检查通过",
                "Worker、Recurring、bidops 队列和基础采集配置均已满足 P0 运行要求。"));
        }

        return new BidOpsConfigCheckDto
        {
            Items = items,
            HasError = items.Any(x => x.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)),
            HasWarning = items.Any(x => x.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
        };
    }

    public async Task<IReadOnlyList<BidOpsChannelHealthDto>> GetChannelHealthAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var since24h = now.AddHours(-24);
        var sourceQuery = await _sources.QueryDataScopeAsync(BidOpsDataResources.CrawlSource, AtlasDataScopeType.AllTenant, ct);
        var channelQuery = await _channels.QueryDataScopeAsync(BidOpsDataResources.CrawlSource, AtlasDataScopeType.AllTenant, ct);
        var sources = await sourceQuery.ToListAsync(ct);
        var sourceMap = sources.ToDictionary(x => x.Id);
        var channels = (await channelQuery
            .OrderBy(x => x.SourceId)
            .ToListAsync(ct))
            .OrderBy(x => x.SourceId)
            .ThenBy(x => x.Code)
            .ToList();

        var jobs = await LoadChannelJobSnapshotAsync(since24h, ct);
        var checkpointQuery = await _checkpoints.QueryDataScopeAsync(BidOpsDataResources.CrawlCheckpoint, AtlasDataScopeType.AllTenant, ct);
        var backfillCheckpoints = await checkpointQuery
            .Where(x => x.Mode == BidOpsCrawlModes.Backfill)
            .ToListAsync(ct);
        var backfillMap = backfillCheckpoints
            .GroupBy(x => x.ChannelId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.LastRunAt ?? y.CreatedAt).First());

        return channels
            .Select(channel => MapChannelHealth(
                channel,
                sourceMap.GetValueOrDefault(channel.SourceId),
                jobs,
                backfillMap.GetValueOrDefault(channel.Id),
                now,
                since24h))
            .ToList();
    }

    public async Task<IReadOnlyList<BidOpsCrawlProgressDto>> GetCrawlProgressAsync(CancellationToken ct = default)
    {
        var sourceQuery = await _sources.QueryDataScopeAsync(BidOpsDataResources.CrawlSource, AtlasDataScopeType.AllTenant, ct);
        var channelQuery = await _channels.QueryDataScopeAsync(BidOpsDataResources.CrawlSource, AtlasDataScopeType.AllTenant, ct);
        var checkpointQuery = await _checkpoints.QueryDataScopeAsync(BidOpsDataResources.CrawlCheckpoint, AtlasDataScopeType.AllTenant, ct);

        var sources = await sourceQuery.ToListAsync(ct);
        var sourceMap = sources.ToDictionary(x => x.Id);
        var channels = (await channelQuery
            .OrderBy(x => x.SourceId)
            .ToListAsync(ct))
            .OrderBy(x => x.SourceId)
            .ThenBy(x => x.Code)
            .ToList();
        var checkpoints = await checkpointQuery.ToListAsync(ct);
        var checkpointMap = checkpoints
            .GroupBy(x => (x.ChannelId, x.Mode))
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.LastRunAt ?? y.CreatedAt).First());

        var items = new List<BidOpsCrawlProgressDto>();
        foreach (var channel in channels)
        {
            var source = sourceMap.GetValueOrDefault(channel.SourceId);
            foreach (var mode in new[] { BidOpsCrawlModes.Incremental, BidOpsCrawlModes.Backfill })
            {
                checkpointMap.TryGetValue((channel.Id, mode), out var checkpoint);
                items.Add(MapCrawlProgress(channel, source, checkpoint, mode));
            }
        }

        return items;
    }

    private BidOpsChannelHealthDto MapChannelHealth(
        CrawlChannel channel,
        CrawlSource? source,
        IReadOnlyCollection<ChannelJobProjection> jobs,
        CrawlCheckpoint? backfillCheckpoint,
        DateTime now,
        DateTime since24h)
    {
        var intervalMinutes = Math.Max(
            1,
            channel.ScanIntervalMinutes.HasValue && channel.ScanIntervalMinutes.Value > 0
                ? channel.ScanIntervalMinutes.Value
                : source?.CrawlIntervalMinutes ?? 60);
        var nextDueAtUtc = channel.LastSuccessTime?.AddMinutes(intervalMinutes);
        var matchedJobs = jobs
            .Where(job => JobMatchesChannel(job, channel.Id))
            .ToList();

        var alert = BuildCrawlAlert(channel, source, backfillCheckpoint);
        return new BidOpsChannelHealthDto
        {
            ChannelId = channel.Id,
            SourceId = channel.SourceId,
            SourceName = source?.Name ?? $"来源 {channel.SourceId}",
            SourceType = source?.SourceType ?? string.Empty,
            ChannelName = channel.Name,
            NoticeType = channel.NoticeType,
            SourceEnabled = source?.Enabled ?? false,
            ChannelEnabled = channel.Enabled,
            Enabled = channel.Enabled && (source?.Enabled ?? false),
            NeedLogin = source?.NeedLogin ?? false,
            ScheduleMode = channel.ScheduleMode,
            ScanIntervalMinutes = channel.ScanIntervalMinutes,
            DailyScanTime = channel.DailyScanTime,
            CrawlIntervalMinutes = intervalMinutes,
            LastScanTime = channel.LastScanTime,
            LastSuccessTime = channel.LastSuccessTime,
            LastError = channel.LastError,
            HealthStatus = CalculateHealthStatus(channel, source, now, intervalMinutes),
            NextDueAtUtc = nextDueAtUtc,
            MinutesSinceLastSuccess = channel.LastSuccessTime.HasValue
                ? Math.Max(0, (int)(now - channel.LastSuccessTime.Value).TotalMinutes)
                : null,
            PendingJobs = matchedJobs.Count(x => x.Status == BackgroundJobStatus.Pending),
            RunningJobs = matchedJobs.Count(x => x.Status == BackgroundJobStatus.Running),
            FailedJobs24h = matchedJobs.Count(x =>
                x.CreatedAt >= since24h &&
                (x.Status == BackgroundJobStatus.Failed || x.Status == BackgroundJobStatus.Dead)),
            SucceededJobs24h = matchedJobs.Count(x =>
                x.CreatedAt >= since24h &&
                x.Status == BackgroundJobStatus.Succeeded),
            BackfillStatus = backfillCheckpoint?.Status ?? string.Empty,
            BackfillNextCursor = backfillCheckpoint?.NextCursor ?? string.Empty,
            BackfillScannedItemCount = backfillCheckpoint?.ScannedItemCount ?? 0,
            BackfillCreatedCount = backfillCheckpoint?.CreatedCount ?? 0,
            BackfillChangedCount = backfillCheckpoint?.ChangedCount ?? 0,
            BackfillDuplicateCount = backfillCheckpoint?.DuplicateCount ?? 0,
            BackfillFailedItemCount = backfillCheckpoint?.FailedItemCount ?? 0,
            BackfillRemainingEstimate = backfillCheckpoint?.RemainingEstimate,
            AlertLevel = alert.Level,
            AlertMessage = alert.Message
        };
    }

    private static string CalculateHealthStatus(
        CrawlChannel channel,
        CrawlSource? source,
        DateTime now,
        int intervalMinutes)
    {
        if (!channel.Enabled)
            return "Disabled";

        if (source == null || !source.Enabled)
            return "SourceDisabled";

        if (source.NeedLogin)
            return "SkippedNeedLogin";

        if (!string.IsNullOrWhiteSpace(channel.LastError) &&
            (!channel.LastSuccessTime.HasValue ||
             !channel.LastScanTime.HasValue ||
             channel.LastScanTime.Value >= channel.LastSuccessTime.Value))
        {
            return "Failed";
        }

        if (!channel.LastSuccessTime.HasValue)
            return "NeverSucceeded";

        var minutesSinceLastSuccess = (now - channel.LastSuccessTime.Value).TotalMinutes;
        if (minutesSinceLastSuccess > intervalMinutes * 2)
            return "Stale";

        if (minutesSinceLastSuccess >= intervalMinutes)
            return "Due";

        return "Healthy";
    }

    private static BidOpsCrawlProgressDto MapCrawlProgress(
        CrawlChannel channel,
        CrawlSource? source,
        CrawlCheckpoint? checkpoint,
        string mode)
    {
        var alert = BuildCrawlAlert(channel, source, checkpoint);
        return new BidOpsCrawlProgressDto
        {
            ChannelId = channel.Id,
            SourceId = channel.SourceId,
            SourceName = source?.Name ?? $"来源 {channel.SourceId}",
            SourceType = source?.SourceType ?? string.Empty,
            ChannelName = channel.Name,
            NoticeType = channel.NoticeType,
            SourceEnabled = source?.Enabled ?? false,
            ChannelEnabled = channel.Enabled,
            Mode = mode,
            Status = checkpoint?.Status ?? "NotStarted",
            NextCursor = checkpoint?.NextCursor ?? "1",
            LastSuccessfulCursor = checkpoint?.LastSuccessfulCursor ?? string.Empty,
            RangeStartPublishTime = checkpoint?.RangeStartPublishTime,
            RangeEndPublishTime = checkpoint?.RangeEndPublishTime,
            HighWatermarkPublishTime = checkpoint?.HighWatermarkPublishTime,
            LowWatermarkPublishTime = checkpoint?.LowWatermarkPublishTime,
            TotalRemoteCount = checkpoint?.TotalRemoteCount,
            ScannedItemCount = checkpoint?.ScannedItemCount ?? 0,
            CreatedCount = checkpoint?.CreatedCount ?? 0,
            ChangedCount = checkpoint?.ChangedCount ?? 0,
            DuplicateCount = checkpoint?.DuplicateCount ?? 0,
            FailedItemCount = checkpoint?.FailedItemCount ?? 0,
            RemainingEstimate = checkpoint?.RemainingEstimate,
            StartedAt = checkpoint?.StartedAt,
            LastRunAt = checkpoint?.LastRunAt,
            CompletedAt = checkpoint?.CompletedAt,
            PausedAt = checkpoint?.PausedAt,
            PauseReason = checkpoint?.PauseReason ?? string.Empty,
            LastError = checkpoint?.LastError ?? string.Empty,
            AlertLevel = alert.Level,
            AlertMessage = alert.Message
        };
    }

    private static (string Level, string Message) BuildCrawlAlert(
        CrawlChannel channel,
        CrawlSource? source,
        CrawlCheckpoint? checkpoint)
    {
        if (source == null)
            return ("Error", "采集来源不存在。");

        if (!source.Enabled)
            return ("Warning", "采集来源已停用。");

        if (!channel.Enabled)
            return ("Warning", "采集栏目已停用。");

        if (source.NeedLogin)
            return ("Warning", "来源需要登录，自动扫描会跳过。");

        if (checkpoint == null)
            return (string.Empty, string.Empty);

        if (checkpoint.Status == BidOpsCrawlCheckpointStatuses.Failed)
            return ("Error", string.IsNullOrWhiteSpace(checkpoint.LastError) ? "最近一次扫描失败。" : checkpoint.LastError);

        if (checkpoint.FailedItemCount > 0)
            return ("Warning", $"累计 {checkpoint.FailedItemCount} 条公告处理失败。");

        if (checkpoint.Status == BidOpsCrawlCheckpointStatuses.Paused)
            return ("Info", string.IsNullOrWhiteSpace(checkpoint.PauseReason) ? "采集游标已暂停。" : checkpoint.PauseReason);

        return (string.Empty, string.Empty);
    }

    private bool HasBidOpsQueue()
    {
        return _workerOptions.Queues.Any(queue =>
            queue.Equals(BidOpsBackgroundJobQueues.BidOps, StringComparison.OrdinalIgnoreCase));
    }

    private bool HasConfiguredArray(string path)
    {
        return _configuration.GetSection(path)
            .GetChildren()
            .Any(child => !string.IsNullOrWhiteSpace(child.Value));
    }

    private bool ReadBool(string key, bool defaultValue)
    {
        var raw = _configuration[key];
        return bool.TryParse(raw, out var value) ? value : defaultValue;
    }

    private async Task<List<ChannelJobProjection>> LoadChannelJobSnapshotAsync(DateTime since24h, CancellationToken ct)
    {
        var pending = await _jobs.SearchAsync(
            new BackgroundJobSearchQuery { Status = BackgroundJobStatus.Pending, PageSize = 200 },
            bidOpsOnly: true,
            ct: ct);
        var running = await _jobs.SearchAsync(
            new BackgroundJobSearchQuery { Status = BackgroundJobStatus.Running, PageSize = 200 },
            bidOpsOnly: true,
            ct: ct);
        var failed = await _jobs.SearchAsync(
            new BackgroundJobSearchQuery { Status = BackgroundJobStatus.Failed, CreatedFrom = since24h, PageSize = 200 },
            bidOpsOnly: true,
            ct: ct);
        var dead = await _jobs.SearchAsync(
            new BackgroundJobSearchQuery { Status = BackgroundJobStatus.Dead, CreatedFrom = since24h, PageSize = 200 },
            bidOpsOnly: true,
            ct: ct);
        var succeeded = await _jobs.SearchAsync(
            new BackgroundJobSearchQuery { Status = BackgroundJobStatus.Succeeded, CreatedFrom = since24h, PageSize = 200 },
            bidOpsOnly: true,
            ct: ct);

        return pending.Items
            .Concat(running.Items)
            .Concat(failed.Items)
            .Concat(dead.Items)
            .Concat(succeeded.Items)
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .Select(x => new ChannelJobProjection
            {
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                Payload = x.PayloadPreview
            })
            .ToList();
    }

    private static bool JobMatchesChannel(ChannelJobProjection job, long channelId)
    {
        if (string.IsNullOrWhiteSpace(job.Payload))
            return false;

        var compact = $"\"channelId\":{channelId}";
        var stringValue = $"\"channelId\":\"{channelId}\"";
        return job.Payload.Contains(compact, StringComparison.OrdinalIgnoreCase) ||
               job.Payload.Contains(stringValue, StringComparison.OrdinalIgnoreCase);
    }

    private static List<BidOpsMetricBucketDto> BuildStageFunnel(IReadOnlyCollection<Opportunity> opportunities)
    {
        var standardStages = new[]
        {
            BidOpsOpportunityStages.New,
            BidOpsOpportunityStages.Watching,
            BidOpsOpportunityStages.Assessing,
            BidOpsOpportunityStages.Decided,
            BidOpsOpportunityStages.PursuitReady,
            "Pursuing",
            BidOpsOpportunityStages.Closed
        };
        var counts = opportunities
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Stage) ? BidOpsOpportunityStages.New : x.Stage)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        var buckets = standardStages
            .Select(stage => new BidOpsMetricBucketDto
            {
                Code = stage,
                Label = StageLabel(stage),
                Count = counts.GetValueOrDefault(stage)
            })
            .ToList();

        var customBuckets = counts
            .Where(x => !standardStages.Contains(x.Key, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Value)
            .Select(x => new BidOpsMetricBucketDto
            {
                Code = x.Key,
                Label = StageLabel(x.Key),
                Count = x.Value
            });

        buckets.AddRange(customBuckets);
        return buckets;
    }

    private static List<BidOpsMetricBucketDto> BuildValueDistribution(IReadOnlyCollection<Opportunity> opportunities)
    {
        var levels = new[]
        {
            BidOpsOpportunityValueLevels.High,
            BidOpsOpportunityValueLevels.Medium,
            BidOpsOpportunityValueLevels.Low,
            BidOpsOpportunityValueLevels.Unknown
        };
        var counts = opportunities
            .GroupBy(x => string.IsNullOrWhiteSpace(x.ValueLevel) ? BidOpsOpportunityValueLevels.Unknown : x.ValueLevel)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        return levels
            .Select(level => new BidOpsMetricBucketDto
            {
                Code = level,
                Label = ValueLabel(level),
                Count = counts.GetValueOrDefault(level)
            })
            .ToList();
    }

    private static List<BidOpsDashboardTodoDto> BuildTodos(
        IReadOnlyCollection<ReviewTask> pendingReviews,
        IReadOnlyCollection<Opportunity> dueOpportunityTodos)
    {
        var reviewTodos = pendingReviews.Select(x => new BidOpsDashboardTodoDto
        {
            Type = "ReviewTask",
            Title = string.IsNullOrWhiteSpace(x.TaskTitle) ? $"审核任务 {x.Id}" : x.TaskTitle,
            Route = $"/bidops/review/tasks/{x.Id}",
            Priority = x.Priority,
            DueAtUtc = x.CreatedAt
        });
        var opportunityTodos = dueOpportunityTodos.Select(x => new BidOpsDashboardTodoDto
        {
            Type = "Opportunity",
            Title = string.IsNullOrWhiteSpace(x.Title) ? x.OpportunityNo : x.Title,
            Route = $"/bidops/opportunities/{x.Id}",
            Priority = x.Priority,
            DueAtUtc = x.NextActionAtUtc
        });

        return reviewTodos
            .Concat(opportunityTodos)
            .OrderBy(x => x.DueAtUtc ?? DateTime.MaxValue)
            .ThenByDescending(x => x.Priority)
            .Take(10)
            .ToList();
    }

    private static BidOpsDashboardDeadlineRiskDto MapDeadlineRisk(
        Opportunity opportunity,
        Notice notice,
        DateTime now)
    {
        var deadline = notice.BidDeadline!.Value;
        var daysRemaining = (int)Math.Floor((deadline.Date - now.Date).TotalDays);
        return new BidOpsDashboardDeadlineRiskDto
        {
            OpportunityId = opportunity.Id,
            NoticeId = opportunity.NoticeId,
            PackageId = opportunity.PackageId,
            OpportunityNo = opportunity.OpportunityNo,
            Title = opportunity.Title,
            Stage = opportunity.Stage,
            ValueLevel = opportunity.ValueLevel,
            BidDeadline = deadline,
            DaysRemaining = daysRemaining,
            RiskLevel = daysRemaining < 0 ? "Overdue" : daysRemaining <= 3 ? "Urgent" : "DueSoon"
        };
    }

    private static BidOpsDashboardOpportunityDto MapOpportunity(Opportunity opportunity)
    {
        return new BidOpsDashboardOpportunityDto
        {
            OpportunityId = opportunity.Id,
            PackageId = opportunity.PackageId,
            OpportunityNo = opportunity.OpportunityNo,
            Title = opportunity.Title,
            Stage = opportunity.Stage,
            Decision = opportunity.Decision,
            ValueLevel = opportunity.ValueLevel,
            ValueScore = opportunity.ValueScore,
            EstimatedAmount = opportunity.EstimatedAmount
        };
    }

    private static bool IsHighValue(Opportunity opportunity)
    {
        return opportunity.ValueLevel.Equals(BidOpsOpportunityValueLevels.High, StringComparison.OrdinalIgnoreCase) ||
               opportunity.ValueScore >= 80m;
    }

    private static string StageLabel(string value)
    {
        return value switch
        {
            BidOpsOpportunityStages.New => "新建",
            BidOpsOpportunityStages.Watching => "关注中",
            BidOpsOpportunityStages.Assessing => "评估中",
            BidOpsOpportunityStages.Decided => "已决策",
            BidOpsOpportunityStages.PursuitReady => "待立项",
            "Pursuing" => "投标作业中",
            BidOpsOpportunityStages.Closed => "已关闭",
            _ => value
        };
    }

    private static string ValueLabel(string value)
    {
        return value switch
        {
            BidOpsOpportunityValueLevels.High => "高价值",
            BidOpsOpportunityValueLevels.Medium => "中价值",
            BidOpsOpportunityValueLevels.Low => "低价值",
            BidOpsOpportunityValueLevels.Unknown => "未评估",
            _ => value
        };
    }

    private static BidOpsConfigCheckItemDto Error(string code, string title, string message)
    {
        return new BidOpsConfigCheckItemDto
        {
            Severity = "Error",
            Code = code,
            Title = title,
            Message = message
        };
    }

    private static BidOpsConfigCheckItemDto Warning(string code, string title, string message)
    {
        return new BidOpsConfigCheckItemDto
        {
            Severity = "Warning",
            Code = code,
            Title = title,
            Message = message
        };
    }

    private static BidOpsConfigCheckItemDto Info(string code, string title, string message)
    {
        return new BidOpsConfigCheckItemDto
        {
            Severity = "Info",
            Code = code,
            Title = title,
            Message = message
        };
    }

    private sealed class ChannelJobProjection
    {
        public BackgroundJobStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Payload { get; set; } = string.Empty;
    }

    private static int ToInt(long value)
    {
        return value > int.MaxValue ? int.MaxValue : (int)value;
    }
}
