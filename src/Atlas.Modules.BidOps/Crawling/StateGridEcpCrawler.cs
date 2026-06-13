using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Atlas.Modules.BidOps.Crawling;

public sealed class StateGridEcpCrawler : IStateGridEcpCrawler
{
    private const string OperationName = "StateGridEcpCrawl";
    private const int DefaultMaxNoticesPerScan = 10;
    private const int MaxNoticesPerScanLimit = 50;
    private const int MaxResponseCharacters = 500_000;

    private readonly HttpClient _httpClient;
    private readonly IRepository<CrawlSource> _sources;
    private readonly IRepository<CrawlChannel> _channels;
    private readonly IRepository<CrawlRunLog> _logs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBidOpsRawIngestionService _ingestion;
    private readonly IIdGenerator _idGenerator;
    private readonly ILogger<StateGridEcpCrawler> _logger;
    private readonly int _maxNoticesPerScan;

    public StateGridEcpCrawler(
        HttpClient httpClient,
        IRepository<CrawlSource> sources,
        IRepository<CrawlChannel> channels,
        IRepository<CrawlRunLog> logs,
        IUnitOfWork unitOfWork,
        IBidOpsRawIngestionService ingestion,
        IIdGenerator idGenerator,
        IConfiguration configuration,
        ILogger<StateGridEcpCrawler> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _ingestion = ingestion ?? throw new ArgumentNullException(nameof(ingestion));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxNoticesPerScan = Math.Clamp(
            configuration.GetValue<int?>("BidOps:StateGridEcp:MaxNoticesPerScan") ?? DefaultMaxNoticesPerScan,
            1,
            MaxNoticesPerScanLimit);
    }

