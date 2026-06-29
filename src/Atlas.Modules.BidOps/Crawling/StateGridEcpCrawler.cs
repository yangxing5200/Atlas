using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Atlas.Modules.BidOps.Crawling;

public sealed class StateGridEcpCrawler : IStateGridEcpCrawler
{
    private const string OperationName = "StateGridEcpCrawl";
    private const int DefaultMaxNoticesPerScan = 10;
    private const int MaxNoticesPerScanLimit = 50;
    private const int MaxResponseCharacters = 500_000;
    private static readonly string[] DefaultProcurementSearchMenuIds =
    [
        "2018032700291334",
        "2018032900295987"
    ];

    private readonly HttpClient _httpClient;
    private readonly IRepository<CrawlSource> _sources;
    private readonly IRepository<CrawlChannel> _channels;
    private readonly IRepository<CrawlRunLog> _logs;
    private readonly IRepository<CrawlCheckpoint> _checkpoints;
    private readonly IRepository<CrawlRun> _runs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBidOpsRawIngestionService _ingestion;
    private readonly IReadOnlyList<IBidOpsCrawlAdapter> _adapters;
    private readonly IIdGenerator _idGenerator;
    private readonly ILogger<StateGridEcpCrawler> _logger;
    private readonly int _maxNoticesPerScan;

    public StateGridEcpCrawler(
        HttpClient httpClient,
        IRepository<CrawlSource> sources,
        IRepository<CrawlChannel> channels,
        IRepository<CrawlRunLog> logs,
        IRepository<CrawlCheckpoint> checkpoints,
        IRepository<CrawlRun> runs,
        IUnitOfWork unitOfWork,
        IBidOpsRawIngestionService ingestion,
        IEnumerable<IBidOpsCrawlAdapter> adapters,
        IIdGenerator idGenerator,
        IConfiguration configuration,
        ILogger<StateGridEcpCrawler> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _ingestion = ingestion ?? throw new ArgumentNullException(nameof(ingestion));
        _adapters = adapters?.ToArray() ?? throw new ArgumentNullException(nameof(adapters));
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
        return await CrawlAsync(
            new StateGridEcpCrawlRequest(channelId, BidOpsCrawlModes.Incremental),
            backgroundJobId,
            ct);
    }

