using Atlas.BackgroundTasks;
using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsCrawlService : IBidOpsCrawlService
{
    private readonly IRepository<CrawlSource> _sources;
    private readonly IRepository<CrawlChannel> _channels;
    private readonly IRepository<CrawlCheckpoint> _checkpoints;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _jobs;
    private readonly ICurrentIdentity _identity;
    private readonly BidOpsContentHasher _hasher;
    private readonly IIdGenerator _idGenerator;
    private readonly IBidOpsRuntimeControlService _runtimeControl;

    public BidOpsCrawlService(
        IRepository<CrawlSource> sources,
        IRepository<CrawlChannel> channels,
        IRepository<CrawlCheckpoint> checkpoints,
        IUnitOfWork unitOfWork,
        IBackgroundJobClient jobs,
        ICurrentIdentity identity,
        BidOpsContentHasher hasher,
        IIdGenerator idGenerator,
        IBidOpsRuntimeControlService runtimeControl)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _runtimeControl = runtimeControl ?? throw new ArgumentNullException(nameof(runtimeControl));
    }

    public async Task<CrawlSourceDto> CreateSourceAsync(CreateCrawlSourceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSourceRequest(request);

        var code = NormalizeCode(request.Code);
        if (await _sources.FirstOrDefaultAsync(x => x.Code == code, ct) != null)
            throw new AtlasException($"BidOps crawl source already exists: {code}");

        var source = new CrawlSource
        {
            Code = code,
            Name = request.Name.Trim(),
            SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "Mock" : request.SourceType.Trim(),
            BaseUrl = request.BaseUrl.Trim(),
            Enabled = request.Enabled,
            RateLimitPerMinute = Math.Max(1, request.RateLimitPerMinute),
            CrawlIntervalMinutes = Math.Max(1, request.CrawlIntervalMinutes),
            MaxRetryCount = Math.Max(1, request.MaxRetryCount),
            NeedJsRender = request.NeedJsRender,
            NeedLogin = request.NeedLogin,
            RespectRobots = request.RespectRobots,
            RobotsPolicyNote = request.RobotsPolicyNote.Trim(),
            Remark = request.Remark.Trim()
        };

        await _sources.AddAsync(source, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Map(source);
    }

    public async Task UpdateSourceAsync(long id, UpdateCrawlSourceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSourceRequest(request);

        var query = await _sources.QueryTrackingAsync(ct);
        var source = await query.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (source == null)
            throw new AtlasException($"BidOps crawl source does not exist: {id}");

        source.Code = NormalizeCode(request.Code);
        source.Name = request.Name.Trim();
        source.SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "Mock" : request.SourceType.Trim();
        source.BaseUrl = request.BaseUrl.Trim();
        source.Enabled = request.Enabled;
        source.RateLimitPerMinute = Math.Max(1, request.RateLimitPerMinute);
        source.CrawlIntervalMinutes = Math.Max(1, request.CrawlIntervalMinutes);
        source.MaxRetryCount = Math.Max(1, request.MaxRetryCount);
        source.NeedJsRender = request.NeedJsRender;
        source.NeedLogin = request.NeedLogin;
        source.RespectRobots = request.RespectRobots;
        source.RobotsPolicyNote = request.RobotsPolicyNote.Trim();
        source.Remark = request.Remark.Trim();

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SetSourceEnabledAsync(long id, bool enabled, string? reason = null, CancellationToken ct = default)
    {
        var query = await _sources.QueryTrackingAsync(ct);
        var source = await query.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (source == null)
            throw new AtlasException($"BidOps crawl source does not exist: {id}");

        source.Enabled = enabled;
        source.PauseReason = enabled ? string.Empty : (reason ?? "Paused by user").Trim();
        source.PausedAt = enabled ? null : DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<CrawlChannelDto> CreateChannelAsync(CreateCrawlChannelRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateChannelRequest(request);

        if (await _sources.FirstOrDefaultAsync(x => x.Id == request.SourceId, ct) == null)
            throw new AtlasException($"BidOps crawl source does not exist: {request.SourceId}");

        var code = NormalizeCode(request.Code);
        if (await _channels.FirstOrDefaultAsync(x => x.SourceId == request.SourceId && x.Code == code, ct) != null)
            throw new AtlasException($"BidOps crawl channel already exists: {code}");

        var channel = new CrawlChannel
        {
            SourceId = request.SourceId,
            Code = code,
            Name = request.Name.Trim(),
            NoticeType = string.IsNullOrWhiteSpace(request.NoticeType) ? "TenderAnnouncement" : request.NoticeType.Trim(),
            ListUrl = request.ListUrl.Trim(),
            Region = request.Region.Trim(),
            Industry = request.Industry.Trim(),
            Enabled = request.Enabled,
            ScheduleMode = NormalizeScheduleMode(request.ScheduleMode),
            ScanIntervalMinutes = NormalizeScanInterval(request.ScanIntervalMinutes),
            DailyScanTime = NormalizeDailyScanTime(request.DailyScanTime)
        };

        await _channels.AddAsync(channel, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Map(channel);
    }

    public async Task UpdateChannelAsync(long id, UpdateCrawlChannelRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateChannelRequest(request);

        var query = await _channels.QueryTrackingAsync(ct);
        var channel = await query.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (channel == null)
            throw new AtlasException($"BidOps crawl channel does not exist: {id}");

        channel.SourceId = request.SourceId;
        channel.Code = NormalizeCode(request.Code);
        channel.Name = request.Name.Trim();
        channel.NoticeType = string.IsNullOrWhiteSpace(request.NoticeType) ? "TenderAnnouncement" : request.NoticeType.Trim();
        channel.ListUrl = request.ListUrl.Trim();
        channel.Region = request.Region.Trim();
        channel.Industry = request.Industry.Trim();
        channel.Enabled = request.Enabled;
        channel.ScheduleMode = NormalizeScheduleMode(request.ScheduleMode);
        channel.ScanIntervalMinutes = NormalizeScanInterval(request.ScanIntervalMinutes);
        channel.DailyScanTime = NormalizeDailyScanTime(request.DailyScanTime);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SetChannelEnabledAsync(long id, bool enabled, string? reason = null, CancellationToken ct = default)
    {
        var query = await _channels.QueryTrackingAsync(ct);
        var channel = await query.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (channel == null)
            throw new AtlasException($"BidOps crawl channel does not exist: {id}");

        channel.Enabled = enabled;
        if (enabled)
        {
            channel.LastError = string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(reason))
        {
            channel.LastError = $"Paused by user: {reason.Trim()}";
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<EnqueueJobDto> EnqueueMockScanAsync(long channelId, CancellationToken ct = default)
    {
        await _runtimeControl.EnsureTasksNotPausedAsync(ct);
        var tenant = RequireTenant();
        var userId = RequireUser();

        var channel = await _channels.FirstOrDefaultAsync(x => x.Id == channelId, ct);
        if (channel == null)
            throw new AtlasException($"BidOps crawl channel does not exist: {channelId}");

        var source = await _sources.FirstOrDefaultAsync(x => x.Id == channel.SourceId, ct);
        if (source == null)
            throw new AtlasException($"BidOps crawl source does not exist: {channel.SourceId}");

        if (string.Equals(source.SourceType, BidOpsCrawlSourceTypes.StateGridEcp, StringComparison.OrdinalIgnoreCase))
            return await EnqueueStateGridEcpScanAsync(channel, tenant, userId, ct);

        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<MockCrawlJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.MockCrawl,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = "BidOps mock public notice scan",
                TenantId = tenant,
                StoreId = _identity.StoreId,
                DeduplicationKey = BidOpsBackgroundJobDeduplicationKeys.ManualScan("mock", tenant, channel),
                Priority = BidOpsBackgroundJobPriorities.Manual,
                MaxAttempts = 3,
                Payload = new MockCrawlJobPayload(
                    tenant,
                    _identity.StoreId,
                    userId,
                    _identity.UserName,
                    channelId)
            },
            ct);

        return new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists);
    }

    private async Task<EnqueueJobDto> EnqueueStateGridEcpScanAsync(
        CrawlChannel channel,
        long tenant,
        long userId,
        CancellationToken ct)
    {
        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<StateGridEcpCrawlJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.StateGridEcpCrawl,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = "BidOps State Grid ECP public notice scan",
                TenantId = tenant,
                StoreId = _identity.StoreId,
                DeduplicationKey = BidOpsBackgroundJobDeduplicationKeys.ManualScan(
                    "state-grid-ecp",
                    tenant,
                    channel),
                Priority = BidOpsBackgroundJobPriorities.Manual,
                MaxAttempts = 3,
                Payload = new StateGridEcpCrawlJobPayload(
                    tenant,
                    _identity.StoreId,
                    userId,
                    _identity.UserName,
                    channel.Id)
            },
            ct);

        return new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists);
    }

    public async Task<EnqueueJobDto> StartBackfillAsync(
        long channelId,
        StartCrawlBackfillRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _runtimeControl.EnsureTasksNotPausedAsync(ct);
        var (tenant, userId, channel, source) = await LoadScanTargetAsync(channelId, ct);
        if (!string.Equals(source.SourceType, BidOpsCrawlSourceTypes.StateGridEcp, StringComparison.OrdinalIgnoreCase))
            throw new AtlasException("Historical backfill is currently supported for StateGridEcp channels.");

        var pageSize = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, 50);
        var maxPages = Math.Clamp(request.MaxPagesPerRun <= 0 ? 3 : request.MaxPagesPerRun, 1, 20);
        var startPage = Math.Max(1, request.StartPage);
        var checkpoint = await GetOrCreateCheckpointAsync(
            source.Id,
            channel.Id,
            BidOpsCrawlModes.Backfill,
            ct);

        if (request.ResetCursor)
        {
            ResetCheckpointState(checkpoint, startPage, request.StartPublishTime, request.EndPublishTime);
        }
        else
        {
            checkpoint.RangeStartPublishTime = request.StartPublishTime ?? checkpoint.RangeStartPublishTime;
            checkpoint.RangeEndPublishTime = request.EndPublishTime ?? checkpoint.RangeEndPublishTime;
            checkpoint.Status = BidOpsCrawlCheckpointStatuses.Idle;
            checkpoint.PausedAt = null;
            checkpoint.PauseReason = string.Empty;
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return await EnqueueStateGridEcpScanAsync(
            channel,
            tenant,
            userId,
            checkpoint,
            pageSize,
            maxPages,
            ct);
    }

    public async Task<EnqueueJobDto> ContinueCheckpointAsync(
        long channelId,
        ContinueCrawlCheckpointRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _runtimeControl.EnsureTasksNotPausedAsync(ct);
        var (tenant, userId, channel, source) = await LoadScanTargetAsync(channelId, ct);
        if (!string.Equals(source.SourceType, BidOpsCrawlSourceTypes.StateGridEcp, StringComparison.OrdinalIgnoreCase))
            throw new AtlasException("Checkpoint continuation is currently supported for StateGridEcp channels.");

        var mode = NormalizeCrawlMode(request.Mode);
        var checkpoint = await GetOrCreateCheckpointAsync(source.Id, channel.Id, mode, ct);
        if (checkpoint.Status == BidOpsCrawlCheckpointStatuses.Paused)
            throw new AtlasException("BidOps crawl checkpoint is paused. Resume it before continuing.");

        checkpoint.Status = BidOpsCrawlCheckpointStatuses.Idle;
        checkpoint.LastError = string.Empty;
        await _unitOfWork.SaveChangesAsync(ct);

        return await EnqueueStateGridEcpScanAsync(
            channel,
            tenant,
            userId,
            checkpoint,
            pageSize: null,
            request.MaxPages,
            ct);
    }

    public async Task PauseCheckpointAsync(
        long channelId,
        PauseCrawlCheckpointRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var channel = await _channels.FirstOrDefaultAsync(x => x.Id == channelId, ct);
        if (channel == null)
            throw new AtlasException($"BidOps crawl channel does not exist: {channelId}");

        var checkpoint = await GetOrCreateCheckpointAsync(
            channel.SourceId,
            channel.Id,
            NormalizeCrawlMode(request.Mode),
            ct);
        checkpoint.Status = BidOpsCrawlCheckpointStatuses.Paused;
        checkpoint.PausedAt = DateTime.UtcNow;
        checkpoint.PauseReason = string.IsNullOrWhiteSpace(request.Reason)
            ? "Paused by user"
            : request.Reason.Trim();
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<EnqueueJobDto> ResumeCheckpointAsync(
        long channelId,
        ContinueCrawlCheckpointRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _runtimeControl.EnsureTasksNotPausedAsync(ct);
        var (tenant, userId, channel, source) = await LoadScanTargetAsync(channelId, ct);
        var checkpoint = await GetOrCreateCheckpointAsync(
            source.Id,
            channel.Id,
            NormalizeCrawlMode(request.Mode),
            ct);
        checkpoint.Status = BidOpsCrawlCheckpointStatuses.Idle;
        checkpoint.PausedAt = null;
        checkpoint.PauseReason = string.Empty;
        checkpoint.LastError = string.Empty;
        await _unitOfWork.SaveChangesAsync(ct);

        return await EnqueueStateGridEcpScanAsync(
            channel,
            tenant,
            userId,
            checkpoint,
            pageSize: null,
            request.MaxPages,
            ct);
    }

    public async Task ResetCheckpointAsync(
        long channelId,
        ResetCrawlCheckpointRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var channel = await _channels.FirstOrDefaultAsync(x => x.Id == channelId, ct);
        if (channel == null)
            throw new AtlasException($"BidOps crawl channel does not exist: {channelId}");

        var checkpoint = await GetOrCreateCheckpointAsync(
            channel.SourceId,
            channel.Id,
            NormalizeCrawlMode(request.Mode),
            ct);
        ResetCheckpointState(
            checkpoint,
            Math.Max(1, request.NextPage),
            request.StartPublishTime,
            request.EndPublishTime);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<EnqueueJobDto> EnqueueManualUrlImportAsync(ImportPublicUrlRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _runtimeControl.EnsureTasksNotPausedAsync(ct);
        if (!Uri.TryCreate(request.DetailUrl?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new AtlasException("A public http/https notice URL is required.");
        }

        var tenant = RequireTenant();
        var userId = RequireUser();
        var urlHash = _hasher.HashUrl(uri.ToString());
        var deduplicationKey = request.ForceRefresh
            ? $"bidops:manual-url-refresh:{tenant}:{urlHash}:{Guid.NewGuid():N}"
            : $"bidops:manual-url:{tenant}:{urlHash}";

        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<ManualUrlImportJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.ManualUrlImport,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = request.ForceRefresh
                    ? "BidOps manual public URL refresh import"
                    : "BidOps manual public URL import",
                TenantId = tenant,
                StoreId = _identity.StoreId,
                DeduplicationKey = deduplicationKey,
                Priority = BidOpsBackgroundJobPriorities.Manual,
                MaxAttempts = 3,
                Payload = new ManualUrlImportJobPayload(
                    tenant,
                    _identity.StoreId,
                    userId,
                    _identity.UserName,
                    request.SourceId,
                    request.ChannelId,
                    uri.ToString(),
                    request.Title,
                    request.NoticeType,
                    request.TextContent,
                    request.ForceRefresh,
                    BidOpsJobProjectCode.FromText(request.Title, request.TextContent))
            },
            ct);

        return new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists);
    }

    public async Task<EnqueueJobDto> EnqueueRawAttachmentBackfillAsync(
        BackfillRawNoticeAttachmentsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _runtimeControl.EnsureTasksNotPausedAsync(ct);

        var tenant = RequireTenant();
        var userId = RequireUser();
        var maxItems = Math.Clamp(request.MaxItems <= 0 ? 100 : request.MaxItems, 1, 500);

        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<RawAttachmentBackfillJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.RawAttachmentBackfill,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = "BidOps historical raw attachment backfill",
                TenantId = tenant,
                StoreId = _identity.StoreId,
                DeduplicationKey = $"bidops:raw-attachment-backfill:{tenant}:{DateTime.UtcNow:yyyyMMddHHmmss}",
                Priority = BidOpsBackgroundJobPriorities.Manual,
                MaxAttempts = 1,
                Payload = new RawAttachmentBackfillJobPayload(
                    tenant,
                    _identity.StoreId,
                    userId,
                    _identity.UserName,
                    maxItems,
                    request.IncludeAlreadyProcessed,
                    request.ForceReparse)
            },
            ct);

        return new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists);
    }

    private async Task<EnqueueJobDto> EnqueueStateGridEcpScanAsync(
        CrawlChannel channel,
        long tenant,
        long userId,
        CrawlCheckpoint checkpoint,
        int? pageSize,
        int? maxPages,
        CancellationToken ct)
    {
        var nextPage = int.TryParse(checkpoint.NextCursor, out var parsedPage) && parsedPage > 0
            ? parsedPage
            : 1;
        var mode = NormalizeCrawlMode(checkpoint.Mode);
        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<StateGridEcpCrawlJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.StateGridEcpCrawl,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = mode == BidOpsCrawlModes.Backfill
                    ? "BidOps State Grid ECP historical backfill"
                    : "BidOps State Grid ECP public notice scan",
                TenantId = tenant,
                StoreId = _identity.StoreId,
                DeduplicationKey = BidOpsBackgroundJobDeduplicationKeys.CheckpointScan(
                    "state-grid-ecp",
                    tenant,
                    channel,
                    checkpoint),
                Priority = BidOpsBackgroundJobPriorities.Manual,
                MaxAttempts = 3,
                Payload = new StateGridEcpCrawlJobPayload(
                    tenant,
                    _identity.StoreId,
                    userId,
                    _identity.UserName,
                    channel.Id,
                    mode,
                    checkpoint.Id,
                    nextPage,
                    pageSize,
                    maxPages,
                    checkpoint.RangeStartPublishTime,
                    checkpoint.RangeEndPublishTime)
            },
            ct);

        return new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists);
    }

    private async Task<(long TenantId, long UserId, CrawlChannel Channel, CrawlSource Source)> LoadScanTargetAsync(
        long channelId,
        CancellationToken ct)
    {
        var tenant = RequireTenant();
        var userId = RequireUser();
        var channel = await _channels.FirstOrDefaultAsync(x => x.Id == channelId, ct);
        if (channel == null)
            throw new AtlasException($"BidOps crawl channel does not exist: {channelId}");

        if (!channel.Enabled)
            throw new AtlasException($"BidOps crawl channel is paused: {channel.Code}");

        var source = await _sources.FirstOrDefaultAsync(x => x.Id == channel.SourceId, ct);
        if (source == null)
            throw new AtlasException($"BidOps crawl source does not exist: {channel.SourceId}");

        if (!source.Enabled)
            throw new AtlasException($"BidOps crawl source is paused: {source.Code}");

        if (source.NeedLogin)
            throw new AtlasException("BidOps MVP does not support login-required sources.");

        return (tenant, userId, channel, source);
    }

    private async Task<CrawlCheckpoint> GetOrCreateCheckpointAsync(
        long sourceId,
        long channelId,
        string mode,
        CancellationToken ct)
    {
        mode = NormalizeCrawlMode(mode);
        var query = await _checkpoints.QueryTrackingAsync(ct);
        var checkpoint = await query
            .Where(x => x.ChannelId == channelId && x.Mode == mode)
            .FirstOrDefaultAsync(ct);
        if (checkpoint != null)
            return checkpoint;

        checkpoint = new CrawlCheckpoint
        {
            Id = _idGenerator.NextId(),
            SourceId = sourceId,
            ChannelId = channelId,
            Mode = mode,
            Status = BidOpsCrawlCheckpointStatuses.Idle,
            CursorKind = BidOpsCrawlCursorKinds.PageIndex,
            NextCursor = "1"
        };
        await _checkpoints.AddAsync(checkpoint, ct);
        return checkpoint;
    }

    private static void ResetCheckpointState(
        CrawlCheckpoint checkpoint,
        int nextPage,
        DateTime? startPublishTime,
        DateTime? endPublishTime)
    {
        checkpoint.Status = BidOpsCrawlCheckpointStatuses.Idle;
        checkpoint.CursorKind = BidOpsCrawlCursorKinds.PageIndex;
        checkpoint.NextCursor = Math.Max(1, nextPage).ToString();
        checkpoint.LastSuccessfulCursor = string.Empty;
        checkpoint.RangeStartPublishTime = startPublishTime;
        checkpoint.RangeEndPublishTime = endPublishTime;
        checkpoint.HighWatermarkPublishTime = null;
        checkpoint.LowWatermarkPublishTime = null;
        checkpoint.TotalRemoteCount = null;
        checkpoint.ScannedItemCount = 0;
        checkpoint.CreatedCount = 0;
        checkpoint.ChangedCount = 0;
        checkpoint.DuplicateCount = 0;
        checkpoint.FailedItemCount = 0;
        checkpoint.RemainingEstimate = null;
        checkpoint.StartedAt = null;
        checkpoint.LastRunAt = null;
        checkpoint.CompletedAt = null;
        checkpoint.PausedAt = null;
        checkpoint.PauseReason = string.Empty;
        checkpoint.LastError = string.Empty;
    }

    private static string NormalizeCrawlMode(string? mode)
    {
        return string.Equals(mode, BidOpsCrawlModes.Incremental, StringComparison.OrdinalIgnoreCase)
            ? BidOpsCrawlModes.Incremental
            : BidOpsCrawlModes.Backfill;
    }

    private long RequireTenant()
    {
        return _identity.TenantId
            ?? throw new AtlasException("Tenant context is required for BidOps operations.");
    }

    private long RequireUser()
    {
        return _identity.UserId
            ?? throw new AtlasException("Authenticated user context is required for BidOps operations.");
    }

    private static void ValidateSourceRequest(CreateCrawlSourceRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        if (request.NeedLogin)
            throw new AtlasException("BidOps MVP does not support login-required sources.");
    }

    private static void ValidateChannelRequest(CreateCrawlChannelRequest request)
    {
        if (request.SourceId <= 0)
            throw new AtlasException("SourceId is required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        var mode = NormalizeScheduleMode(request.ScheduleMode);
        if (mode == BidOpsCrawlScheduleModes.Daily &&
            string.IsNullOrWhiteSpace(NormalizeDailyScanTime(request.DailyScanTime)))
        {
            throw new AtlasException("Daily scan time is required when ScheduleMode is Daily.");
        }
    }

    private static string NormalizeScheduleMode(string? value)
    {
        return string.Equals(value, BidOpsCrawlScheduleModes.Daily, StringComparison.OrdinalIgnoreCase)
            ? BidOpsCrawlScheduleModes.Daily
            : BidOpsCrawlScheduleModes.Interval;
    }

    private static int? NormalizeScanInterval(int? value)
    {
        return value.HasValue && value.Value > 0 ? Math.Max(1, value.Value) : null;
    }

    private static string NormalizeDailyScanTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (!TimeSpan.TryParseExact(trimmed, @"hh\:mm", null, out var time) &&
            !TimeSpan.TryParseExact(trimmed, @"h\:mm", null, out time))
        {
            throw new AtlasException("Daily scan time must use HH:mm format.");
        }

        if (time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
            throw new AtlasException("Daily scan time must be between 00:00 and 23:59.");

        return $"{(int)time.TotalHours:00}:{time.Minutes:00}";
    }

    private static string NormalizeCode(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static CrawlSourceDto Map(CrawlSource source)
    {
        return new CrawlSourceDto
        {
            Id = source.Id,
            Code = source.Code,
            Name = source.Name,
            SourceType = source.SourceType,
            BaseUrl = source.BaseUrl,
            Enabled = source.Enabled,
            RateLimitPerMinute = source.RateLimitPerMinute,
            CrawlIntervalMinutes = source.CrawlIntervalMinutes,
            MaxRetryCount = source.MaxRetryCount,
            NeedLogin = source.NeedLogin,
            RespectRobots = source.RespectRobots,
            RobotsPolicyNote = source.RobotsPolicyNote,
            PauseReason = source.PauseReason
        };
    }

    private static CrawlChannelDto Map(CrawlChannel channel)
    {
        return new CrawlChannelDto
        {
            Id = channel.Id,
            SourceId = channel.SourceId,
            Code = channel.Code,
            Name = channel.Name,
            NoticeType = channel.NoticeType,
            ListUrl = channel.ListUrl,
            Region = channel.Region,
            Industry = channel.Industry,
            Enabled = channel.Enabled,
            ScheduleMode = channel.ScheduleMode,
            ScanIntervalMinutes = channel.ScanIntervalMinutes,
            DailyScanTime = channel.DailyScanTime,
            LastScanTime = channel.LastScanTime,
            LastSuccessTime = channel.LastSuccessTime,
            LastError = channel.LastError
        };
    }
}