    public async Task<StateGridEcpCrawlResult> CrawlAsync(
        long channelId,
        long? backgroundJobId,
        CancellationToken ct = default)
    {
        var channelQuery = await _channels.QueryTrackingAsync(ct);
        var channel = await channelQuery.Where(x => x.Id == channelId).FirstOrDefaultAsync(ct);
        if (channel == null)
            throw new AtlasException($"BidOps crawl channel does not exist: {channelId}");

        var source = await GetSourceAsync(channel.SourceId, ct);
        EnsureCanRun(source, channel);
        EnsureRateLimit(source, channel);

        channel.LastScanTime = DateTime.UtcNow;

        try
        {
            return StateGridEcpWcmParser.TryGetMenuId(channel.ListUrl, out var menuId)
                ? await CrawlWcmApiChannelAsync(source, channel, menuId, backgroundJobId, ct)
                : await CrawlHtmlChannelAsync(source, channel, backgroundJobId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            channel.LastError = ex.Message;
            await AddLogAsync(source.Id, channel.Id, backgroundJobId, "Failed", ex.Message, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            _logger.LogWarning(ex, "State Grid ECP crawl failed for channel {ChannelId}.", channel.Id);
            throw;
        }
    }

    public async Task<long?> ImportPublicDetailAsync(
        string detailUrl,
        long? sourceId,
        long? channelId,
        string? noticeType,
        long? backgroundJobId,
        CancellationToken ct = default)
    {
        if (!StateGridEcpWcmParser.TryParsePortalDetailUrl(detailUrl, out var doctype, out var noticeId, out var menuId))
            return null;

        if (!Uri.TryCreate(detailUrl.Trim(), UriKind.Absolute, out var detailUri) ||
            !detailUri.Host.EndsWith("sgcc.com.cn", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var (source, channel) = await ResolvePublicDetailTargetAsync(sourceId, channelId, detailUri, menuId, ct);
        if (channel == null)
        {
            EnsureSourceCanRun(source);
        }
        else
        {
            EnsureCanRun(source, channel);
        }

        EnsureAllowedStateGridUri(source, detailUri);
        await DelayForRateLimitAsync(source, ct);

        var notice = new StateGridEcpApiNotice(
            string.Empty,
            detailUri.ToString(),
            doctype,
            menuId,
            noticeId,
            null,
            null,
            string.Empty,
            string.Empty);

        var document = await FetchApiDetailDocumentAsync(source, channel, notice, backgroundJobId, ct);
        var rawId = await _ingestion.IngestPublicNoticeAsync(
            new RawIngestionCommand(
                source.Id,
                channel?.Id,
                detailUri.ToString(),
                document.Title,
                string.IsNullOrWhiteSpace(noticeType) ? channel?.NoticeType ?? "TenderAnnouncement" : noticeType.Trim(),
                document.Text,
                document.Html,
                document.PublishTime,
                MapAttachments(document.Attachments)),
            backgroundJobId,
            OperationName,
            ct);

        await AddLogAsync(
            source.Id,
            channel?.Id,
            backgroundJobId,
            "Succeeded",
            $"State Grid public detail import completed. doctype={doctype}, noticeId={noticeId}, attachments={document.Attachments.Count}.",
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return rawId;
    }

    private async Task<StateGridEcpCrawlResult> CrawlWcmApiChannelAsync(
        CrawlSource source,
        CrawlChannel channel,
        string menuId,
        long? backgroundJobId,
        CancellationToken ct)
    {
        var notices = await FetchApiNoticeListAsync(source, menuId, ct);
        if (notices.Count == 0)
        {
            channel.LastSuccessTime = DateTime.UtcNow;
            channel.LastError = string.Empty;
            await AddLogAsync(
                source.Id,
                channel.Id,
                backgroundJobId,
                "Skipped",
                $"No public State Grid notices were returned for menu {menuId}.",
                ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return new StateGridEcpCrawlResult(source.Id, channel.Id, 0, 0, Array.Empty<long>());
        }

        var rawIds = new List<long>();
        foreach (var notice in notices)
        {
            ct.ThrowIfCancellationRequested();
            await DelayForRateLimitAsync(source, ct);

            try
            {
                var document = await FetchApiDetailDocumentAsync(source, channel, notice, backgroundJobId, ct);
                var rawId = await _ingestion.IngestPublicNoticeAsync(
                    new RawIngestionCommand(
                        source.Id,
                        channel.Id,
                        notice.DetailUrl,
                        document.Title,
                        channel.NoticeType,
                        document.Text,
                        document.Html,
                        document.PublishTime,
                        MapAttachments(document.Attachments)),
                    backgroundJobId,
                    OperationName,
                    ct);
                rawIds.Add(rawId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await AddLogAsync(
                    source.Id,
                    channel.Id,
                    backgroundJobId,
                    "NoticeFailed",
                    $"Failed to ingest State Grid API notice {notice.NoticeId}: {ex.Message}",
                    ct);
            }
        }

        channel.LastSuccessTime = DateTime.UtcNow;
        channel.LastError = string.Empty;
        await AddLogAsync(
            source.Id,
            channel.Id,
            backgroundJobId,
            "Succeeded",
            $"State Grid WCM API crawl completed. menu={menuId}, discovered={notices.Count}, ingested={rawIds.Count}.",
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new StateGridEcpCrawlResult(source.Id, channel.Id, notices.Count, rawIds.Count, rawIds);
    }

    private async Task<StateGridEcpCrawlResult> CrawlHtmlChannelAsync(
        CrawlSource source,
        CrawlChannel channel,
        long? backgroundJobId,
        CancellationToken ct)
    {
        var listUri = BuildListUri(source, channel);
        EnsureAllowedStateGridUri(source, listUri);

        var listHtml = await FetchStringAsync(listUri, source, ct);
        var notices = StateGridEcpHtmlParser.DiscoverNotices(listHtml, listUri, _maxNoticesPerScan)
            .Where(notice =>
                Uri.TryCreate(notice.DetailUrl, UriKind.Absolute, out var detailUri) &&
                IsAllowedStateGridUri(source, detailUri))
            .ToArray();

        if (notices.Length == 0)
        {
            channel.LastSuccessTime = DateTime.UtcNow;
            channel.LastError = string.Empty;
            await AddLogAsync(
                source.Id,
                channel.Id,
                backgroundJobId,
                "Skipped",
                $"No public State Grid notice links were discovered from {listUri}.",
                ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return new StateGridEcpCrawlResult(source.Id, channel.Id, 0, 0, Array.Empty<long>());
        }

        var rawIds = new List<long>();
        foreach (var notice in notices)
        {
            ct.ThrowIfCancellationRequested();
            await DelayForRateLimitAsync(source, ct);

            try
            {
                var detailUri = new Uri(notice.DetailUrl);
                var detailHtml = await FetchStringAsync(detailUri, source, ct);
                var document = StateGridEcpHtmlParser.ExtractDetail(
                    detailHtml,
                    notice.Title,
                    notice.PublishTime);
                document = document with
                {
                    Attachments = StateGridEcpHtmlParser.DiscoverAttachments(detailHtml, detailUri)
                };

                var rawId = await _ingestion.IngestPublicNoticeAsync(
                    new RawIngestionCommand(
                        source.Id,
                        channel.Id,
                        detailUri.ToString(),
                        document.Title,
                        channel.NoticeType,
                        document.Text,
                        document.Html,
                        document.PublishTime,
                        MapAttachments(document.Attachments)),
                    backgroundJobId,
                    OperationName,
                    ct);
                rawIds.Add(rawId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await AddLogAsync(
                    source.Id,
                    channel.Id,
                    backgroundJobId,
                    "NoticeFailed",
                    $"Failed to ingest State Grid HTML notice {notice.DetailUrl}: {ex.Message}",
                    ct);
            }
        }

        channel.LastSuccessTime = DateTime.UtcNow;
        channel.LastError = string.Empty;
        await AddLogAsync(
            source.Id,
            channel.Id,
            backgroundJobId,
            "Succeeded",
            $"State Grid HTML crawl completed. discovered={notices.Length}, ingested={rawIds.Count}.",
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new StateGridEcpCrawlResult(source.Id, channel.Id, notices.Length, rawIds.Count, rawIds);
    }

    private async Task<CrawlSource> GetSourceAsync(long sourceId, CancellationToken ct)
    {
        var source = await _sources.FirstOrDefaultAsync(x => x.Id == sourceId, ct);
        if (source == null)
            throw new AtlasException($"BidOps crawl source does not exist: {sourceId}");

        return source;
    }

    private async Task<(CrawlSource Source, CrawlChannel? Channel)> ResolvePublicDetailTargetAsync(
        long? sourceId,
        long? channelId,
        Uri detailUri,
        string menuId,
        CancellationToken ct)
    {
        if (channelId.HasValue)
        {
            var channelQuery = await _channels.QueryTrackingAsync(ct);
            var channel = await channelQuery.Where(x => x.Id == channelId.Value).FirstOrDefaultAsync(ct);
            if (channel == null)
                throw new AtlasException($"BidOps crawl channel does not exist: {channelId.Value}");

            if (sourceId.HasValue && channel.SourceId != sourceId.Value)
                throw new AtlasException("State Grid manual import sourceId does not match channel source.");

            return (await GetSourceAsync(channel.SourceId, ct), channel);
        }

        var source = sourceId.HasValue
            ? await GetSourceAsync(sourceId.Value, ct)
            : await FindStateGridSourceAsync(detailUri, ct)
                ?? throw new AtlasException("No enabled State Grid ECP crawl source is configured for this tenant.");
        var matchedChannel = await FindStateGridChannelAsync(source.Id, menuId, ct);
        return (source, matchedChannel);
    }

    private async Task<CrawlSource?> FindStateGridSourceAsync(Uri detailUri, CancellationToken ct)
    {
        var sourceQuery = await _sources.QueryTrackingAsync(ct);
        var sources = await sourceQuery
            .Where(x => x.Enabled && !x.NeedLogin && x.SourceType == BidOpsCrawlSourceTypes.StateGridEcp)
            .ToListAsync(ct);

        return sources
            .Where(x => IsAllowedStateGridUri(x, detailUri))
            .OrderBy(x => string.Equals(x.Code, BidOpsSystemValues.StateGridEcpSourceCode, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x.Priority)
            .FirstOrDefault();
    }

    private async Task<CrawlChannel?> FindStateGridChannelAsync(long sourceId, string menuId, CancellationToken ct)
    {
        var channelQuery = await _channels.QueryTrackingAsync(ct);
        var channels = await channelQuery
            .Where(x => x.SourceId == sourceId && x.Enabled)
            .ToListAsync(ct);

        return channels
            .OrderBy(x =>
                !string.IsNullOrWhiteSpace(menuId) &&
                x.ListUrl.Contains(menuId, StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : 1)
            .ThenBy(x => x.LastScanTime.HasValue ? 1 : 0)
            .FirstOrDefault();
    }

    private async Task<string> FetchStringAsync(Uri uri, CrawlSource source, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        AddCommonHeaders(request, source, "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        return content.Length <= MaxResponseCharacters
            ? content
            : content[..MaxResponseCharacters];
    }

    private async Task<IReadOnlyList<StateGridEcpApiNotice>> FetchApiNoticeListAsync(
        CrawlSource source,
        string menuId,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            index = 1,
            size = _maxNoticesPerScan,
            firstPageMenuId = menuId,
            purOrgStatus = string.Empty,
            purOrgCode = string.Empty,
            purType = string.Empty,
            noticeType = string.Empty,
            orgId = string.Empty,
            key = string.Empty,
            orgName = string.Empty
        });
        var json = await PostJsonAsync(BuildApiUri(source, "index/noteList"), source, payload, ct);
        return StateGridEcpWcmParser.ParseNoticeList(json, source.BaseUrl, _maxNoticesPerScan);
    }

    private async Task<StateGridNoticeDocument> FetchApiDetailDocumentAsync(
        CrawlSource source,
        CrawlChannel? channel,
        StateGridEcpApiNotice notice,
        long? backgroundJobId,
        CancellationToken ct)
    {
        try
        {
            var payload = JsonSerializer.Serialize(notice.NoticeId);
            var json = await PostJsonAsync(
                BuildApiUri(source, StateGridEcpWcmParser.GetDetailApiPath(notice.Doctype)),
                source,
                payload,
                ct);
            var document = StateGridEcpWcmParser.ParseNoticeDetail(json, notice);
            var apiAttachments = await FetchApiAttachmentDocumentsAsync(source, channel, notice, backgroundJobId, ct);
            return document with
            {
                Attachments = MergeAttachments(document.Attachments, apiAttachments)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await AddLogAsync(
                source.Id,
                channel?.Id,
                backgroundJobId,
                "DetailFallback",
                $"State Grid detail API failed for notice {notice.NoticeId}; using list metadata. {ex.Message}",
                ct);
            return StateGridEcpWcmParser.CreateFallbackDocument(notice);
        }
    }

    private async Task<IReadOnlyList<StateGridAttachmentDocument>> FetchApiAttachmentDocumentsAsync(
        CrawlSource source,
        CrawlChannel? channel,
        StateGridEcpApiNotice notice,
        long? backgroundJobId,
        CancellationToken ct)
    {
        var attachmentApiPath = StateGridEcpWcmParser.GetAttachmentApiPath(notice.Doctype);
        if (string.IsNullOrWhiteSpace(attachmentApiPath))
            return Array.Empty<StateGridAttachmentDocument>();

        try
        {
            var payload = JsonSerializer.Serialize(notice.NoticeId);
            var json = await PostJsonAsync(BuildApiUri(source, attachmentApiPath), source, payload, ct);
            return StateGridEcpWcmParser.ParseNoticeFileList(json, notice, source.BaseUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await AddLogAsync(
                source.Id,
                channel?.Id,
                backgroundJobId,
                "AttachmentListFailed",
                $"State Grid attachment API failed for notice {notice.NoticeId}: {ex.Message}",
                ct);
            return Array.Empty<StateGridAttachmentDocument>();
        }
    }

    private async Task<string> PostJsonAsync(
        Uri uri,
        CrawlSource source,
        string jsonBody,
        CancellationToken ct)
    {
        EnsureAllowedStateGridUri(source, uri);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        AddCommonHeaders(request, source, "application/json, text/plain, */*");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        return content.Length <= MaxResponseCharacters
            ? content
            : content[..MaxResponseCharacters];
    }

    private static Uri BuildApiUri(CrawlSource source, string apiPath)
    {
        if (!Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var baseUri))
            throw new AtlasException("State Grid source BaseUrl must be an absolute public URL.");

        var builder = new UriBuilder(baseUri)
        {
            Path = $"/ecp2.0/ecpwcmcore/{apiPath.TrimStart('/')}",
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri;
    }

    private static Uri BuildListUri(CrawlSource source, CrawlChannel channel)
    {
        if (Uri.TryCreate(channel.ListUrl, UriKind.Absolute, out var listUri))
            return listUri;

        if (!Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var baseUri))
            throw new AtlasException("State Grid source BaseUrl must be an absolute public URL.");

        if (!Uri.TryCreate(baseUri, channel.ListUrl, out listUri))
            throw new AtlasException("State Grid channel ListUrl is invalid.");

        return listUri;
    }

    private static void EnsureCanRun(CrawlSource source, CrawlChannel channel)
    {
        EnsureSourceCanRun(source);

        if (!channel.Enabled)
            throw new AtlasException($"BidOps crawl channel is paused: {channel.Code}");
    }

    private static void EnsureSourceCanRun(CrawlSource source)
    {
        if (!source.Enabled)
            throw new AtlasException($"BidOps crawl source is paused: {source.Code}");

        if (source.NeedLogin)
            throw new AtlasException("State Grid crawler supports public pages only; login-required sources are not allowed.");

        if (!string.Equals(source.SourceType, BidOpsCrawlSourceTypes.StateGridEcp, StringComparison.OrdinalIgnoreCase))
            throw new AtlasException($"Crawl source '{source.Code}' is not a StateGridEcp source.");
    }

    private static void EnsureRateLimit(CrawlSource source, CrawlChannel channel)
    {
        var minIntervalSeconds = Math.Max(1, 60 / Math.Max(1, source.RateLimitPerMinute));
        if (channel.LastScanTime.HasValue &&
            channel.LastScanTime.Value.AddSeconds(minIntervalSeconds) > DateTime.UtcNow)
        {
            throw new AtlasException("State Grid source rate limit prevented this scan.");
        }
    }

    private static Task DelayForRateLimitAsync(CrawlSource source, CancellationToken ct)
    {
        var delayMs = Math.Clamp(60_000 / Math.Max(1, source.RateLimitPerMinute), 1_000, 10_000);
        return Task.Delay(delayMs, ct);
    }

    private static void EnsureAllowedStateGridUri(CrawlSource source, Uri uri)
    {
        if (!IsAllowedStateGridUri(source, uri))
            throw new AtlasException($"State Grid crawler rejected non-SGCC URL: {uri.Host}");
    }

    private static bool IsAllowedStateGridUri(CrawlSource source, Uri uri)
    {
        if (uri.Scheme is not ("http" or "https"))
            return false;

        if (!uri.Host.EndsWith("sgcc.com.cn", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var baseUri))
            return true;

        return string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddCommonHeaders(
        HttpRequestMessage request,
        CrawlSource source,
        string accept)
    {
        request.Headers.UserAgent.ParseAdd(NormalizeUserAgent(source.UserAgent));
        request.Headers.Accept.ParseAdd(accept);

        var referer = BuildPortalReferer(source);
        request.Headers.Referrer = referer;
        request.Headers.TryAddWithoutValidation("Origin", referer.GetLeftPart(UriPartial.Authority));
    }

    private static Uri BuildPortalReferer(CrawlSource source)
    {
        if (!Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var baseUri))
            return new Uri("https://ecp.sgcc.com.cn/ecp2.0/portal/");

        var builder = new UriBuilder(baseUri)
        {
            Path = "/ecp2.0/portal/",
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri;
    }

    private async Task AddLogAsync(
        long? sourceId,
        long? channelId,
        long? backgroundJobId,
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
            Operation = OperationName,
            Status = status,
            Message = message.Length <= 2000 ? message : message[..2000]
        }, ct);
    }

    private static string NormalizeUserAgent(string userAgent)
    {
        return string.IsNullOrWhiteSpace(userAgent)
            ? "AtlasBidOps/0.1 (+public procurement crawler; no-login)"
            : userAgent.Trim();
    }

    private static IReadOnlyList<RawAttachmentCandidate> MapAttachments(
        IReadOnlyList<StateGridAttachmentDocument> attachments)
    {
        if (attachments.Count == 0)
            return Array.Empty<RawAttachmentCandidate>();

        return attachments
            .Where(x => !string.IsNullOrWhiteSpace(x.FileUrl))
            .Select(x => new RawAttachmentCandidate(
                x.FileName,
                x.FileUrl,
                x.FileType,
                x.FileSize))
            .ToArray();
    }

    private static IReadOnlyList<StateGridAttachmentDocument> MergeAttachments(
        IReadOnlyList<StateGridAttachmentDocument> primary,
        IReadOnlyList<StateGridAttachmentDocument> secondary)
    {
        if (primary.Count == 0)
            return secondary;

        if (secondary.Count == 0)
            return primary;

        var merged = new List<StateGridAttachmentDocument>(primary.Count + secondary.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attachment in primary.Concat(secondary))
        {
            if (string.IsNullOrWhiteSpace(attachment.FileUrl) || !seen.Add(attachment.FileUrl))
                continue;

            merged.Add(attachment);
        }

        return merged;
    }
}
