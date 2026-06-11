using Atlas.BackgroundTasks;
using Atlas.Core.Exceptions;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _jobs;
    private readonly ICurrentIdentity _identity;
    private readonly BidOpsContentHasher _hasher;

    public BidOpsCrawlService(
        IRepository<CrawlSource> sources,
        IRepository<CrawlChannel> channels,
        IUnitOfWork unitOfWork,
        IBackgroundJobClient jobs,
        ICurrentIdentity identity,
        BidOpsContentHasher hasher)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
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
            Enabled = request.Enabled
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
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<EnqueueJobDto> EnqueueMockScanAsync(long channelId, CancellationToken ct = default)
    {
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
                DeduplicationKey = $"bidops:mock-scan:{tenant}:{channelId}:{DateTime.UtcNow:yyyyMMddHHmm}",
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
                DeduplicationKey = $"bidops:state-grid-ecp-scan:{tenant}:{channel.Id}:{DateTime.UtcNow:yyyyMMddHHmm}",
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

    public async Task<EnqueueJobDto> EnqueueManualUrlImportAsync(ImportPublicUrlRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Uri.TryCreate(request.DetailUrl?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new AtlasException("A public http/https notice URL is required.");
        }

        var tenant = RequireTenant();
        var userId = RequireUser();
        var urlHash = _hasher.HashUrl(uri.ToString());

        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<ManualUrlImportJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.ManualUrlImport,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = "BidOps manual public URL import",
                TenantId = tenant,
                StoreId = _identity.StoreId,
                DeduplicationKey = $"bidops:manual-url:{tenant}:{urlHash}",
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
                    request.TextContent)
            },
            ct);

        return new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists);
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
            LastScanTime = channel.LastScanTime,
            LastSuccessTime = channel.LastSuccessTime,
            LastError = channel.LastError
        };
    }
}
