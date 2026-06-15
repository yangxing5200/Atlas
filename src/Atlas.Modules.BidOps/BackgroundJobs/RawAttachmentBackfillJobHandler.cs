using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class RawAttachmentBackfillJobHandler : IBackgroundJobHandler
{
    private const int MaxBackfillItems = 500;
    private const int MaxCandidateScanLimit = 5_000;

    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IRepository<CrawlSource> _sources;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<RawAttachment> _attachments;
    private readonly IRepository<Notice> _notices;
    private readonly IStateGridEcpCrawler _stateGridCrawler;
    private readonly IBackgroundJobClient _jobs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RawAttachmentBackfillJobHandler> _logger;

    public RawAttachmentBackfillJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IRepository<CrawlSource> sources,
        IRepository<RawNotice> rawNotices,
        IRepository<RawAttachment> attachments,
        IRepository<Notice> notices,
        IStateGridEcpCrawler stateGridCrawler,
        IBackgroundJobClient jobs,
        IUnitOfWork unitOfWork,
        ILogger<RawAttachmentBackfillJobHandler> logger)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _stateGridCrawler = stateGridCrawler ?? throw new ArgumentNullException(nameof(stateGridCrawler));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string JobType => BidOpsBackgroundJobTypes.RawAttachmentBackfill;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<RawAttachmentBackfillJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);

        var maxItems = Math.Clamp(payload.MaxItems <= 0 ? 100 : payload.MaxItems, 1, MaxBackfillItems);
        var candidates = await FindCandidatesAsync(maxItems, payload.IncludeAlreadyProcessed, ct);
        var refreshed = 0;
        var attachmentJobs = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var rawNoticeId = await _stateGridCrawler.ImportPublicDetailAsync(
                    candidate.DetailUrl,
                    candidate.SourceId,
                    candidate.ChannelId,
                    candidate.NoticeType,
                    context.Job.Id,
                    forceRefresh: false,
                    ct);
                if (!rawNoticeId.HasValue)
                {
                    skipped++;
                    continue;
                }

                if (!await MarkRawForReparseAsync(rawNoticeId.Value, payload.ForceReparse, context.Job.Id, ct))
                {
                    skipped++;
                    continue;
                }

                var forceParseRunId = payload.ForceReparse
                    ? $"raw-attachment-backfill-{context.Job.Id}-{rawNoticeId.Value}"
                    : null;
                await _jobs.EnqueueAsync(
                    new EnqueueBackgroundJobRequest<AttachmentProcessJobPayload>
                    {
                        JobType = BidOpsBackgroundJobTypes.AttachmentProcess,
                        Queue = BidOpsBackgroundJobQueues.BidOps,
                        JobName = "BidOps process backfilled raw notice attachments",
                        TenantId = payload.TenantId,
                        StoreId = payload.StoreId,
                        DeduplicationKey = $"bidops:historical-attachment-process:{payload.TenantId}:{rawNoticeId.Value}:{context.Job.Id}",
                        MaxAttempts = 3,
                        Payload = new AttachmentProcessJobPayload(
                            payload.TenantId,
                            payload.StoreId,
                            payload.UserId,
                            payload.UserName,
                            rawNoticeId.Value,
                            forceParseRunId)
                    },
                    ct);

                refreshed++;
                attachmentJobs++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                _logger.LogWarning(
                    ex,
                    "BidOps historical attachment backfill failed for raw notice {RawNoticeId}.",
                    candidate.RawNoticeId);
            }
        }

        return BackgroundJobExecutionResult.Success(
            $"candidates={candidates.Count};refreshed={refreshed};attachmentJobs={attachmentJobs};skipped={skipped};failed={failed}");
    }

    private async Task<IReadOnlyList<BackfillCandidate>> FindCandidatesAsync(
        int maxItems,
        bool includeAlreadyProcessed,
        CancellationToken ct)
    {
        var scanLimit = Math.Clamp(maxItems * 20, 100, MaxCandidateScanLimit);
        var rawQuery = await _rawNotices.QueryAsync(ct);
        var rawRows = await rawQuery
            .Where(x => x.DetailUrl.Contains("ecp.sgcc.com.cn") &&
                        x.DetailUrl.Contains("/doc/doci-bid/") &&
                        x.Status != RawNoticeStatus.Approved &&
                        x.Status != RawNoticeStatus.Ignored)
            .OrderBy(x => x.FetchTime)
            .Take(scanLimit)
            .SelectToListAsync(
                x => new RawProjection
                {
                    Id = x.Id,
                    SourceId = x.SourceId,
                    ChannelId = x.ChannelId,
                    DetailUrl = x.DetailUrl,
                    NoticeType = x.NoticeType
                },
                ct);

        if (rawRows.Count == 0)
            return Array.Empty<BackfillCandidate>();

        var rawIds = rawRows.Select(x => x.Id).ToArray();
        var formalQuery = await _notices.QueryAsync(ct);
        var formalRawIds = (await formalQuery
                .Where(x => rawIds.Contains(x.RawNoticeId))
                .SelectToListAsync(x => new RawNoticeIdProjection { Id = x.RawNoticeId }, ct))
            .Select(x => x.Id)
            .ToHashSet();

        var sourceIds = rawRows.Select(x => x.SourceId).Distinct().ToArray();
        var sourceQuery = await _sources.QueryAsync(ct);
        var sourceRows = await sourceQuery
            .Where(x => sourceIds.Contains(x.Id))
            .SelectToListAsync(
                x => new SourceProjection
                {
                    Id = x.Id,
                    SourceType = x.SourceType
                },
                ct);
        var sourceTypes = sourceRows.ToDictionary(x => x.Id, x => x.SourceType);

        var attachmentRows = await LoadAttachmentRowsAsync(rawIds, ct);
        var attachmentsByRawId = attachmentRows
            .GroupBy(x => x.RawNoticeId)
            .ToDictionary(x => x.Key, x => (IReadOnlyCollection<AttachmentProjection>)x.ToArray());

        var candidates = new List<BackfillCandidate>();
        foreach (var raw in rawRows)
        {
            if (formalRawIds.Contains(raw.Id) ||
                !IsTenderOrProcurement(raw.NoticeType) ||
                !StateGridEcpWcmParser.TryParsePortalDetailUrl(raw.DetailUrl, out var doctype, out _, out _) ||
                !string.Equals(doctype, "doci-bid", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            attachmentsByRawId.TryGetValue(raw.Id, out var attachments);
            if (!includeAlreadyProcessed && !NeedsAttachmentRefresh(attachments ?? Array.Empty<AttachmentProjection>()))
                continue;

            var sourceId = sourceTypes.TryGetValue(raw.SourceId, out var sourceType) &&
                           string.Equals(sourceType, BidOpsCrawlSourceTypes.StateGridEcp, StringComparison.OrdinalIgnoreCase)
                ? raw.SourceId
                : (long?)null;
            var channelId = sourceId.HasValue ? raw.ChannelId : null;
            candidates.Add(new BackfillCandidate(
                raw.Id,
                raw.DetailUrl,
                sourceId,
                channelId,
                raw.NoticeType));

            if (candidates.Count >= maxItems)
                break;
        }

        return candidates;
    }

    private async Task<IReadOnlyList<AttachmentProjection>> LoadAttachmentRowsAsync(
        IReadOnlyCollection<long> rawIds,
        CancellationToken ct)
    {
        if (rawIds.Count == 0)
            return Array.Empty<AttachmentProjection>();

        var attachmentQuery = await _attachments.QueryAsync(ct);
        return await attachmentQuery
            .Where(x => rawIds.Contains(x.RawNoticeId))
            .SelectToListAsync(
                x => new AttachmentProjection
                {
                    RawNoticeId = x.RawNoticeId,
                    DownloadStatus = x.DownloadStatus,
                    TextExtractStatus = x.TextExtractStatus,
                    StorageKey = x.StorageKey,
                    TextContentStorageKey = x.TextContentStorageKey
                },
                ct);
    }

    private async Task<bool> MarkRawForReparseAsync(
        long rawNoticeId,
        bool forceReparse,
        long backgroundJobId,
        CancellationToken ct)
    {
        var rawQuery = await _rawNotices.QueryTrackingAsync(ct);
        var raw = await rawQuery.Where(x => x.Id == rawNoticeId).FirstOrDefaultAsync(ct);
        if (raw == null ||
            raw.Status == RawNoticeStatus.Approved ||
            raw.Status == RawNoticeStatus.Ignored ||
            await _notices.FirstOrDefaultAsync(x => x.RawNoticeId == rawNoticeId, ct) != null)
        {
            return false;
        }

        if (forceReparse)
        {
            raw.Status = RawNoticeStatus.ParseQueued;
            raw.LastError = $"Historical attachment backfill queued attachment extraction and structured reparse. jobId={backgroundJobId}";
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return true;
    }

    private static bool NeedsAttachmentRefresh(IReadOnlyCollection<AttachmentProjection> attachments)
    {
        return attachments.Count == 0 ||
               attachments.Any(x =>
                   x.DownloadStatus != DownloadStatus.Succeeded ||
                   x.TextExtractStatus != TextExtractStatus.Succeeded ||
                   string.IsNullOrWhiteSpace(x.StorageKey) ||
                   string.IsNullOrWhiteSpace(x.TextContentStorageKey));
    }

    private static bool IsTenderOrProcurement(string noticeType)
    {
        return string.IsNullOrWhiteSpace(noticeType) ||
               noticeType.Contains("Tender", StringComparison.OrdinalIgnoreCase) ||
               noticeType.Contains("Procurement", StringComparison.OrdinalIgnoreCase) ||
               noticeType.Contains("招标", StringComparison.OrdinalIgnoreCase) ||
               noticeType.Contains("采购", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record BackfillCandidate(
        long RawNoticeId,
        string DetailUrl,
        long? SourceId,
        long? ChannelId,
        string NoticeType);

    private sealed class RawProjection
    {
        public long Id { get; init; }

        public long SourceId { get; init; }

        public long? ChannelId { get; init; }

        public string DetailUrl { get; init; } = string.Empty;

        public string NoticeType { get; init; } = string.Empty;
    }

    private sealed class SourceProjection
    {
        public long Id { get; init; }

        public string SourceType { get; init; } = string.Empty;
    }

    private sealed class RawNoticeIdProjection
    {
        public long Id { get; init; }
    }

    private sealed class AttachmentProjection
    {
        public long RawNoticeId { get; init; }

        public DownloadStatus DownloadStatus { get; init; }

        public TextExtractStatus TextExtractStatus { get; init; }

        public string StorageKey { get; init; } = string.Empty;

        public string TextContentStorageKey { get; init; } = string.Empty;
    }
}