    public async Task<StateGridEcpCrawlResult> CrawlAsync(
        StateGridEcpCrawlRequest request,
        long? backgroundJobId,
        CancellationToken ct = default)
    {
        var channelQuery = await _channels.QueryTrackingAsync(ct);
        var channel = await channelQuery.Where(x => x.Id == request.ChannelId).FirstOrDefaultAsync(ct);
        if (channel == null)
            throw new AtlasException($"BidOps crawl channel does not exist: {request.ChannelId}");

        var source = await GetSourceAsync(channel.SourceId, ct);
        EnsureCanRun(source, channel);
        EnsureAdapterCanRun(source);
        EnsureRateLimit(source, channel);

        var checkpoint = await ResolveCheckpointAsync(source, channel, request, ct);
        var run = await CreateRunAsync(source, channel, checkpoint, request, backgroundJobId, ct);
        channel.LastScanTime = DateTime.UtcNow;

        try
        {
            var result = StateGridEcpWcmParser.TryGetMenuId(channel.ListUrl, out var menuId)
                ? await CrawlWcmApiChannelAsync(source, channel, checkpoint, run, menuId, request, backgroundJobId, ct)
                : await CrawlHtmlChannelAsync(source, channel, checkpoint, run, backgroundJobId, ct);

            ApplyRunSuccess(run, result);
            ApplyCheckpointSuccess(checkpoint, result);
            await _unitOfWork.SaveChangesAsync(ct);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            channel.LastError = ex.Message;
            ApplyRunFailure(run, ex.Message);
            ApplyCheckpointFailure(checkpoint, ex.Message);
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
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        if (!Uri.TryCreate(detailUrl.Trim(), UriKind.Absolute, out var detailUri) ||
            !detailUri.Host.EndsWith("sgcc.com.cn", StringComparison.OrdinalIgnoreCase) ||
            !_adapters.Any(x => x.CanImportDetail(detailUri)) ||
            !StateGridEcpWcmParser.TryParsePortalDetailUrl(detailUri.ToString(), out var doctype, out var noticeId, out var menuId))
        {
            return null;
        }

        var (source, channel) = await ResolvePublicDetailTargetAsync(sourceId, channelId, detailUri, menuId, ct);
        if (channel == null)
        {
            EnsureSourceCanRun(source);
            EnsureAdapterCanRun(source);
        }
        else
        {
            EnsureCanRun(source, channel);
            EnsureAdapterCanRun(source);
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
                MapAttachments(document.Attachments),
                forceRefresh),
            backgroundJobId,
            OperationName,
            ct);

        await AddLogAsync(
            source.Id,
            channel?.Id,
            backgroundJobId,
            "Succeeded",
            $"State Grid public detail import completed. doctype={doctype}, noticeId={noticeId}, attachments={document.Attachments.Count}, forceRefresh={forceRefresh}.",
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return rawId;
    }

    public async Task<IReadOnlyList<StateGridEcpPublicNoticeCandidate>> SearchPublicNoticesAsync(
        StateGridEcpNoticeSearchRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var projectCode = NormalizeProjectCodeForSearch(request.ProjectCode);
        if (string.IsNullOrWhiteSpace(projectCode))
            return Array.Empty<StateGridEcpPublicNoticeCandidate>();

        var source = await FindStateGridSourceAsync(new Uri("https://ecp.sgcc.com.cn/ecp2.0/portal/"), ct)
            ?? throw new AtlasException("No enabled State Grid ECP crawl source is configured for this tenant.");
        EnsureSourceCanRun(source);
        EnsureAdapterCanRun(source);

        var channels = await LoadEnabledStateGridChannelsAsync(source.Id, ct);
        var menus = BuildPublicNoticeSearchMenus(request.MenuIds, channels);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 10 : request.PageSize, 1, MaxNoticesPerScanLimit);
        var candidates = new List<StateGridEcpPublicNoticeCandidate>();

        foreach (var menu in menus)
        {
            ct.ThrowIfCancellationRequested();
            var page = await FetchApiNoticeListAsync(
                source,
                menu.MenuId,
                pageIndex: 1,
                pageSize,
                ct,
                projectCode: projectCode);

            AddPublicNoticeCandidates(candidates, source, channels, menu, page);
        }

        if (candidates.Count < pageSize)
        {
            foreach (var menu in menus)
            {
                ct.ThrowIfCancellationRequested();
                var page = await FetchApiNoticeListAsync(
                    source,
                    menu.MenuId,
                    pageIndex: 1,
                    pageSize,
                    ct,
                    keyword: projectCode);

                AddPublicNoticeCandidates(candidates, source, channels, menu, page);
            }
        }

        return candidates
            .GroupBy(x => x.DetailUrl, StringComparer.OrdinalIgnoreCase)
            .Select(x => x
                .OrderByDescending(candidate => ProjectCodeEquals(candidate.ProjectCode, projectCode))
                .ThenByDescending(candidate => string.Equals(candidate.Doctype, "doci-bid", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(candidate => candidate.PublishTime ?? DateTime.MinValue)
                .First())
            .OrderByDescending(x => ProjectCodeEquals(x.ProjectCode, projectCode))
            .ThenByDescending(x => string.Equals(x.Doctype, "doci-bid", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.PublishTime ?? DateTime.MinValue)
            .Take(pageSize)
            .ToArray();
    }

    private static void AddPublicNoticeCandidates(
        List<StateGridEcpPublicNoticeCandidate> candidates,
        CrawlSource source,
        IReadOnlyList<CrawlChannel> channels,
        StateGridEcpSearchMenu menu,
        StateGridEcpNoticeListPage page)
    {
        foreach (var notice in page.Notices)
        {
            var menuId = string.IsNullOrWhiteSpace(notice.MenuId) ? menu.MenuId : notice.MenuId;
            var channel = FindChannelForMenu(channels, menuId) ??
                          (menu.ChannelId.HasValue ? channels.FirstOrDefault(x => x.Id == menu.ChannelId.Value) : null);
            var noticeType = string.IsNullOrWhiteSpace(channel?.NoticeType)
                ? ResolveNoticeType(menuId, notice.Doctype)
                : channel!.NoticeType;

            candidates.Add(new StateGridEcpPublicNoticeCandidate(
                source.Id,
                channel?.Id,
                noticeType,
                notice.Title,
                notice.DetailUrl,
                notice.Doctype,
                menuId,
                notice.NoticeId,
                notice.FirstPageDocId,
                notice.PublishTime,
                notice.PublishOrgName,
                notice.ProjectCode));
        }
    }

    private async Task<StateGridEcpCrawlResult> CrawlWcmApiChannelAsync(
        CrawlSource source,
        CrawlChannel channel,
        CrawlCheckpoint checkpoint,
        CrawlRun run,
        string menuId,
        StateGridEcpCrawlRequest request,
        long? backgroundJobId,
        CancellationToken ct)
    {
        var startPage = ResolveStartPage(request, checkpoint);
        var pageSize = ResolvePageSize(request);
        var maxPages = ResolveMaxPages(request);
        var endPage = startPage;
        var pagesScanned = 0;
        var discovered = 0;
        var created = 0;
        var changed = 0;
        var skipped = 0;
        var failed = 0;
        var totalRemoteCount = default(int?);
        var rangeCompleted = false;
        var rawIds = new List<long>();
        var publishTimes = new List<DateTime>();

        for (var pageIndex = startPage; pageIndex < startPage + maxPages; pageIndex++)
        {
            ct.ThrowIfCancellationRequested();
            var page = await FetchApiNoticeListAsync(source, menuId, pageIndex, pageSize, ct);
            totalRemoteCount ??= page.TotalCount;
            endPage = pageIndex;
            pagesScanned++;

            if (page.Notices.Count == 0)
            {
                rangeCompleted = true;
                break;
            }

            foreach (var notice in page.Notices)
            {
                ct.ThrowIfCancellationRequested();
                discovered++;
                if (notice.PublishTime.HasValue)
                    publishTimes.Add(notice.PublishTime.Value);

                if (request.RangeEndPublishTime.HasValue &&
                    notice.PublishTime.HasValue &&
                    notice.PublishTime.Value > request.RangeEndPublishTime.Value)
                {
                    skipped++;
                    continue;
                }

                if (request.RangeStartPublishTime.HasValue &&
                    notice.PublishTime.HasValue &&
                    notice.PublishTime.Value < request.RangeStartPublishTime.Value)
                {
                    rangeCompleted = true;
                    skipped++;
                    break;
                }

                var existingRawNoticeId = await _ingestion.FindExistingRawNoticeIdByUrlAsync(
                    channel.NoticeType,
                    notice.DetailUrl,
                    ct);
                if (existingRawNoticeId.HasValue)
                {
                    skipped++;
                    continue;
                }

                await DelayForRateLimitAsync(source, ct);

                try
                {
                    var document = await FetchApiDetailDocumentAsync(source, channel, notice, backgroundJobId, ct);
                    var ingest = await _ingestion.IngestPublicNoticeWithResultAsync(
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
                    if (ingest.ShouldProcess)
                        rawIds.Add(ingest.RawNoticeId);

                    if (ingest.Status == BidOpsRawIngestionStatuses.Created)
                        created++;
                    else if (ingest.Status is BidOpsRawIngestionStatuses.Changed or BidOpsRawIngestionStatuses.Refreshed)
                        changed++;
                    else
                        skipped++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    await AddLogAsync(
                        source.Id,
                        channel.Id,
                        backgroundJobId,
                        "NoticeFailed",
                        $"Failed to ingest State Grid API notice {notice.NoticeId}: {ex.Message}",
                        ct);
                }
            }

            if (rangeCompleted ||
                page.Notices.Count < pageSize ||
                (totalRemoteCount.HasValue && pageIndex * pageSize >= totalRemoteCount.Value) ||
                IsIncrementalDuplicateStop(request, page.Notices.Count, created, changed, failed))
            {
                break;
            }
        }

        var remainingEstimate = totalRemoteCount.HasValue
            ? Math.Max(0, totalRemoteCount.Value - endPage * pageSize)
            : (int?)null;
        var isCompleted = rangeCompleted || remainingEstimate == 0;
        var nextCursor = string.Equals(NormalizeMode(request.Mode), BidOpsCrawlModes.Incremental, StringComparison.OrdinalIgnoreCase)
            ? "1"
            : (isCompleted ? endPage.ToString(CultureInfo.InvariantCulture) : (endPage + 1).ToString(CultureInfo.InvariantCulture));

        if (discovered == 0)
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
            return new StateGridEcpCrawlResult(
                source.Id,
                channel.Id,
                0,
                0,
                Array.Empty<long>(),
                PageSize: pageSize,
                PageCount: pagesScanned,
                StartPage: startPage,
                EndPage: endPage,
                TotalRemoteCount: totalRemoteCount,
                RemainingEstimate: remainingEstimate,
                NextCursor: nextCursor,
                IsCompleted: isCompleted);
        }

        channel.LastSuccessTime = DateTime.UtcNow;
        channel.LastError = string.Empty;
        await AddLogAsync(
            source.Id,
            channel.Id,
            backgroundJobId,
            "Succeeded",
            $"State Grid WCM API crawl completed. menu={menuId}, mode={NormalizeMode(request.Mode)}, pages={startPage}-{endPage}, discovered={discovered}, created={created}, changed={changed}, skipped={skipped}, failed={failed}.",
            ct);
        return new StateGridEcpCrawlResult(
            source.Id,
            channel.Id,
            discovered,
            created + changed,
            rawIds,
            created,
            changed,
            skipped,
            failed,
            startPage,
            endPage,
            pageSize,
            pagesScanned,
            totalRemoteCount,
            remainingEstimate,
            nextCursor,
            publishTimes.Count == 0 ? null : publishTimes.Max(),
            publishTimes.Count == 0 ? null : publishTimes.Min(),
            isCompleted);
    }

    private async Task<StateGridEcpCrawlResult> CrawlHtmlChannelAsync(
        CrawlSource source,
        CrawlChannel channel,
        CrawlCheckpoint checkpoint,
        CrawlRun run,
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
            return new StateGridEcpCrawlResult(
                source.Id,
                channel.Id,
                0,
                0,
                Array.Empty<long>(),
                PageCount: 1,
                IsCompleted: true);
        }

        var rawIds = new List<long>();
        var created = 0;
        var changed = 0;
        var skipped = 0;
        var failed = 0;
        var publishTimes = notices
            .Where(x => x.PublishTime.HasValue)
            .Select(x => x.PublishTime!.Value)
            .ToList();
        foreach (var notice in notices)
        {
            ct.ThrowIfCancellationRequested();
            var existingRawNoticeId = await _ingestion.FindExistingRawNoticeIdByUrlAsync(
                channel.NoticeType,
                notice.DetailUrl,
                ct);
            if (existingRawNoticeId.HasValue)
            {
                skipped++;
                continue;
            }

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

                var ingest = await _ingestion.IngestPublicNoticeWithResultAsync(
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
                if (ingest.ShouldProcess)
                    rawIds.Add(ingest.RawNoticeId);

                if (ingest.Status == BidOpsRawIngestionStatuses.Created)
                    created++;
                else if (ingest.Status is BidOpsRawIngestionStatuses.Changed or BidOpsRawIngestionStatuses.Refreshed)
                    changed++;
                else
                    skipped++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
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
            $"State Grid HTML crawl completed. discovered={notices.Length}, created={created}, changed={changed}, skipped={skipped}, failed={failed}.",
            ct);

        return new StateGridEcpCrawlResult(
            source.Id,
            channel.Id,
            notices.Length,
            created + changed,
            rawIds,
            created,
            changed,
            skipped,
            failed,
            PageSize: notices.Length,
            PageCount: 1,
            HighWatermarkPublishTime: publishTimes.Count == 0 ? null : publishTimes.Max(),
            LowWatermarkPublishTime: publishTimes.Count == 0 ? null : publishTimes.Min(),
            IsCompleted: true);
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
        var channels = await LoadEnabledStateGridChannelsAsync(sourceId, ct);

        return channels
            .OrderBy(x =>
                !string.IsNullOrWhiteSpace(menuId) &&
                x.ListUrl.Contains(menuId, StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : 1)
            .ThenBy(x => x.LastScanTime.HasValue ? 1 : 0)
            .FirstOrDefault();
    }

    private async Task<IReadOnlyList<CrawlChannel>> LoadEnabledStateGridChannelsAsync(long sourceId, CancellationToken ct)
    {
        var channelQuery = await _channels.QueryTrackingAsync(ct);
        return await channelQuery
            .Where(x => x.SourceId == sourceId && x.Enabled)
            .ToListAsync(ct);
    }

    private static IReadOnlyList<StateGridEcpSearchMenu> BuildPublicNoticeSearchMenus(
        IReadOnlyCollection<string>? requestedMenuIds,
        IReadOnlyList<CrawlChannel> channels)
    {
        var menuIds = requestedMenuIds?
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (menuIds is not { Length: > 0 })
        {
            menuIds = channels
                .Select(x => StateGridEcpWcmParser.TryGetMenuId(x.ListUrl, out var menuId) ? menuId : string.Empty)
                .Where(x => DefaultProcurementSearchMenuIds.Contains(x, StringComparer.OrdinalIgnoreCase))
                .Concat(DefaultProcurementSearchMenuIds)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return menuIds
            .Select(menuId =>
            {
                var channel = FindChannelForMenu(channels, menuId);
                return new StateGridEcpSearchMenu(menuId, channel?.Id, channel?.NoticeType ?? ResolveNoticeType(menuId, "doci-bid"));
            })
            .ToArray();
    }

    private static CrawlChannel? FindChannelForMenu(IReadOnlyList<CrawlChannel> channels, string menuId)
    {
        if (string.IsNullOrWhiteSpace(menuId))
            return null;

        return channels
            .Where(x => x.ListUrl.Contains(menuId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => IsProcurementNoticeType(x.NoticeType) ? 0 : 1)
            .ThenBy(x => x.LastScanTime.HasValue ? 1 : 0)
            .FirstOrDefault();
    }

    private static string ResolveNoticeType(string menuId, string doctype)
    {
        if (string.Equals(doctype, "doci-win", StringComparison.OrdinalIgnoreCase))
            return "AwardAnnouncement";
        if (string.Equals(doctype, "doci-change", StringComparison.OrdinalIgnoreCase))
            return "ChangeAnnouncement";
        if (string.Equals(menuId, "2018032900295987", StringComparison.OrdinalIgnoreCase))
            return "ProcurementAnnouncement";
        return "TenderAnnouncement";
    }

    private static bool IsProcurementNoticeType(string noticeType)
    {
        return noticeType.Contains("Tender", StringComparison.OrdinalIgnoreCase) ||
               noticeType.Contains("Procurement", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ProjectCodeEquals(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(
                   NormalizeProjectCodeForSearch(left),
                   NormalizeProjectCodeForSearch(right),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProjectCodeForSearch(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        cleaned = Regex.Replace(
            cleaned,
            @"^(?:code|项目编号|项目编码|采购编号|招标编号|批次编号|采购项目编号|招标项目编号)\s*[:：=]\s*",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var match = Regex.Match(cleaned, @"[A-Za-z0-9][A-Za-z0-9_.\-/]*", RegexOptions.CultureInvariant);
        return match.Success
            ? match.Value.ToUpperInvariant()
            : cleaned.Trim(' ', '\t', '。', '.', '；', ';', '，', ',', '、', '）', ')').ToUpperInvariant();
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

    private async Task<StateGridEcpNoticeListPage> FetchApiNoticeListAsync(
        CrawlSource source,
        string menuId,
        int pageIndex,
        int pageSize,
        CancellationToken ct,
        string? keyword = null,
        string? projectCode = null)
    {
        var searchKey = string.IsNullOrWhiteSpace(keyword)
            ? string.Empty
            : keyword.Trim();
        var payload = BuildApiNoticeListPayload(pageIndex, pageSize, menuId, searchKey, projectCode);
        var json = await PostJsonAsync(BuildApiUri(source, "index/noteList"), source, payload, ct);
        return StateGridEcpWcmParser.ParseNoticeListPage(json, source.BaseUrl, pageSize);
    }

    private static string BuildApiNoticeListPayload(
        int pageIndex,
        int pageSize,
        string menuId,
        string? keyword,
        string? projectCode)
    {
        var searchKey = string.IsNullOrWhiteSpace(keyword)
            ? string.Empty
            : keyword.Trim();
        var purOrgCode = NormalizeProjectCodeForSearch(projectCode);
        return JsonSerializer.Serialize(new
        {
            index = pageIndex,
            size = pageSize,
            firstPageMenuId = menuId,
            purOrgStatus = string.Empty,
            purOrgCode,
            purType = string.Empty,
            noticeType = string.Empty,
            orgId = string.Empty,
            key = searchKey,
            orgName = string.Empty
        });
    }

    private async Task<CrawlCheckpoint> ResolveCheckpointAsync(
        CrawlSource source,
        CrawlChannel channel,
        StateGridEcpCrawlRequest request,
        CancellationToken ct)
    {
        var mode = NormalizeMode(request.Mode);
        var query = await _checkpoints.QueryTrackingAsync(ct);
        var checkpoint = request.CheckpointId.HasValue
            ? await query.Where(x => x.Id == request.CheckpointId.Value).FirstOrDefaultAsync(ct)
            : await query
                .Where(x => x.ChannelId == channel.Id && x.Mode == mode)
                .FirstOrDefaultAsync(ct);

        if (checkpoint == null)
        {
            checkpoint = new CrawlCheckpoint
            {
                Id = _idGenerator.NextId(),
                SourceId = source.Id,
                ChannelId = channel.Id,
                Mode = mode,
                Status = BidOpsCrawlCheckpointStatuses.Idle,
                CursorKind = BidOpsCrawlCursorKinds.PageIndex,
                NextCursor = Math.Max(1, request.StartPage ?? 1).ToString(CultureInfo.InvariantCulture),
                RangeStartPublishTime = request.RangeStartPublishTime,
                RangeEndPublishTime = request.RangeEndPublishTime
            };
            await _checkpoints.AddAsync(checkpoint, ct);
        }

        if (checkpoint.ChannelId != channel.Id || checkpoint.SourceId != source.Id)
            throw new AtlasException("BidOps crawl checkpoint does not match the requested crawl channel.");

        if (checkpoint.Status == BidOpsCrawlCheckpointStatuses.Paused)
            throw new AtlasException("BidOps crawl checkpoint is paused.");

        var now = DateTime.UtcNow;
        checkpoint.Status = BidOpsCrawlCheckpointStatuses.Running;
        checkpoint.StartedAt ??= now;
        checkpoint.LastRunAt = now;
        checkpoint.CompletedAt = null;
        checkpoint.PausedAt = null;
        checkpoint.PauseReason = string.Empty;
        checkpoint.LastError = string.Empty;
        checkpoint.RangeStartPublishTime = request.RangeStartPublishTime ?? checkpoint.RangeStartPublishTime;
        checkpoint.RangeEndPublishTime = request.RangeEndPublishTime ?? checkpoint.RangeEndPublishTime;
        return checkpoint;
    }

    private async Task<CrawlRun> CreateRunAsync(
        CrawlSource source,
        CrawlChannel channel,
        CrawlCheckpoint checkpoint,
        StateGridEcpCrawlRequest request,
        long? backgroundJobId,
        CancellationToken ct)
    {
        var run = new CrawlRun
        {
            Id = _idGenerator.NextId(),
            SourceId = source.Id,
            ChannelId = channel.Id,
            CheckpointId = checkpoint.Id,
            BackgroundJobId = backgroundJobId,
            Mode = checkpoint.Mode,
            Status = BidOpsCrawlRunStatuses.Running,
            StartCursor = ResolveStartPage(request, checkpoint).ToString(CultureInfo.InvariantCulture),
            PageSize = ResolvePageSize(request),
            StartedAt = DateTime.UtcNow
        };
        await _runs.AddAsync(run, ct);
        return run;
    }

    private static void ApplyRunSuccess(CrawlRun run, StateGridEcpCrawlResult result)
    {
        run.Status = BidOpsCrawlRunStatuses.Succeeded;
        run.EndCursor = result.EndPage.ToString(CultureInfo.InvariantCulture);
        run.PageSize = result.PageSize;
        run.PageCount = result.PageCount;
        run.DiscoveredCount = result.Discovered;
        run.CreatedCount = result.Created;
        run.ChangedCount = result.Changed;
        run.DuplicateCount = result.Skipped;
        run.FailedItemCount = result.Failed;
        run.TotalRemoteCount = result.TotalRemoteCount;
        run.RemainingEstimate = result.RemainingEstimate;
        run.CompletedAt = DateTime.UtcNow;
        run.Message = $"discovered={result.Discovered};created={result.Created};changed={result.Changed};skipped={result.Skipped};failed={result.Failed};remaining={result.RemainingEstimate?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}";
    }

    private static void ApplyRunFailure(CrawlRun run, string error)
    {
        run.Status = BidOpsCrawlRunStatuses.Failed;
        run.CompletedAt = DateTime.UtcNow;
        run.Message = Truncate(error, 2000);
    }

    private static void ApplyCheckpointSuccess(CrawlCheckpoint checkpoint, StateGridEcpCrawlResult result)
    {
        checkpoint.Status = result.IsCompleted && checkpoint.Mode == BidOpsCrawlModes.Backfill
            ? BidOpsCrawlCheckpointStatuses.Completed
            : BidOpsCrawlCheckpointStatuses.Idle;
        checkpoint.NextCursor = result.NextCursor;
        checkpoint.LastSuccessfulCursor = result.EndPage.ToString(CultureInfo.InvariantCulture);
        checkpoint.TotalRemoteCount = result.TotalRemoteCount ?? checkpoint.TotalRemoteCount;
        checkpoint.ScannedItemCount += result.Discovered;
        checkpoint.CreatedCount += result.Created;
        checkpoint.ChangedCount += result.Changed;
        checkpoint.DuplicateCount += result.Skipped;
        checkpoint.FailedItemCount += result.Failed;
        checkpoint.RemainingEstimate = checkpoint.Mode == BidOpsCrawlModes.Incremental ? 0 : result.RemainingEstimate;
        checkpoint.LastRunAt = DateTime.UtcNow;
        checkpoint.CompletedAt = checkpoint.Status == BidOpsCrawlCheckpointStatuses.Completed ? DateTime.UtcNow : null;
        checkpoint.LastError = string.Empty;

        if (result.HighWatermarkPublishTime.HasValue &&
            (!checkpoint.HighWatermarkPublishTime.HasValue ||
             result.HighWatermarkPublishTime.Value > checkpoint.HighWatermarkPublishTime.Value))
        {
            checkpoint.HighWatermarkPublishTime = result.HighWatermarkPublishTime;
        }

        if (result.LowWatermarkPublishTime.HasValue &&
            (!checkpoint.LowWatermarkPublishTime.HasValue ||
             result.LowWatermarkPublishTime.Value < checkpoint.LowWatermarkPublishTime.Value))
        {
            checkpoint.LowWatermarkPublishTime = result.LowWatermarkPublishTime;
        }

        if (checkpoint.Mode == BidOpsCrawlModes.Incremental)
            checkpoint.NextCursor = "1";
    }

    private static void ApplyCheckpointFailure(CrawlCheckpoint checkpoint, string error)
    {
        checkpoint.Status = BidOpsCrawlCheckpointStatuses.Failed;
        checkpoint.LastRunAt = DateTime.UtcNow;
        checkpoint.LastError = Truncate(error, 2000);
    }

    private int ResolvePageSize(StateGridEcpCrawlRequest request)
    {
        return Math.Clamp(request.PageSize ?? _maxNoticesPerScan, 1, MaxNoticesPerScanLimit);
    }

    private static int ResolveMaxPages(StateGridEcpCrawlRequest request)
    {
        return Math.Clamp(request.MaxPages ?? 1, 1, 20);
    }

    private static int ResolveStartPage(StateGridEcpCrawlRequest request, CrawlCheckpoint checkpoint)
    {
        if (request.StartPage.HasValue)
            return Math.Max(1, request.StartPage.Value);

        return int.TryParse(checkpoint.NextCursor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var page) && page > 0
            ? page
            : 1;
    }

    private static bool IsIncrementalDuplicateStop(
        StateGridEcpCrawlRequest request,
        int pageItemCount,
        int created,
        int changed,
        int failed)
    {
        return string.Equals(NormalizeMode(request.Mode), BidOpsCrawlModes.Incremental, StringComparison.OrdinalIgnoreCase) &&
               pageItemCount > 0 &&
               created == 0 &&
               changed == 0 &&
               failed == 0;
    }

    private static string NormalizeMode(string? value)
    {
        return string.Equals(value, BidOpsCrawlModes.Backfill, StringComparison.OrdinalIgnoreCase)
            ? BidOpsCrawlModes.Backfill
            : BidOpsCrawlModes.Incremental;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength];
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

    private void EnsureAdapterCanRun(CrawlSource source)
    {
        if (!_adapters.Any(x => x.CanHandle(source)))
            throw new AtlasException($"No BidOps crawl adapter can handle source '{source.Code}'.");
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

    private sealed record StateGridEcpSearchMenu(
        string MenuId,
        long? ChannelId,
        string NoticeType);
}
