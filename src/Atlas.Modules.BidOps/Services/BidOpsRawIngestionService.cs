using System.Text;
using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsRawIngestionService : IBidOpsRawIngestionService
{
    private const int TextPreviewMaxLength = 200;

    private readonly IRepository<CrawlSource> _sources;
    private readonly IRepository<CrawlChannel> _channels;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<RawAttachment> _rawAttachments;
    private readonly IRepository<CrawlRunLog> _logs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBidOpsFileStore _fileStore;
    private readonly BidOpsContentHasher _hasher;
    private readonly IIdGenerator _idGenerator;

    public BidOpsRawIngestionService(
        IRepository<CrawlSource> sources,
        IRepository<CrawlChannel> channels,
        IRepository<RawNotice> rawNotices,
        IRepository<RawAttachment> rawAttachments,
        IRepository<CrawlRunLog> logs,
        IUnitOfWork unitOfWork,
        IBidOpsFileStore fileStore,
        BidOpsContentHasher hasher,
        IIdGenerator idGenerator)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _rawAttachments = rawAttachments ?? throw new ArgumentNullException(nameof(rawAttachments));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<long> ImportManualUrlAsync(
        RawIngestionCommand command,
        long? backgroundJobId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.DetailUrl);

        var source = command.SourceId.HasValue
            ? await GetSourceAsync(command.SourceId.Value, ct)
            : await GetOrCreateManualSourceAsync(ct);

        EnsureSourceCanRun(source);

        var title = string.IsNullOrWhiteSpace(command.Title)
            ? $"Manual public notice import {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
            : command.Title.Trim();
        var text = string.IsNullOrWhiteSpace(command.TextContent)
            ? $"Manual public URL imported for review: {command.DetailUrl}"
            : command.TextContent.Trim();
        var html = string.IsNullOrWhiteSpace(command.HtmlContent)
            ? $"<html><body><h1>{title}</h1><p>{text}</p></body></html>"
            : command.HtmlContent;

        var rawId = await UpsertRawNoticeAsync(
            source.Id,
            command.ChannelId,
            title,
            command.DetailUrl,
            string.IsNullOrWhiteSpace(command.NoticeType) ? "TenderAnnouncement" : command.NoticeType.Trim(),
            text,
            html,
            command.PublishTime,
            command.Attachments,
            backgroundJobId,
            "ManualUrlImport",
            command.ForceRefresh,
            ct);

        return rawId;
    }

    public async Task<long> IngestPublicNoticeAsync(
        RawIngestionCommand command,
        long? backgroundJobId,
        string operation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.DetailUrl);
        if (!command.SourceId.HasValue)
            throw new AtlasException("SourceId is required for public crawler ingestion.");

        var source = await GetSourceAsync(command.SourceId.Value, ct);
        EnsureSourceCanRun(source);

        var title = string.IsNullOrWhiteSpace(command.Title)
            ? $"Public notice import {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
            : command.Title.Trim();
        var text = string.IsNullOrWhiteSpace(command.TextContent)
            ? $"Public URL imported for review: {command.DetailUrl}"
            : command.TextContent.Trim();
        var html = string.IsNullOrWhiteSpace(command.HtmlContent)
            ? $"<html><body><h1>{title}</h1><p>{text}</p></body></html>"
            : command.HtmlContent;

        return await UpsertRawNoticeAsync(
            source.Id,
            command.ChannelId,
            title,
            command.DetailUrl,
            string.IsNullOrWhiteSpace(command.NoticeType) ? "TenderAnnouncement" : command.NoticeType.Trim(),
            text,
            html,
            command.PublishTime,
            command.Attachments,
            backgroundJobId,
            string.IsNullOrWhiteSpace(operation) ? "PublicCrawler" : operation.Trim(),
            command.ForceRefresh,
            ct);
    }

    public async Task<long> CreateMockRawNoticeAsync(
        long channelId,
        long? backgroundJobId,
        CancellationToken ct = default)
    {
        var channelQuery = await _channels.QueryTrackingAsync(ct);
        var channel = await channelQuery.Where(x => x.Id == channelId).FirstOrDefaultAsync(ct);
        if (channel == null)
            throw new AtlasException($"BidOps crawl channel does not exist: {channelId}");

        var source = await GetSourceAsync(channel.SourceId, ct);
        EnsureSourceCanRun(source);
        if (!channel.Enabled)
            throw new AtlasException($"BidOps crawl channel is paused: {channel.Code}");

        var minIntervalSeconds = Math.Max(1, 60 / Math.Max(1, source.RateLimitPerMinute));
        if (channel.LastScanTime.HasValue &&
            channel.LastScanTime.Value.AddSeconds(minIntervalSeconds) > DateTime.UtcNow)
        {
            await AddLogAsync(source.Id, channel.Id, backgroundJobId, "MockCrawl", "RateLimited", "Source rate limit prevented this scan.", ct);
            await _unitOfWork.SaveChangesAsync(ct);
            throw new AtlasException("BidOps source rate limit prevented this scan.");
        }

        channel.LastScanTime = DateTime.UtcNow;

        var title = $"公开招标公告样例 {DateTime.UtcNow:yyyyMMddHHmmss}";
        var detailUrl = $"mock://public-tender/{channel.Code}/{DateTime.UtcNow:yyyyMMddHHmmss}";
        var text = """
公开招标公告样例。
采购内容：设备与相关服务。
供应商应具备履行合同所必需的设备和专业技术能力。
投标文件递交截止时间以公告载明时间为准。
""";
        var html = $"<html><body><h1>{title}</h1><pre>{text}</pre></body></html>";

        var rawId = await UpsertRawNoticeAsync(
            source.Id,
            channel.Id,
            title,
            detailUrl,
            channel.NoticeType,
            text,
            html,
            DateTime.UtcNow,
            Array.Empty<RawAttachmentCandidate>(),
            backgroundJobId,
            "MockCrawl",
            forceRefresh: false,
            ct);

        channel.LastSuccessTime = DateTime.UtcNow;
        channel.LastError = string.Empty;
        await _unitOfWork.SaveChangesAsync(ct);

        return rawId;
    }

    private async Task<long> UpsertRawNoticeAsync(
        long sourceId,
        long? channelId,
        string title,
        string detailUrl,
        string noticeType,
        string text,
        string html,
        DateTime? publishTime,
        IReadOnlyList<RawAttachmentCandidate>? attachments,
        long? backgroundJobId,
        string operation,
        bool forceRefresh,
        CancellationToken ct)
    {
        var urlHash = _hasher.HashUrl(detailUrl);
        var contentHash = _hasher.HashText(text);
        var existing = await _rawNotices.FirstOrDefaultAsync(
            x => x.SourceId == sourceId && x.DetailUrlHash == urlHash,
            ct);
        if (existing != null)
        {
            if (forceRefresh && existing.Status == RawNoticeStatus.Approved)
                throw new AtlasException("Approved BidOps raw notices cannot be force-refreshed in MVP.");

            var contentChanged = !string.Equals(existing.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase);
            if (contentChanged || forceRefresh)
            {
                var changedHtmlInfo = await SaveTextAsync(html, "notice.html", "text/html", ct);
                var changedTextInfo = await SaveTextAsync(text, "notice.txt", "text/plain", ct);

                existing.Title = Trim(title, 500);
                existing.DetailUrl = detailUrl.Trim();
                existing.NoticeType = Trim(noticeType, 64);
                existing.PublishTime = publishTime;
                existing.FetchTime = DateTime.UtcNow;
                existing.ContentHash = contentHash;
                existing.StorageProvider = changedTextInfo.StorageProvider;
                existing.HtmlSnapshotStorageKey = changedHtmlInfo.StorageKey;
                existing.TextContentStorageKey = changedTextInfo.StorageKey;
                existing.TextPreview = Trim(text, TextPreviewMaxLength);
                existing.Status = RawNoticeStatus.ParseQueued;
                existing.LastError = contentChanged
                    ? "Public notice content changed; staging review is required."
                    : "Public notice was force-refreshed; staging review is required.";

                await AddLogAsync(
                    sourceId,
                    channelId,
                    backgroundJobId,
                    operation,
                    contentChanged ? "Changed" : "Refreshed",
                    contentChanged
                        ? $"Raw notice content changed: {existing.Id}"
                        : $"Raw notice force-refreshed: {existing.Id}",
                    ct);
            }
            else
            {
                await AddLogAsync(sourceId, channelId, backgroundJobId, operation, "Skipped", $"Raw notice already exists: {existing.Id}", ct);
            }

            await UpsertRawAttachmentsAsync(existing.Id, attachments, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return existing.Id;
        }

        var htmlInfo = await SaveTextAsync(html, "notice.html", "text/html", ct);
        var textInfo = await SaveTextAsync(text, "notice.txt", "text/plain", ct);
        var raw = new RawNotice
        {
            Id = _idGenerator.NextId(),
            SourceId = sourceId,
            ChannelId = channelId,
            SourceNoticeId = urlHash[..16],
            Title = Trim(title, 500),
            DetailUrl = detailUrl.Trim(),
            DetailUrlHash = urlHash,
            NoticeType = Trim(noticeType, 64),
            PublishTime = publishTime,
            FetchTime = DateTime.UtcNow,
            ContentHash = contentHash,
            StorageProvider = textInfo.StorageProvider,
            HtmlSnapshotStorageKey = htmlInfo.StorageKey,
            TextContentStorageKey = textInfo.StorageKey,
            TextPreview = Trim(text, TextPreviewMaxLength),
            Status = RawNoticeStatus.ParseQueued
        };

        await _rawNotices.AddAsync(raw, ct);
        await UpsertRawAttachmentsAsync(raw.Id, attachments, ct);
        await AddLogAsync(sourceId, channelId, backgroundJobId, operation, "Succeeded", $"Raw notice created: {raw.Id}", ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return raw.Id;
    }

    private async Task UpsertRawAttachmentsAsync(
        long rawNoticeId,
        IReadOnlyList<RawAttachmentCandidate>? attachments,
        CancellationToken ct)
    {
        if (attachments == null || attachments.Count == 0)
            return;

        foreach (var candidate in attachments)
        {
            if (string.IsNullOrWhiteSpace(candidate.FileUrl) ||
                !Uri.TryCreate(candidate.FileUrl.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https"))
            {
                continue;
            }

            var normalizedUrl = uri.ToString();
            var fileHash = _hasher.HashUrl(normalizedUrl);
            var existing = await _rawAttachments.FirstOrDefaultAsync(
                x => x.RawNoticeId == rawNoticeId && (x.FileHash == fileHash || x.FileUrl == normalizedUrl),
                ct);
            if (existing != null)
            {
                existing.FileUrl = Trim(normalizedUrl, 1500);
                existing.FileHash = fileHash;
                if (!string.IsNullOrWhiteSpace(candidate.FileName))
                    existing.FileName = Trim(candidate.FileName.Trim(), 500);
                existing.FileType = Trim(NormalizeFileType(candidate.FileType, normalizedUrl), 64);
                existing.FileSize = candidate.FileSize ?? existing.FileSize;
                continue;
            }

            var fileName = string.IsNullOrWhiteSpace(candidate.FileName)
                ? Path.GetFileName(uri.LocalPath)
                : candidate.FileName.Trim();
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "bidops-attachment";

            await _rawAttachments.AddAsync(new RawAttachment
            {
                Id = _idGenerator.NextId(),
                RawNoticeId = rawNoticeId,
                FileName = Trim(fileName, 500),
                FileUrl = Trim(normalizedUrl, 1500),
                FileType = Trim(NormalizeFileType(candidate.FileType, normalizedUrl), 64),
                FileSize = candidate.FileSize,
                FileHash = fileHash,
                StorageProvider = BidOpsSystemValues.LocalStorageProvider,
                DownloadStatus = DownloadStatus.Pending,
                TextExtractStatus = TextExtractStatus.Pending
            }, ct);
        }
    }

    private async Task<CrawlSource> GetOrCreateManualSourceAsync(CancellationToken ct)
    {
        var source = await _sources.FirstOrDefaultAsync(x => x.Code == BidOpsSystemValues.ManualSourceCode, ct);
        if (source != null)
            return source;

        source = new CrawlSource
        {
            Id = _idGenerator.NextId(),
            Code = BidOpsSystemValues.ManualSourceCode,
            Name = "Manual public URL import",
            SourceType = "Manual",
            BaseUrl = "manual://public-url",
            Enabled = true,
            RateLimitPerMinute = 10,
            CrawlIntervalMinutes = 60,
            MaxRetryCount = 3,
            NeedLogin = false,
            RespectRobots = true,
            RobotsPolicyNote = "Manual public URLs only. No login, captcha, or bypass behavior is supported."
        };

        await _sources.AddAsync(source, ct);
        return source;
    }

    private async Task<CrawlSource> GetSourceAsync(long sourceId, CancellationToken ct)
    {
        var source = await _sources.FirstOrDefaultAsync(x => x.Id == sourceId, ct);
        if (source == null)
            throw new AtlasException($"BidOps crawl source does not exist: {sourceId}");

        return source;
    }

    private static void EnsureSourceCanRun(CrawlSource source)
    {
        if (!source.Enabled)
            throw new AtlasException($"BidOps crawl source is paused: {source.Code}");

        if (source.NeedLogin)
            throw new AtlasException("BidOps MVP does not run login-required sources.");
    }

    private async Task AddLogAsync(
        long? sourceId,
        long? channelId,
        long? backgroundJobId,
        string operation,
        string status,
        string message,
        CancellationToken ct)
    {
        await _logs.AddAsync(new CrawlRunLog
        {
            Id = _idGenerator.NextId(),
            SourceId = sourceId,
            ChannelId = channelId,
            BackgroundJobId = backgroundJobId,
            Operation = operation,
            Status = status,
            Message = Trim(message, 2000)
        }, ct);
    }

    private async Task<StoredFileInfo> SaveTextAsync(
        string text,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await using var stream = new MemoryStream(bytes);
        return await _fileStore.SaveAsync(stream, fileName, contentType, ct);
    }

    private static string Trim(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string NormalizeFileType(string? fileType, string fileUrl)
    {
        if (!string.IsNullOrWhiteSpace(fileType))
            return fileType.Trim().TrimStart('.').ToLowerInvariant();

        var path = fileUrl.Split('?', '#')[0];
        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(extension) ? "file" : extension;
    }
}
