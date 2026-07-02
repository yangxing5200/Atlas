using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Atlas.BackgroundTasks;
using Atlas.Core.Authorization;
using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsReverseLifecycleClosureService : IBidOpsReverseLifecycleClosureService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;
    private const int RelatedNoticeScanLimit = 300;
    private const int StoredTextReadLimit = 300_000;
    private const int MaxDisplayContextSortRows = 1000;
    private const string ManualProjectCodeRemarkMarker = "项目编号手动改为 ";
    private const string StateGridTenderNoticeMenuId = "2018032700291334";
    private const string StateGridProcurementNoticeMenuId = "2018032900295987";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<RawAttachment> _rawAttachments;
    private readonly IRepository<LifecyclePackageLink> _lifecycleLinks;
    private readonly IRepository<OutcomeSupplierRecord> _outcomeRecords;
    private readonly IRepository<ReviewTask> _reviewTasks;
    private readonly IRepository<PackageStaging> _packageStaging;
    private readonly IRepository<ProcurementDetailStaging> _procurementDetailStaging;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _jobs;
    private readonly ICurrentIdentity _current;
    private readonly IIdGenerator _idGenerator;
    private readonly IBidOpsFileStore _fileStore;
    private readonly IBidOpsCrawlService _crawl;
    private readonly IStateGridEcpCrawler _stateGridCrawler;
    private readonly IBidOpsRuntimeControlService _runtimeControl;
    private readonly IBidOpsLifecycleFieldEnrichmentAiService _fieldEnrichmentAi;
    private readonly IBidOpsAmountCandidateService _amountCandidates;
    private readonly BidOpsContentHasher _hasher;
    private readonly ILogger<BidOpsReverseLifecycleClosureService> _logger;

    public BidOpsReverseLifecycleClosureService(
        IRepository<RawNotice> rawNotices,
        IRepository<RawAttachment> rawAttachments,
        IRepository<LifecyclePackageLink> lifecycleLinks,
        IRepository<OutcomeSupplierRecord> outcomeRecords,
        IRepository<ReviewTask> reviewTasks,
        IRepository<PackageStaging> packageStaging,
        IRepository<ProcurementDetailStaging> procurementDetailStaging,
        IUnitOfWork unitOfWork,
        IBackgroundJobClient jobs,
        ICurrentIdentity current,
        IIdGenerator idGenerator,
        IBidOpsFileStore fileStore,
        IBidOpsCrawlService crawl,
        IStateGridEcpCrawler stateGridCrawler,
        IBidOpsRuntimeControlService runtimeControl,
        IBidOpsLifecycleFieldEnrichmentAiService fieldEnrichmentAi,
        IBidOpsAmountCandidateService amountCandidates,
        BidOpsContentHasher hasher,
        ILogger<BidOpsReverseLifecycleClosureService> logger)
    {
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _rawAttachments = rawAttachments ?? throw new ArgumentNullException(nameof(rawAttachments));
        _lifecycleLinks = lifecycleLinks ?? throw new ArgumentNullException(nameof(lifecycleLinks));
        _outcomeRecords = outcomeRecords ?? throw new ArgumentNullException(nameof(outcomeRecords));
        _reviewTasks = reviewTasks ?? throw new ArgumentNullException(nameof(reviewTasks));
        _packageStaging = packageStaging ?? throw new ArgumentNullException(nameof(packageStaging));
        _procurementDetailStaging = procurementDetailStaging ?? throw new ArgumentNullException(nameof(procurementDetailStaging));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _current = current ?? throw new ArgumentNullException(nameof(current));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _crawl = crawl ?? throw new ArgumentNullException(nameof(crawl));
        _stateGridCrawler = stateGridCrawler ?? throw new ArgumentNullException(nameof(stateGridCrawler));
        _runtimeControl = runtimeControl ?? throw new ArgumentNullException(nameof(runtimeControl));
        _fieldEnrichmentAi = fieldEnrichmentAi ?? throw new ArgumentNullException(nameof(fieldEnrichmentAi));
        _amountCandidates = amountCandidates ?? throw new ArgumentNullException(nameof(amountCandidates));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<LifecyclePackageLinkDto>> SearchLifecycleLinksAsync(
        LifecyclePackageLinkSearchQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _lifecycleLinks.QueryDataScopeAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x =>
                x.ProjectCode.Contains(keyword) ||
                x.ProjectName.Contains(keyword) ||
                x.LotNo.Contains(keyword) ||
                x.LotName.Contains(keyword) ||
                x.PackageNo.Contains(keyword) ||
                x.PackageName.Contains(keyword) ||
                x.SupplierName.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.ProjectCode))
        {
            var projectCode = query.ProjectCode.Trim();
            builder = builder.Where(x =>
                x.ProjectCode.Contains(projectCode) ||
                x.ManualRemark.Contains(projectCode));
        }

        if (!string.IsNullOrWhiteSpace(query.LotNo))
        {
            var lotNo = query.LotNo.Trim();
            builder = builder.Where(x => x.LotNo.Contains(lotNo));
        }

        if (!string.IsNullOrWhiteSpace(query.LotName))
        {
            var lotName = query.LotName.Trim();
            builder = builder.Where(x => x.LotName.Contains(lotName));
        }

        if (!string.IsNullOrWhiteSpace(query.PackageNo))
        {
            var packageNo = query.PackageNo.Trim();
            builder = builder.Where(x => x.PackageNo.Contains(packageNo));
        }

        if (!string.IsNullOrWhiteSpace(query.SupplierName))
        {
            var supplierName = query.SupplierName.Trim();
            builder = builder.Where(x => x.SupplierName.Contains(supplierName));
        }

        if (!string.IsNullOrWhiteSpace(query.LinkStatus))
        {
            var status = query.LinkStatus.Trim();
            builder = builder.Where(x => x.LinkStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(query.MatchType))
        {
            var matchType = query.MatchType.Trim();
            builder = builder.Where(x => x.MatchType == matchType);
        }

        if (query.RequiresManualReview.HasValue)
        {
            var requiresManualReview = query.RequiresManualReview.Value;
            builder = builder.Where(x => x.RequiresManualReview == requiresManualReview);
        }

        if (query.RawNoticeId.HasValue)
        {
            var rawNoticeId = query.RawNoticeId.Value;
            builder = builder.Where(x =>
                x.ProcurementRawNoticeId == rawNoticeId ||
                x.CandidateRawNoticeId == rawNoticeId ||
                x.AwardRawNoticeId == rawNoticeId);

            return await SearchLifecycleLinksForRawNoticeAsync(query, builder, pageIndex, pageSize, ct);
        }

        var total = await builder.CountAsync(ct);
        if (query.RawNoticeId.HasValue &&
            RequiresLifecycleDisplayContextSort(query.SortBy) &&
            total <= MaxDisplayContextSortRows)
        {
            var allLinks = await builder.ToListAsync(ct);
            var allItems = allLinks.Select(MapLifecycleLink).ToList();
            await EnrichLifecycleNoticeRefsAsync(allItems, ct);
            await EnrichLifecycleLinkDtosFromOutcomeContextAsync(allItems, ct);
            var pageItems = SortLifecycleLinkDtosByDisplayContext(allItems, query.SortBy)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<LifecyclePackageLinkDto>(
                total,
                pageItems,
                pageIndex,
                pageSize);
        }

        builder = ApplyLifecycleLinkSorting(builder, query.SortBy);

        var links = await builder
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        var items = links.Select(MapLifecycleLink).ToList();
        await EnrichLifecycleNoticeRefsAsync(items, ct);
        await EnrichLifecycleLinkDtosFromOutcomeContextAsync(items, ct);

        return new PagedResult<LifecyclePackageLinkDto>(
            total,
            items,
            pageIndex,
            pageSize);
    }

    private async Task<PagedResult<LifecyclePackageLinkDto>> SearchLifecycleLinksForRawNoticeAsync(
        LifecyclePackageLinkSearchQuery query,
        IQueryBuilder<LifecyclePackageLink> builder,
        int pageIndex,
        int pageSize,
        CancellationToken ct)
    {
        var links = await builder.ToListAsync(ct);
        var items = links.Select(MapLifecycleLink).ToList();
        await EnrichLifecycleNoticeRefsAsync(items, ct);
        await EnrichLifecycleLinkDtosFromOutcomeContextAsync(items, ct);

        var statusOnlyRows = await LoadStatusOnlyOutcomeRowsAsync(query, items, ct);
        if (statusOnlyRows.Count > 0)
        {
            await EnrichLifecycleNoticeRefsAsync(statusOnlyRows, ct);
            items.AddRange(statusOnlyRows);
        }

        var sorted = SortLifecycleLinkDtos(items, query.SortBy);
        var pageItems = sorted
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<LifecyclePackageLinkDto>(
            sorted.Count,
            pageItems,
            pageIndex,
            pageSize);
    }

    public async Task<IReadOnlyList<LifecycleProcurementNoticeCandidateDto>> SearchProcurementNoticeCandidatesAsync(
        long linkId,
        CancellationToken ct = default)
    {
        var link = await LoadLifecycleLinkForReadAsync(linkId, ct);
        var projectCode = await ResolveLifecycleLinkProjectCodeAsync(link, ct);
        if (string.IsNullOrWhiteSpace(projectCode))
            throw new AtlasException("Lifecycle link does not have a project/procurement code for State Grid search.");

        return await SearchProcurementNoticeCandidatesForProjectCodeAsync(link, projectCode, ct);
    }

    private async Task<IReadOnlyList<LifecycleProcurementNoticeCandidateDto>> SearchProcurementNoticeCandidatesForProjectCodeAsync(
        LifecyclePackageLink link,
        string projectCode,
        CancellationToken ct)
    {
        var classification = await ClassifyLifecycleSourceNoticeAsync(projectCode, link.AwardRawNoticeId, ct);
        var candidates = await _stateGridCrawler.SearchPublicNoticesAsync(
            new StateGridEcpNoticeSearchRequest(
                projectCode,
                PageSize: 20,
                MenuIds: ResolveStateGridSourceNoticeMenuIds(classification)),
            ct);
        var procurementCandidates = candidates
            .Where(IsProcurementNoticeCandidate)
            .ToList();
        var rawByUrlHash = await LoadRawNoticesByUrlHashAsync(procurementCandidates.Select(x => x.DetailUrl), ct);

        var mappedCandidates = procurementCandidates
            .Select(candidate => MapProcurementNoticeCandidate(candidate, projectCode, rawByUrlHash, classification))
            .ToList();
        var filteredCandidates = FilterProcurementNoticeCandidatesByProjectCode(mappedCandidates, projectCode);

        return filteredCandidates
            .OrderByDescending(x => x.IsExactProjectCodeMatch)
            .ThenBy(x => BidOpsSourceNoticeClassifier.PreferredIndex(classification, x.SourceNoticeType))
            .ThenByDescending(x => x.PublishTime ?? DateTime.MinValue)
            .ToArray();
    }

    public async Task<LifecycleProcurementNoticeImportResultDto> ImportProcurementNoticeCandidateAsync(
        long linkId,
        LifecycleProcurementNoticeImportRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var link = await LoadLifecycleLinkForUpdateAsync(linkId, ct);
        var linkProjectCode = await ResolveLifecycleLinkProjectCodeAsync(link, ct);
        if (string.IsNullOrWhiteSpace(linkProjectCode))
            throw new AtlasException("Lifecycle link does not have a project/procurement code.");
        var projectCodeChanged = ApplyResolvedProjectCode(link, linkProjectCode);
        var previousProcurementRawNoticeId = link.ProcurementRawNoticeId;

        if (!Uri.TryCreate(request.DetailUrl?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !uri.Host.EndsWith("sgcc.com.cn", StringComparison.OrdinalIgnoreCase) ||
            !StateGridEcpWcmParser.TryParsePortalDetailUrl(uri.ToString(), out var doctype, out _, out var menuId) ||
            !string.Equals(doctype, "doci-bid", StringComparison.OrdinalIgnoreCase))
        {
            throw new AtlasException("A public State Grid procurement/tender detail URL is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectCode) &&
            !ProjectCodeTextMatches(request.ProjectCode, linkProjectCode))
        {
            throw new AtlasException("Selected procurement notice project code does not match this lifecycle link.");
        }

        var existing = await FindRawNoticeByUrlAsync(uri.ToString(), ct);
        if (existing != null && !request.ForceRefresh && LooksLikeTenderNotice(existing))
        {
            var updatedLinkCount = await ApplyProcurementRawNoticeToLinkGroupAsync(
                link,
                existing.Id,
                previousProcurementRawNoticeId,
                linkProjectCode,
                request.ApplyToRelatedLinks,
                ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return new LifecycleProcurementNoticeImportResultDto
            {
                RawNoticeId = existing.Id,
                UpdatedLinkCount = updatedLinkCount,
                Message = updatedLinkCount > 1
                    ? $"前置公告已重新匹配，并同步替换 {updatedLinkCount} 条闭环记录。"
                    : "前置公告已重新匹配到当前闭环记录。"
            };
        }

        var noticeType = NormalizeProcurementNoticeType(request.NoticeType, menuId);
        var job = await _crawl.EnqueueManualUrlImportAsync(new ImportPublicUrlRequest
        {
            SourceId = request.SourceId,
            ChannelId = request.ChannelId,
            DetailUrl = uri.ToString(),
            Title = request.Title,
            NoticeType = noticeType,
            ForceRefresh = request.ForceRefresh
        }, ct);
        if (projectCodeChanged)
            await _unitOfWork.SaveChangesAsync(ct);

        return new LifecycleProcurementNoticeImportResultDto
        {
            ImportJob = job,
            Message = $"前置公告导入任务已提交：{job.JobId}。导入完成后可重新选择候选并替换错配关系。"
        };
    }

    public async Task<LifecycleProcurementAutoCollectResultDto> AutoCollectProcurementNoticesForAwardAsync(
        long awardRawNoticeId,
        LifecycleProcurementAutoCollectRequest request,
        long? backgroundJobId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = new LifecycleProcurementAutoCollectResultDto
        {
            AwardRawNoticeId = awardRawNoticeId
        };

        var query = await _lifecycleLinks.QueryDataScopeTrackingAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);
        var links = await query
            .Where(x =>
                x.AwardRawNoticeId == awardRawNoticeId &&
                x.LinkStatus != BidOpsLifecycleLinkStatuses.Confirmed &&
                x.LinkStatus != BidOpsLifecycleLinkStatuses.Rejected)
            .ToListAsync(ct);
        var targets = links
            .Where(x =>
                x.LinkStatus == BidOpsLifecycleLinkStatuses.Suggested &&
                !x.ProcurementRawNoticeId.HasValue &&
                !BidOpsOutcomeRecordPolicy.IsNonAwardLifecycleLink(x))
            .ToList();
        result.EligibleLinkCount = targets.Count;

        var targetsByProjectCode = new Dictionary<string, List<LifecyclePackageLink>>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in targets)
        {
            var projectCode = await ResolveLifecycleLinkProjectCodeAsync(link, ct);
            if (string.IsNullOrWhiteSpace(projectCode))
            {
                AddProcurementAutoCollectItem(
                    result,
                    link.Id,
                    string.Empty,
                    "Skipped",
                    "未识别到项目/采购/招标编号，不能自动补采集前置公告。",
                    0,
                    0,
                    null,
                    string.Empty);
                continue;
            }

            ApplyResolvedProjectCode(link, projectCode);
            if (!targetsByProjectCode.TryGetValue(projectCode, out var group))
            {
                group = [];
                targetsByProjectCode[projectCode] = group;
            }

            group.Add(link);
        }

        foreach (var (projectCode, groupLinks) in targetsByProjectCode)
        {
            ct.ThrowIfCancellationRequested();
            var representative = groupLinks[0];
            try
            {
                var candidates = await SearchProcurementNoticeCandidatesForProjectCodeAsync(representative, projectCode, ct);
                result.CandidateCount += candidates.Count;
                var selected = SelectAutoProcurementCandidate(candidates, projectCode, out var skipReason);
                if (selected == null)
                {
                    AddProcurementAutoCollectItem(
                        result,
                        representative.Id,
                        projectCode,
                        "Skipped",
                        skipReason,
                        candidates.Count,
                        0,
                        null,
                        string.Empty);
                    continue;
                }

                var linkedExisting = !request.ForceRefresh && selected.ExistingRawNoticeId.HasValue;
                var rawNoticeId = linkedExisting
                    ? selected.ExistingRawNoticeId
                    : await ImportProcurementNoticeCandidateForAutoCollectAsync(
                        selected,
                        projectCode,
                        request.ForceRefresh,
                        backgroundJobId,
                        ct);
                if (!rawNoticeId.HasValue)
                {
                    AddProcurementAutoCollectItem(
                        result,
                        representative.Id,
                        projectCode,
                        "Failed",
                        "候选前置公告详情导入失败，未能生成 RawNotice。",
                        candidates.Count,
                        0,
                        null,
                        selected.DetailUrl);
                    continue;
                }

                await EnqueueAttachmentProcessForAutoCollectedProcurementAsync(
                    rawNoticeId.Value,
                    projectCode,
                    ct);
                var updatedCount = ApplyProcurementRawNoticeToAutoCollectGroup(groupLinks, rawNoticeId.Value, projectCode);
                if (linkedExisting)
                    result.ExistingLinkedCount += 1;
                else
                    result.CollectedCount += 1;
                result.UpdatedLinkCount += updatedCount;
                AddProcurementAutoCollectItem(
                    result,
                    representative.Id,
                    projectCode,
                    linkedExisting ? "LinkedExisting" : "Collected",
                    linkedExisting
                        ? $"已按项目编号 {projectCode} 关联现有前置公告 RawNotice。"
                        : $"已按项目编号 {projectCode} 自动采集前置公告并关联闭环行。",
                    candidates.Count,
                    updatedCount,
                    rawNoticeId,
                    selected.DetailUrl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AddProcurementAutoCollectItem(
                    result,
                    representative.Id,
                    projectCode,
                    "Failed",
                    ex.Message,
                    0,
                    0,
                    null,
                    string.Empty);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        if (request.AutoReview)
            result.AutoReview = await AutoReviewLifecycleLinksForAwardAsync(awardRawNoticeId, ct);

        result.Message = BuildProcurementAutoCollectMessage(result);
        return result;
    }

    public async Task<LifecycleProjectCodeUpdateResultDto> UpdateLifecycleProjectCodeAsync(
        long linkId,
        LifecycleProjectCodeUpdateRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var projectCode = ResolveProjectCodeForMatch(request.ProjectCode);
        if (string.IsNullOrWhiteSpace(projectCode))
            throw new AtlasException("请输入有效的招标/采购/项目编号。");

        var link = await LoadLifecycleLinkForUpdateAsync(linkId, ct);
        var updatedCount = await ApplyProjectCodeToLinkGroupAsync(
            link,
            projectCode,
            request.Remark,
            request.ApplyToRelatedLinks,
            request.ClearProcurementNotice,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var dto = MapLifecycleLink(link);
        await EnrichLifecycleNoticeRefsAsync([dto], ct);
        await EnrichLifecycleLinkDtosFromOutcomeContextAsync([dto], ct);
        return new LifecycleProjectCodeUpdateResultDto
        {
            Link = dto,
            ProjectCode = projectCode,
            UpdatedLinkCount = updatedCount,
            Message = updatedCount > 1
                ? $"项目编号已更新为 {projectCode}，并同步 {updatedCount} 条闭环记录。"
                : $"项目编号已更新为 {projectCode}。"
        };
    }

    public async Task<EnqueueJobDto> EnqueueReverseClosureAsync(
        BidOpsReverseCloseJobRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _runtimeControl.EnsureTasksNotPausedAsync(ct);
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var rawNoticeId = request.RawNoticeId is > 0 ? request.RawNoticeId : null;
        var awardUrl = string.IsNullOrWhiteSpace(request.Url) ? null : request.Url.Trim();
        if (!rawNoticeId.HasValue &&
            (!Uri.TryCreate(awardUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
        {
            throw new AtlasException("Lifecycle reverse closure requires RawNoticeId or a public http/https award notice URL.");
        }

        var persistLinks = request.PersistLifecycleLinksOnCompletion || request.PersistLifecycleLinks;
        var runId = _idGenerator.NextId().ToString(CultureInfo.InvariantCulture);
        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<LifecycleReverseClosureJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.LifecycleReverseClosure,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = "BidOps award-driven lifecycle reverse closure",
                TenantId = tenantId,
                StoreId = _current.StoreId,
                DeduplicationKey = BidOpsBackgroundJobDeduplicationKeys.LifecycleReverseClosure(
                    tenantId,
                    rawNoticeId,
                    awardUrl,
                    persistLinks,
                    runId),
                Priority = BidOpsBackgroundJobPriorities.Manual,
                MaxAttempts = 2,
                Payload = new LifecycleReverseClosureJobPayload(
                    tenantId,
                    _current.StoreId,
                    userId,
                    _current.UserName,
                    rawNoticeId,
                    awardUrl,
                    persistLinks)
            },
            ct);

        return new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists);
    }

    public async Task<EnqueueJobDto> EnqueueLifecycleFieldEnrichmentAsync(
        long linkId,
        LifecycleFieldEnrichmentRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _runtimeControl.EnsureTasksNotPausedAsync(ct);
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<LifecycleFieldEnrichmentJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.LifecycleFieldEnrichment,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = string.IsNullOrWhiteSpace(request.ReviewerPrompt)
                    ? "BidOps lifecycle field AI enrichment"
                    : "BidOps lifecycle field AI enrichment with reviewer prompt",
                TenantId = tenantId,
                StoreId = _current.StoreId,
                DeduplicationKey = BidOpsBackgroundJobDeduplicationKeys.LifecycleFieldEnrichment(
                    tenantId,
                    linkId,
                    request.ReviewerPrompt),
                Priority = BidOpsBackgroundJobPriorities.Manual,
                MaxAttempts = 2,
                Payload = new LifecycleFieldEnrichmentJobPayload(
                    tenantId,
                    _current.StoreId,
                    userId,
                    _current.UserName,
                    linkId,
                    request.ReviewerPrompt)
            },
            ct);

        return new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists);
    }

    public async Task<EnqueueJobDto> EnqueueOutcomeSupplierReparseAsync(
        long rawNoticeId,
        LifecycleOutcomeSupplierReparseRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _runtimeControl.EnsureTasksNotPausedAsync(ct);
        var tenantId = RequireTenantId();
        var userId = RequireUserId();

        var rawQuery = await _rawNotices.QueryDataScopeAsync(
            BidOpsDataResources.RawNotice,
            AtlasDataScopeType.AllTenant,
            ct);
        var raw = await rawQuery
            .Where(x => x.Id == rawNoticeId)
            .FirstOrDefaultAsync(ct)
            ?? throw new AtlasException($"BidOps raw notice does not exist: {rawNoticeId}");

        if (!BidOpsOutcomeSupplierTextParser.LooksLikeOutcomeNotice(raw.Title, raw.NoticeType, raw.TextPreview))
            throw new AtlasException("Only award/result/candidate notices can re-extract outcome supplier records from the lifecycle closure center.");

        var reviewerPrompt = string.IsNullOrWhiteSpace(request.ReviewerPrompt)
            ? null
            : request.ReviewerPrompt.Trim();
        var reparseRunId = Guid.NewGuid().ToString("N");
        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<OutcomeSupplierExtractJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.OutcomeSupplierExtract,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = string.IsNullOrWhiteSpace(reviewerPrompt)
                    ? "BidOps lifecycle outcome supplier reparse"
                    : "BidOps lifecycle outcome supplier reparse with reviewer prompt",
                TenantId = tenantId,
                StoreId = _current.StoreId,
                DeduplicationKey = $"bidops:lifecycle-outcome-supplier-reparse:{tenantId}:{raw.Id}:{reparseRunId}",
                Priority = BidOpsBackgroundJobPriorities.Manual,
                MaxAttempts = 3,
                Payload = new OutcomeSupplierExtractJobPayload(
                    tenantId,
                    _current.StoreId,
                    userId,
                    _current.UserName,
                    raw.Id,
                    reviewerPrompt,
                    BidOpsJobProjectCode.FromRawNotice(raw),
                    RefreshLifecycleLinks: true)
            },
            ct);

        return new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists);
    }

    public async Task<BidOpsReverseClosureDebugResult> ReverseCloseUrlAsync(
        BidOpsReverseCloseUrlRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Uri.TryCreate(request.Url?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new AtlasException("A public http/https award notice URL is required.");

        var result = new BidOpsReverseClosureDebugResult
        {
            InputAwardNoticeUrl = uri.ToString()
        };

        if (request.ResetDerivedData)
        {
            result.Warnings.Add(
                "resetDerivedData is intentionally not executed by the debug API. Use tools/Atlas.LocalSetup reset-bidops-derived-data --dry-run first and --confirm only in a development database.");
        }

        var raw = await FindRawNoticeByUrlAsync(uri.ToString(), ct);
        if (raw == null)
        {
            if (request.PersistEvidence)
            {
                result.ImportJob = await _crawl.EnqueueManualUrlImportAsync(new ImportPublicUrlRequest
                {
                    DetailUrl = uri.ToString(),
                    NoticeType = "AwardAnnouncement"
                }, ct);
                result.Warnings.Add(
                    $"award raw notice not found; manual import job queued as {result.ImportJob.JobId}. Run the BidOps Worker, wait for attachment/text extraction, then call this debug endpoint again.");
            }
            else
            {
                result.Warnings.Add("award raw notice not found and persistEvidence=false, so no import job was queued.");
            }

            result.Warnings.Add("candidate notice not found");
            result.Warnings.Add("tender notice not found");
            result.Warnings.Add("award evidence not extracted because the raw award notice is not available yet.");
            return result;
        }

        var rawResult = await ReverseCloseRawNoticeCoreAsync(raw, uri.ToString(), ct);
        if (request.PersistLifecycleLinks)
            await PersistLifecycleLinksAsync(raw.TenantId, rawResult, ct);

        return rawResult;
    }

    public async Task<BidOpsReverseClosureDebugResult> ReverseCloseRawNoticeAsync(
        long rawNoticeId,
        CancellationToken ct = default)
    {
        var raw = await _rawNotices.GetByIdAsync(rawNoticeId, ct);
        if (raw == null)
            throw new AtlasException($"BidOps raw notice does not exist: {rawNoticeId}");

        return await ReverseCloseRawNoticeCoreAsync(raw, raw.DetailUrl, ct);
    }

    public async Task<BidOpsReverseClosureDebugResult> ReverseCloseRawNoticeAndPersistAsync(
        long rawNoticeId,
        CancellationToken ct = default)
    {
        var raw = await _rawNotices.GetByIdAsync(rawNoticeId, ct);
        if (raw == null)
            throw new AtlasException($"BidOps raw notice does not exist: {rawNoticeId}");

        var result = await ReverseCloseRawNoticeCoreAsync(raw, raw.DetailUrl, ct);
        await PersistLifecycleLinksAsync(raw.TenantId, result, ct);
        return result;
    }

    public async Task<LifecyclePackageLinkDto> ConfirmLifecycleLinkAsync(
        long linkId,
        LifecyclePackageLinkDecisionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var link = await LoadLifecycleLinkForUpdateAsync(linkId, ct);
        if (BidOpsOutcomeRecordPolicy.IsNonAwardLifecycleLink(link))
            throw new AtlasException("流标/废标/失败行仅用于展示，不能确认为闭环依据。");

        link.LinkStatus = BidOpsLifecycleLinkStatuses.Confirmed;
        link.RequiresManualReview = request.RequiresManualReview ?? false;
        if (request.FinalAwardAmount.HasValue)
            link.FinalAwardAmount = request.FinalAwardAmount.Value;
        if (!string.IsNullOrWhiteSpace(request.FinalAwardAmountSource))
            link.FinalAwardAmountSource = Truncate(request.FinalAwardAmountSource, 128);
        if (!link.ProcurementRawNoticeId.HasValue)
        {
            var projectCode = await ResolveLifecycleLinkProjectCodeAsync(link, ct);
            ApplyResolvedProjectCode(link, projectCode);
            var procurementRaw = await FindProcurementNoticeCandidateAsync(projectCode, link.AwardRawNoticeId, ct);
            if (procurementRaw != null)
                link.ProcurementRawNoticeId = procurementRaw.Id;
        }

        link.ManualRemark = Truncate(request.Remark, 1000);
        link.ConfirmedBy = _current.UserId;
        link.ConfirmedAt = DateTime.UtcNow;
        link.UpdatedAt = link.ConfirmedAt;
        await _unitOfWork.SaveChangesAsync(ct);
        var dto = MapLifecycleLink(link);
        await EnrichLifecycleNoticeRefsAsync([dto], ct);
        await EnrichLifecycleLinkDtosFromOutcomeContextAsync([dto], ct);
        return dto;
    }

    public async Task<LifecyclePackageLinkBatchReviewResultDto> BatchReviewLifecycleLinksAsync(
        LifecyclePackageLinkBatchReviewRequest request,
        CancellationToken ct = default)
    {
        return await ApplyLifecycleBatchReviewAsync(request, autoOnly: false, ct);
    }

    public async Task<LifecyclePackageLinkBatchReviewResultDto> AutoReviewLifecycleLinksForAwardAsync(
        long awardRawNoticeId,
        CancellationToken ct = default)
    {
        var query = await _lifecycleLinks.QueryDataScopeAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);
        var links = await query
            .Where(x =>
                x.AwardRawNoticeId == awardRawNoticeId &&
                x.LinkStatus == BidOpsLifecycleLinkStatuses.Suggested)
            .ToListAsync(ct);
        var linkIds = links.Select(x => x.Id).ToList();

        if (linkIds.Count == 0)
            return new LifecyclePackageLinkBatchReviewResultDto();

        return await ApplyLifecycleBatchReviewAsync(
            new LifecyclePackageLinkBatchReviewRequest
            {
                LinkIds = linkIds,
                Decision = "Confirm",
                Remark = "系统自动审核：已按项目编号唯一匹配前置公告，且闭环行满足自动确认条件。",
                RequiresManualReview = false
            },
            autoOnly: true,
            ct);
    }

    public async Task<LifecyclePackageLinkDto> RejectLifecycleLinkAsync(
        long linkId,
        LifecyclePackageLinkDecisionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var link = await LoadLifecycleLinkForUpdateAsync(linkId, ct);
        link.LinkStatus = BidOpsLifecycleLinkStatuses.Rejected;
        link.RequiresManualReview = false;
        link.ManualRemark = Truncate(request.Remark, 1000);
        link.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        var dto = MapLifecycleLink(link);
        await EnrichLifecycleNoticeRefsAsync([dto], ct);
        await EnrichLifecycleLinkDtosFromOutcomeContextAsync([dto], ct);
        return dto;
    }

    public static IReadOnlyList<LifecyclePackageClosure> LinkEvidenceForDebug(
        IReadOnlyList<AwardEvidence> awards,
        IReadOnlyList<CandidateEvidence> candidates,
        IReadOnlyList<TenderPackageEvidence> tenders)
    {
        return BuildClosures(awards, candidates, tenders);
    }

    public static IReadOnlyList<LifecyclePackageClosure> DeduplicateLifecycleClosuresForDebug(
        long tenantId,
        IReadOnlyList<LifecyclePackageClosure> closures)
    {
        return BuildLifecycleLinkDrafts(tenantId, closures)
            .Select(x => x.Closure)
            .ToList();
    }

    public static IReadOnlyList<AwardEvidence> BuildOutcomeAwardEvidenceForDebug(
        RawNotice raw,
        IReadOnlyList<OutcomeSupplierRecord> records,
        IReadOnlyList<PackageStaging> reviewPackages)
    {
        return BuildOutcomeAwardEvidence(raw, records, reviewPackages);
    }

    public static void EnrichLifecycleLinkFromOutcomeContextForDebug(
        LifecyclePackageLinkDto link,
        IEnumerable<OutcomeSupplierRecord> records,
        IReadOnlyList<PackageStaging> packages,
        IReadOnlyList<ProcurementDetailStaging>? procurementDetails = null)
    {
        EnrichLifecycleLinkFromOutcomeContext(link, records, packages, procurementDetails ?? []);
    }

    public async Task<LifecyclePackageLinkDto> EnrichLifecycleLinkFieldsAsync(
        long linkId,
        string? reviewerPrompt,
        CancellationToken ct = default)
    {
        var link = await LoadLifecycleLinkForUpdateAsync(linkId, ct);
        if (link.LinkStatus == BidOpsLifecycleLinkStatuses.Rejected)
            throw new AtlasException("Rejected lifecycle links cannot be AI-enriched.");

        var dtoBefore = MapLifecycleLink(link);
        await EnrichLifecycleNoticeRefsAsync([dtoBefore], ct);
        await EnrichLifecycleLinkDtosFromOutcomeContextAsync([dtoBefore], ct);
        var resolvedProjectCode = await ResolveLifecycleLinkProjectCodeAsync(link, ct);
        ApplyResolvedProjectCode(link, resolvedProjectCode);
        var evidence = await BuildLifecycleFieldEvidenceInputsAsync(link, ct);
        var result = await _fieldEnrichmentAi.EnrichAsync(
            new BidOpsLifecycleFieldEnrichmentRequest(
                link.Id,
                FirstNonEmpty(resolvedProjectCode, dtoBefore.ProjectCode, link.ProjectCode) ?? string.Empty,
                FirstNonEmpty(dtoBefore.ProjectName, link.ProjectName) ?? string.Empty,
                FirstNonEmpty(dtoBefore.LotNo, link.LotNo) ?? string.Empty,
                FirstNonEmpty(dtoBefore.LotName, link.LotName) ?? string.Empty,
                FirstNonEmpty(dtoBefore.PackageNo, link.PackageNo) ?? string.Empty,
                FirstNonEmpty(dtoBefore.PackageName, link.PackageName) ?? string.Empty,
                FirstNonEmpty(dtoBefore.SupplierName, link.SupplierName) ?? string.Empty,
                link.FinalAwardAmount,
                link.FinalAwardAmountSource,
                link.EvidenceJson,
                evidence,
                reviewerPrompt),
            ct);

        ApplyFieldEnrichmentResult(link, result, reviewerPrompt);
        await _unitOfWork.SaveChangesAsync(ct);

        var dto = MapLifecycleLink(link);
        await EnrichLifecycleNoticeRefsAsync([dto], ct);
        await EnrichLifecycleLinkDtosFromOutcomeContextAsync([dto], ct);
        return dto;
    }

    private async Task PersistLifecycleLinksAsync(
        long tenantId,
        BidOpsReverseClosureDebugResult result,
        CancellationToken ct)
    {
        if (result.Closures.Count == 0)
            return;

        var drafts = BuildLifecycleLinkDrafts(tenantId, result.Closures);
        if (drafts.Count != result.Closures.Count)
        {
            _logger.LogWarning(
                "BidOps lifecycle reverse closure collapsed {DuplicateCount} duplicate lifecycle link suggestions for tenant {TenantId} before persistence.",
                result.Closures.Count - drafts.Count,
                tenantId);
        }

        var hashes = drafts.Select(x => x.SourceHash).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var hashSet = hashes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var awardRawNoticeIds = drafts
            .Select(x => x.Closure.Award.Evidence.RawNoticeId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        var query = await _lifecycleLinks.QueryDataScopeTrackingAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);
        var existingLinks = awardRawNoticeIds.Length > 0
            ? await query
                .Where(x =>
                    hashes.Contains(x.SourceHash) ||
                    (x.AwardRawNoticeId.HasValue && awardRawNoticeIds.Contains(x.AwardRawNoticeId.Value)))
                .ToListAsync(ct)
            : await query
                .Where(x => hashes.Contains(x.SourceHash))
                .ToListAsync(ct);
        var staleLinks = existingLinks
            .Where(x =>
                x.AwardRawNoticeId.HasValue &&
                awardRawNoticeIds.Contains(x.AwardRawNoticeId.Value) &&
                !hashSet.Contains(x.SourceHash) &&
                x.LinkStatus != BidOpsLifecycleLinkStatuses.Confirmed)
            .ToList();
        if (staleLinks.Count > 0)
        {
            await _lifecycleLinks.RemoveRangeAsync(staleLinks, ct);
            existingLinks = existingLinks.Except(staleLinks).ToList();
            _logger.LogInformation(
                "BidOps lifecycle reverse closure removed {StaleCount} stale non-confirmed lifecycle links before refreshing award raw notices {AwardRawNoticeIds}.",
                staleLinks.Count,
                string.Join(",", awardRawNoticeIds));
        }

        var existingByHash = existingLinks.ToDictionary(x => x.SourceHash, StringComparer.OrdinalIgnoreCase);
        var savedLinks = new List<LifecyclePackageLink>();
        foreach (var draft in drafts)
        {
            if (!existingByHash.TryGetValue(draft.SourceHash, out var link))
            {
                var confirmedEquivalent = existingLinks.FirstOrDefault(x => IsConfirmedEquivalentLifecycleLink(x, draft.Closure));
                if (confirmedEquivalent != null)
                {
                    savedLinks.Add(confirmedEquivalent);
                    continue;
                }

                link = new LifecyclePackageLink
                {
                    Id = _idGenerator.NextId(),
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    SourceHash = draft.SourceHash
                };
                await _lifecycleLinks.AddAsync(link, ct);
                existingByHash[draft.SourceHash] = link;
            }
            else if (link.LinkStatus == BidOpsLifecycleLinkStatuses.Confirmed)
            {
                savedLinks.Add(link);
                continue;
            }

            ApplyClosureToLifecycleLink(link, draft.Closure);
            savedLinks.Add(link);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        result.PersistedLifecycleLinks = savedLinks
            .OrderByDescending(x => x.MatchScore)
            .Select(MapLifecycleLink)
            .ToList();
    }

    private static bool IsConfirmedEquivalentLifecycleLink(
        LifecyclePackageLink link,
        LifecyclePackageClosure closure)
    {
        if (link.LinkStatus != BidOpsLifecycleLinkStatuses.Confirmed)
            return false;

        if (link.AwardRawNoticeId != closure.Award.Evidence.RawNoticeId)
            return false;

        var linkPackageNo = NormalizePackageNoForMatch(link.PackageNo);
        var closurePackageNo = NormalizePackageNoForMatch(closure.NormalizedPackageNo ?? closure.PackageNo);
        if (string.IsNullOrWhiteSpace(linkPackageNo) ||
            string.IsNullOrWhiteSpace(closurePackageNo) ||
            !string.Equals(linkPackageNo, closurePackageNo, StringComparison.Ordinal))
        {
            return false;
        }

        var linkSupplier = BidOpsSupplierNameNormalizer.NormalizeForMatch(link.SupplierName);
        var closureSupplier = BidOpsSupplierNameNormalizer.NormalizeForMatch(closure.Award.AwardedSupplierName);
        if (string.IsNullOrWhiteSpace(linkSupplier) ||
            string.IsNullOrWhiteSpace(closureSupplier) ||
            !string.Equals(linkSupplier, closureSupplier, StringComparison.Ordinal))
        {
            return false;
        }

        var linkProjectCode = NormalizeProjectCodeForMatch(link.ProjectCode);
        var closureProjectCode = NormalizeProjectCodeForMatch(closure.ProjectCode);
        return string.IsNullOrWhiteSpace(linkProjectCode) ||
               string.IsNullOrWhiteSpace(closureProjectCode) ||
               string.Equals(linkProjectCode, closureProjectCode, StringComparison.Ordinal);
    }

    private static IReadOnlyList<LifecycleLinkDraft> BuildLifecycleLinkDrafts(
        long tenantId,
        IReadOnlyList<LifecyclePackageClosure> closures)
    {
        return closures
            .Select(closure => new LifecycleLinkDraft(
                closure,
                ComputeSourceHash(
                    tenantId.ToString(),
                    closure.Award.Evidence.RawNoticeId?.ToString() ?? string.Empty,
                    closure.MatchedCandidate?.Evidence.RawNoticeId?.ToString() ?? string.Empty,
                    closure.Tender?.Evidence.RawNoticeId?.ToString() ?? string.Empty,
                    closure.ProjectCode ?? string.Empty,
                    closure.LotNo ?? string.Empty,
                    closure.NormalizedPackageNo ?? closure.PackageNo ?? string.Empty,
                    closure.Award.AwardedSupplierName)))
            .GroupBy(x => x.SourceHash, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(x => x.Closure.RequiresManualReview)
                .ThenByDescending(x => x.Closure.LinkConfidence)
                .ThenByDescending(x => x.Closure.FinalAwardAmount.HasValue)
                .ThenByDescending(x => x.Closure.MatchedCandidate != null)
                .ThenByDescending(x => x.Closure.Tender != null)
                .ThenByDescending(x => x.Closure.MatchReasons.Count)
                .ThenBy(x => x.Closure.MissingFields.Count)
                .First())
            .ToList();
    }

    private static void ApplyClosureToLifecycleLink(
        LifecyclePackageLink link,
        LifecyclePackageClosure closure)
    {
        var manualProjectCode = ResolveManualProjectCodeForMatch(
            link.ProjectCode,
            link.ManualRemark,
            link.EvidenceJson);
        var now = DateTime.UtcNow;
        link.ProcurementRawNoticeId = closure.Tender?.Evidence.RawNoticeId;
        link.CandidateRawNoticeId = closure.MatchedCandidate?.Evidence.RawNoticeId ??
                                    closure.Candidates.FirstOrDefault()?.Evidence.RawNoticeId;
        link.AwardRawNoticeId = closure.Award.Evidence.RawNoticeId;
        link.ProjectCode = Truncate(closure.ProjectCode, 128);
        link.ProjectName = Truncate(closure.ProjectName, 500);
        link.LotNo = Truncate(closure.LotNo, 128);
        link.LotName = Truncate(closure.LotName, 300);
        link.PackageNo = Truncate(closure.PackageNo, 128);
        link.PackageName = Truncate(closure.PackageName, 500);
        link.SupplierName = Truncate(closure.Award.AwardedSupplierName, 300);
        link.SupplierNameNormalized = Truncate(BidOpsSupplierNameNormalizer.NormalizeForMatch(closure.Award.AwardedSupplierName), 300);
        link.FinalAwardAmount = closure.FinalAwardAmount;
        link.FinalAwardAmountSource = Truncate(closure.FinalAwardAmountSource, 128);
        link.Currency = "CNY";
        link.MatchScore = Convert.ToDecimal(closure.LinkConfidence);
        link.MatchType = ResolveLifecycleMatchType(closure);
        link.LinkStatus = BidOpsLifecycleLinkStatuses.Suggested;
        link.RequiresManualReview = closure.RequiresManualReview;
        link.MatchReasonsJson = JsonSerializer.Serialize(closure.MatchReasons, JsonOptions);
        link.MissingFieldsJson = JsonSerializer.Serialize(closure.MissingFields, JsonOptions);
        link.EvidenceJson = JsonSerializer.Serialize(new
        {
            closure.ProjectCode,
            closure.ProjectName,
            closure.LotNo,
            closure.LotName,
            closure.PackageNo,
            closure.PackageName,
            award = closure.Award,
            matchedCandidate = closure.MatchedCandidate,
            candidates = closure.Candidates,
            tender = closure.Tender,
            amount = closure.PricingDecision,
            link = new
            {
                confidence = closure.LinkConfidence,
                closure.MatchReasons,
                closure.MissingFields,
                closure.RequiresManualReview
            }
        }, JsonOptions);

        if (!string.IsNullOrWhiteSpace(manualProjectCode))
        {
            // 闭环刷新会重建 EvidenceJson；人工修正的项目编号必须在刷新后重新落回行字段和证据链。
            link.ProjectCode = Truncate(manualProjectCode, 128);
            link.EvidenceJson = ApplyManualProjectCodeEvidence(link.EvidenceJson, manualProjectCode, now);
            link.RequiresManualReview = true;
            if (!ProjectCodeTextMatches(closure.ProjectCode, manualProjectCode))
                link.ProcurementRawNoticeId = null;
        }

        link.UpdatedAt = now;
    }

    private async Task<IReadOnlyList<BidOpsLifecycleFieldEvidenceInput>> BuildLifecycleFieldEvidenceInputsAsync(
        LifecyclePackageLink link,
        CancellationToken ct)
    {
        var inferredProcurement = link.ProcurementRawNoticeId.HasValue
            ? null
            : await FindProcurementNoticeCandidateAsync(link.ProjectCode, link.AwardRawNoticeId, ct);
        var rawIds = new[]
            {
                link.AwardRawNoticeId,
                link.CandidateRawNoticeId,
                link.ProcurementRawNoticeId,
                inferredProcurement?.Id
            }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        if (rawIds.Length == 0)
            return [];

        var query = await _rawNotices.QueryDataScopeAsync(
            BidOpsDataResources.RawNotice,
            AtlasDataScopeType.AllTenant,
            ct);
        var rawNotices = await query
            .Where(x => rawIds.Contains(x.Id))
            .ToListAsync(ct);
        if (inferredProcurement != null && rawNotices.All(x => x.Id != inferredProcurement.Id))
            rawNotices.Add(inferredProcurement);

        var inputs = new List<BidOpsLifecycleFieldEvidenceInput>();
        foreach (var raw in rawNotices.OrderBy(x => ResolveLifecycleEvidenceStageOrder(link, x.Id, inferredProcurement?.Id)))
        {
            var stage = ResolveLifecycleEvidenceStage(link, raw.Id, inferredProcurement?.Id);
            var documents = await ReadEvidenceDocumentsAsync(raw, ct);
            foreach (var document in documents)
            {
                inputs.Add(new BidOpsLifecycleFieldEvidenceInput(
                    stage,
                    document.Source.RawNoticeId,
                    document.Source.RawAttachmentId,
                    document.Title,
                    document.NoticeType,
                    document.Source.SourceUrl ?? string.Empty,
                    document.Source.AttachmentName ?? string.Empty,
                    document.Text));
            }
        }

        return inputs;
    }

    private static int ResolveLifecycleEvidenceStageOrder(
        LifecyclePackageLink link,
        long rawNoticeId,
        long? inferredProcurementRawNoticeId)
    {
        return ResolveLifecycleEvidenceStage(link, rawNoticeId, inferredProcurementRawNoticeId) switch
        {
            "AwardNotice" => 0,
            "CandidateNotice" => 1,
            "ProcurementNotice" => 2,
            _ => 9
        };
    }

    private static string ResolveLifecycleEvidenceStage(
        LifecyclePackageLink link,
        long rawNoticeId,
        long? inferredProcurementRawNoticeId)
    {
        if (link.AwardRawNoticeId == rawNoticeId)
            return "AwardNotice";
        if (link.CandidateRawNoticeId == rawNoticeId)
            return "CandidateNotice";
        if (link.ProcurementRawNoticeId == rawNoticeId || inferredProcurementRawNoticeId == rawNoticeId)
            return "ProcurementNotice";

        return "RelatedNotice";
    }

    private static void ApplyFieldEnrichmentResult(
        LifecyclePackageLink link,
        BidOpsLifecycleFieldEnrichmentResult result,
        string? reviewerPrompt)
    {
        ArgumentNullException.ThrowIfNull(result);
        var acceptedFields = result.Fields
            .Where(x => x.Confidence >= 0.6m)
            .ToList();

        foreach (var field in acceptedFields)
        {
            var value = Truncate(field.Value, 1000);
            if (string.IsNullOrWhiteSpace(value) && field.NumericValue == null)
                continue;

            switch (NormalizeFieldName(field.FieldName))
            {
                case "projectcode":
                    if (string.IsNullOrWhiteSpace(link.ProjectCode))
                        link.ProjectCode = Truncate(value, 128);
                    break;
                case "projectname":
                    if (string.IsNullOrWhiteSpace(link.ProjectName))
                        link.ProjectName = Truncate(value, 500);
                    break;
                case "lotno":
                    if (string.IsNullOrWhiteSpace(link.LotNo))
                        link.LotNo = Truncate(value, 128);
                    break;
                case "lotname":
                    if (string.IsNullOrWhiteSpace(link.LotName))
                        link.LotName = Truncate(value, 300);
                    break;
                case "packageno":
                    if (string.IsNullOrWhiteSpace(link.PackageNo))
                        link.PackageNo = Truncate(value, 128);
                    break;
                case "packagename":
                    if (string.IsNullOrWhiteSpace(link.PackageName))
                        link.PackageName = Truncate(value, 500);
                    break;
                case "suppliername":
                    if (string.IsNullOrWhiteSpace(link.SupplierName))
                    {
                        link.SupplierName = Truncate(value, 300);
                        link.SupplierNameNormalized = Truncate(BidOpsSupplierNameNormalizer.NormalizeForMatch(value), 300);
                    }
                    break;
                case "finalawardamount":
                    if (!BidOpsOutcomeRecordPolicy.IsNonAwardLifecycleLink(link) &&
                        !link.FinalAwardAmount.HasValue &&
                        field.NumericValue.HasValue)
                    {
                        link.FinalAwardAmount = Math.Round(field.NumericValue.Value, 2);
                        link.FinalAwardAmountSource = Truncate(ResolveAmountSource(field), 128);
                    }
                    break;
                case "finalawardamountsource":
                    if (!BidOpsOutcomeRecordPolicy.IsNonAwardLifecycleLink(link) &&
                        string.IsNullOrWhiteSpace(link.FinalAwardAmountSource))
                    {
                        link.FinalAwardAmountSource = Truncate(value, 128);
                    }
                    break;
            }
        }

        if (BidOpsOutcomeRecordPolicy.IsNonAwardLifecycleLink(link))
        {
            link.FinalAwardAmount = null;
            link.FinalAwardAmountSource = "Missing";
        }

        link.RequiresManualReview = true;
        link.EvidenceJson = UpsertFieldEnrichmentEvidenceJson(link.EvidenceJson, result, reviewerPrompt);
        link.UpdatedAt = DateTime.UtcNow;
    }

    private static string ResolveAmountSource(BidOpsLifecycleFieldSuggestion field)
    {
        if (!string.IsNullOrWhiteSpace(field.Value) &&
            !decimal.TryParse(field.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            return field.Value;
        }

        return field.SourceStage switch
        {
            "AwardNotice" => BidOpsAmountKinds.DirectAwardAmount,
            "CandidateNotice" => BidOpsAmountKinds.CandidateFinalQuote,
            "ProcurementNotice" => "TenderEvidence",
            _ => "AiFieldEnrichment"
        };
    }

    private static string NormalizeFieldName(string fieldName)
    {
        var normalized = new string((fieldName ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .ToArray());
        return normalized.ToLowerInvariant() switch
        {
            "分标编号" or "标段编号" => "lotno",
            "分标名称" or "标段名称" => "lotname",
            "包号" or "包件号" => "packageno",
            "包名称" or "包件名称" => "packagename",
            "中标商家" or "成交商家" or "供应商" => "suppliername",
            "中标金额" or "成交金额" => "finalawardamount",
            "金额来源" => "finalawardamountsource",
            _ => normalized.ToLowerInvariant()
        };
    }

    private static string UpsertFieldEnrichmentEvidenceJson(
        string evidenceJson,
        BidOpsLifecycleFieldEnrichmentResult result,
        string? reviewerPrompt)
    {
        JsonObject root;
        if (string.IsNullOrWhiteSpace(evidenceJson))
        {
            root = [];
        }
        else
        {
            try
            {
                root = JsonNode.Parse(evidenceJson) as JsonObject ?? [];
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                root = [];
            }
        }

        root["fieldEnrichment"] = JsonSerializer.SerializeToNode(new
        {
            generatedAtUtc = DateTime.UtcNow,
            reviewerPromptProvided = !string.IsNullOrWhiteSpace(reviewerPrompt),
            reviewerPrompt = string.IsNullOrWhiteSpace(reviewerPrompt) ? string.Empty : reviewerPrompt.Trim(),
            result
        }, JsonOptions);

        return root.ToJsonString(JsonOptions);
    }

    private async Task<LifecyclePackageLink> LoadLifecycleLinkForUpdateAsync(
        long linkId,
        CancellationToken ct)
    {
        var query = await _lifecycleLinks.QueryDataScopeTrackingAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);
        return await query.Where(x => x.Id == linkId).FirstOrDefaultAsync(ct)
               ?? throw new AtlasException($"BidOps lifecycle package link does not exist: {linkId}");
    }

    private async Task<LifecyclePackageLink> LoadLifecycleLinkForReadAsync(
        long linkId,
        CancellationToken ct)
    {
        var query = await _lifecycleLinks.QueryDataScopeAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);
        return await query.Where(x => x.Id == linkId).FirstOrDefaultAsync(ct)
               ?? throw new AtlasException($"BidOps lifecycle package link does not exist: {linkId}");
    }

    private async Task<LifecyclePackageLinkBatchReviewResultDto> ApplyLifecycleBatchReviewAsync(
        LifecyclePackageLinkBatchReviewRequest request,
        bool autoOnly,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var decision = NormalizeLifecycleBatchDecision(request.Decision);
        if (decision == "Reject" && string.IsNullOrWhiteSpace(request.Remark))
            throw new AtlasException("批量驳回闭环明细必须填写原因。");

        var ids = request.LinkIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        var result = new LifecyclePackageLinkBatchReviewResultDto
        {
            RequestedCount = ids.Length
        };
        if (ids.Length == 0)
            return result;

        var query = await _lifecycleLinks.QueryDataScopeTrackingAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);
        var links = await query
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);
        var linksById = links.ToDictionary(x => x.Id);
        var updatedLinks = new List<LifecyclePackageLink>();

        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            if (!linksById.TryGetValue(id, out var link))
            {
                AddBatchReviewItem(result, id, false, false, "闭环明细不存在或无权访问。");
                continue;
            }

            if (link.LinkStatus != BidOpsLifecycleLinkStatuses.Suggested)
            {
                AddBatchReviewItem(result, id, false, true, "仅待确认闭环明细支持批量审核。");
                continue;
            }

            if (BidOpsOutcomeRecordPolicy.IsNonAwardLifecycleLink(link))
            {
                // 流标/废标行只能作为结果展示，不能进入后续闭环流程。
                AddBatchReviewItem(result, id, false, true, "流标/废标/失败行仅用于展示，不能作为闭环依据。");
                continue;
            }

            if (autoOnly && !CanAutoConfirmLifecycleLink(link, out var reason))
            {
                AddBatchReviewItem(result, id, false, true, reason);
                continue;
            }

            var decisionRequest = new LifecyclePackageLinkDecisionRequest
            {
                Remark = request.Remark,
                RequiresManualReview = request.RequiresManualReview
            };
            if (decision == "Confirm")
                await ApplyLifecycleConfirmDecisionAsync(link, decisionRequest, ct);
            else
                ApplyLifecycleRejectDecision(link, decisionRequest);

            updatedLinks.Add(link);
            AddBatchReviewItem(
                result,
                id,
                true,
                false,
                decision == "Confirm" ? "确认成功。" : "驳回成功。");
        }

        if (updatedLinks.Count == 0)
            return result;

        await _unitOfWork.SaveChangesAsync(ct);
        var dtos = updatedLinks
            .Select(MapLifecycleLink)
            .ToList();
        await EnrichLifecycleNoticeRefsAsync(dtos, ct);
        await EnrichLifecycleLinkDtosFromOutcomeContextAsync(dtos, ct);
        var dtoById = dtos.ToDictionary(x => x.Id);
        foreach (var item in result.Items.Where(x => x.Succeeded))
        {
            if (dtoById.TryGetValue(item.LinkId, out var dto))
                item.Link = dto;
        }

        return result;
    }

    private async Task ApplyLifecycleConfirmDecisionAsync(
        LifecyclePackageLink link,
        LifecyclePackageLinkDecisionRequest request,
        CancellationToken ct)
    {
        link.LinkStatus = BidOpsLifecycleLinkStatuses.Confirmed;
        link.RequiresManualReview = request.RequiresManualReview ?? false;
        if (request.FinalAwardAmount.HasValue)
            link.FinalAwardAmount = request.FinalAwardAmount.Value;
        if (!string.IsNullOrWhiteSpace(request.FinalAwardAmountSource))
            link.FinalAwardAmountSource = Truncate(request.FinalAwardAmountSource, 128);
        if (!link.ProcurementRawNoticeId.HasValue)
        {
            var projectCode = await ResolveLifecycleLinkProjectCodeAsync(link, ct);
            ApplyResolvedProjectCode(link, projectCode);
            var procurementRaw = await FindProcurementNoticeCandidateAsync(projectCode, link.AwardRawNoticeId, ct);
            if (procurementRaw != null)
                link.ProcurementRawNoticeId = procurementRaw.Id;
        }

        link.ManualRemark = Truncate(request.Remark, 1000);
        link.ConfirmedBy = _current.UserId;
        link.ConfirmedAt = DateTime.UtcNow;
        link.UpdatedAt = link.ConfirmedAt;
    }

    private static void ApplyLifecycleRejectDecision(
        LifecyclePackageLink link,
        LifecyclePackageLinkDecisionRequest request)
    {
        link.LinkStatus = BidOpsLifecycleLinkStatuses.Rejected;
        link.RequiresManualReview = false;
        link.ManualRemark = Truncate(request.Remark, 1000);
        link.UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeLifecycleBatchDecision(string? decision)
    {
        if (string.Equals(decision, "Confirm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, BidOpsLifecycleLinkStatuses.Confirmed, StringComparison.OrdinalIgnoreCase))
        {
            return "Confirm";
        }

        if (string.Equals(decision, "Reject", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, BidOpsLifecycleLinkStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            return "Reject";
        }

        throw new AtlasException("Unsupported lifecycle batch review decision.");
    }

    private static void AddBatchReviewItem(
        LifecyclePackageLinkBatchReviewResultDto result,
        long linkId,
        bool succeeded,
        bool skipped,
        string message)
    {
        if (succeeded)
            result.SucceededCount += 1;
        else if (skipped)
            result.SkippedCount += 1;
        else
            result.FailedCount += 1;

        result.Items.Add(new LifecyclePackageLinkBatchReviewItemDto
        {
            LinkId = linkId,
            Succeeded = succeeded,
            Skipped = skipped,
            Message = message
        });
    }

    private static bool CanAutoConfirmLifecycleLink(
        LifecyclePackageLink link,
        out string reason)
    {
        if (link.LinkStatus != BidOpsLifecycleLinkStatuses.Suggested)
        {
            reason = "仅待确认闭环明细支持自动审核。";
            return false;
        }

        if (BidOpsOutcomeRecordPolicy.IsNonAwardLifecycleLink(link))
        {
            reason = "流标/废标/失败行不能自动确认为闭环依据。";
            return false;
        }

        if (!link.ProcurementRawNoticeId.HasValue)
        {
            reason = "尚未关联前置公告，不能自动审核。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(link.ProjectCode) ||
            string.IsNullOrWhiteSpace(link.SupplierName))
        {
            reason = "项目编号或中标供应商缺失，不能自动审核。";
            return false;
        }

        if (link.MatchScore < 0.85m)
        {
            reason = "闭环匹配分低于自动审核阈值。";
            return false;
        }

        if (link.FinalAwardAmount.HasValue &&
            ContainsAny(link.FinalAwardAmountSource, "AgencyFee", "ServiceFee", "代理服务费", "服务费"))
        {
            reason = "金额来源疑似服务费，需人工复核。";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private async Task<int> ApplyProcurementRawNoticeToLinkGroupAsync(
        LifecyclePackageLink link,
        long procurementRawNoticeId,
        long? previousProcurementRawNoticeId,
        string projectCode,
        bool applyToRelatedLinks,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        link.ProcurementRawNoticeId = procurementRawNoticeId;
        ApplyResolvedProjectCode(link, projectCode);
        link.UpdatedAt = now;

        if (!applyToRelatedLinks || !link.AwardRawNoticeId.HasValue)
            return 1;

        var awardRawNoticeId = link.AwardRawNoticeId.Value;
        var query = await _lifecycleLinks.QueryDataScopeTrackingAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);
        var relatedLinks = await query
            .Where(x =>
                x.Id != link.Id &&
                x.AwardRawNoticeId == awardRawNoticeId &&
                x.LinkStatus != BidOpsLifecycleLinkStatuses.Confirmed &&
                x.LinkStatus != BidOpsLifecycleLinkStatuses.Rejected)
            .ToListAsync(ct);

        var updatedCount = 1;
        foreach (var relatedLink in relatedLinks)
        {
            if (!ProcurementRawNoticeIdMatches(relatedLink.ProcurementRawNoticeId, previousProcurementRawNoticeId))
                continue;

            // 前置公告错配通常会让同一结果公告下的一批待审核行指向同一个旧 RawNotice；
            // 批量替换只覆盖旧 Raw 一致的行，避免误改同页其它项目或已定稿记录。
            relatedLink.ProcurementRawNoticeId = procurementRawNoticeId;
            ApplyResolvedProjectCode(relatedLink, projectCode);
            relatedLink.UpdatedAt = now;
            updatedCount += 1;
        }

        return updatedCount;
    }

    private async Task<int> ApplyProjectCodeToLinkGroupAsync(
        LifecyclePackageLink link,
        string projectCode,
        string? remark,
        bool applyToRelatedLinks,
        bool clearProcurementNotice,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        ApplyProjectCodeToLink(link, projectCode, remark, clearProcurementNotice, now);

        if (!applyToRelatedLinks || !link.AwardRawNoticeId.HasValue)
            return 1;

        var awardRawNoticeId = link.AwardRawNoticeId.Value;
        var query = await _lifecycleLinks.QueryDataScopeTrackingAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);
        var relatedLinks = await query
            .Where(x =>
                x.Id != link.Id &&
                x.AwardRawNoticeId == awardRawNoticeId)
            .ToListAsync(ct);

        var updatedCount = 1;
        foreach (var relatedLink in relatedLinks)
        {
            // 项目编号是公告级元数据；同一结果公告下的所有明细都应随“本次闭环公告”一起更新。
            ApplyProjectCodeToLink(relatedLink, projectCode, remark, clearProcurementNotice, now);
            updatedCount += 1;
        }

        return updatedCount;
    }

    private static void ApplyProjectCodeToLink(
        LifecyclePackageLink link,
        string projectCode,
        string? remark,
        bool clearProcurementNotice,
        DateTime now)
    {
        link.ProjectCode = Truncate(projectCode, 128);
        if (clearProcurementNotice)
        {
            // 手动改编号通常是在修正错配；清空旧 RawNotice 后，页面会按新编号重新推断或搜索前置公告。
            link.ProcurementRawNoticeId = null;
        }

        link.RequiresManualReview = true;
        link.ManualRemark = AppendManualRemark(
            link.ManualRemark,
            $"{ManualProjectCodeRemarkMarker}{projectCode}",
            remark);
        link.EvidenceJson = ApplyManualProjectCodeEvidence(link.EvidenceJson, projectCode, now);
        link.UpdatedAt = now;
    }

    private static string ApplyManualProjectCodeEvidence(
        string evidenceJson,
        string projectCode,
        DateTime updatedAtUtc)
    {
        JsonObject root;
        if (string.IsNullOrWhiteSpace(evidenceJson))
        {
            root = [];
        }
        else
        {
            try
            {
                root = JsonNode.Parse(evidenceJson)?.AsObject() ?? [];
            }
            catch (JsonException)
            {
                root = [];
            }
        }

        root["manualProjectCodeOverride"] = JsonSerializer.SerializeToNode(new
        {
            projectCode,
            updatedAtUtc
        }, JsonOptions);
        return root.ToJsonString(JsonOptions);
    }

    private async Task<long?> ImportProcurementNoticeCandidateForAutoCollectAsync(
        LifecycleProcurementNoticeCandidateDto candidate,
        string projectCode,
        bool forceRefresh,
        long? backgroundJobId,
        CancellationToken ct)
    {
        if (!Uri.TryCreate(candidate.DetailUrl?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !uri.Host.EndsWith("sgcc.com.cn", StringComparison.OrdinalIgnoreCase) ||
            !StateGridEcpWcmParser.TryParsePortalDetailUrl(uri.ToString(), out var doctype, out _, out var menuId) ||
            !string.Equals(doctype, "doci-bid", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var noticeType = NormalizeProcurementNoticeType(candidate.NoticeType, menuId);
        var rawNoticeId = await _stateGridCrawler.ImportPublicDetailAsync(
            uri.ToString(),
            candidate.SourceId,
            candidate.ChannelId,
            noticeType,
            backgroundJobId,
            forceRefresh,
            ct);
        if (rawNoticeId.HasValue)
        {
            _logger.LogInformation(
                "BidOps auto collected procurement notice {RawNoticeId} for project code {ProjectCode}.",
                rawNoticeId.Value,
                projectCode);
        }

        return rawNoticeId;
    }

    private async Task EnqueueAttachmentProcessForAutoCollectedProcurementAsync(
        long rawNoticeId,
        string projectCode,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var raw = await _rawNotices.GetByIdAsync(rawNoticeId, ct);
        await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<AttachmentProcessJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.AttachmentProcess,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = "BidOps process auto-collected procurement notice attachments",
                TenantId = tenantId,
                StoreId = _current.StoreId,
                DeduplicationKey = BidOpsBackgroundJobDeduplicationKeys.AttachmentProcess(
                    tenantId,
                    rawNoticeId,
                    raw?.ContentHash),
                Priority = BidOpsBackgroundJobPriorities.Automatic,
                Payload = new AttachmentProcessJobPayload(
                    tenantId,
                    _current.StoreId,
                    userId,
                    _current.UserName ?? string.Empty,
                    rawNoticeId,
                    ProjectCode: projectCode)
            },
            ct);
    }

    private static LifecycleProcurementNoticeCandidateDto? SelectAutoProcurementCandidate(
        IReadOnlyList<LifecycleProcurementNoticeCandidateDto> candidates,
        string projectCode,
        out string reason)
    {
        if (candidates.Count == 0)
        {
            reason = "未搜索到当前项目编号对应的前置公告候选。";
            return null;
        }

        var exactCandidates = candidates
            .Where(x => x.IsExactProjectCodeMatch || ProjectCodeTextMatches(x.ProjectCode, projectCode))
            .GroupBy(x => x.DetailUrl, StringComparer.OrdinalIgnoreCase)
            .Select(x => x
                .OrderBy(AutoProcurementCandidatePriority)
                .ThenByDescending(candidate => candidate.PublishTime ?? DateTime.MinValue)
                .First())
            .OrderBy(AutoProcurementCandidatePriority)
            .ThenByDescending(x => x.PublishTime ?? DateTime.MinValue)
            .ToArray();
        if (exactCandidates.Length == 0)
        {
            reason = "候选公告没有项目编号精确命中，已跳过自动补采集。";
            return null;
        }

        var bestPriority = AutoProcurementCandidatePriority(exactCandidates[0]);
        var bestCandidates = exactCandidates
            .Where(x => AutoProcurementCandidatePriority(x) == bestPriority)
            .ToArray();
        if (bestCandidates.Length == 1)
        {
            reason = string.Empty;
            return bestCandidates[0];
        }

        // 多个同优先级精确候选时宁可交给人工选择，避免自动导入错省份或错批次前置公告。
        reason = $"项目编号 {projectCode} 搜到 {bestCandidates.Length} 条同优先级精确候选，需人工选择。";
        return null;
    }

    private static int AutoProcurementCandidatePriority(LifecycleProcurementNoticeCandidateDto candidate)
    {
        var sourceType = candidate.SourceNoticeType;
        var nonBidding = string.Equals(
            candidate.ProjectProcessType,
            BidOpsProjectProcessTypes.NonBidding,
            StringComparison.OrdinalIgnoreCase);
        return (nonBidding, sourceType) switch
        {
            (true, BidOpsSourceNoticeTypes.ProcurementNotice) => 0,
            (true, BidOpsSourceNoticeTypes.ProcurementInvitation) => 1,
            (true, BidOpsSourceNoticeTypes.TenderNotice) => 2,
            (true, BidOpsSourceNoticeTypes.BidInvitation) => 3,
            (false, BidOpsSourceNoticeTypes.TenderNotice) => 0,
            (false, BidOpsSourceNoticeTypes.BidInvitation) => 1,
            (false, BidOpsSourceNoticeTypes.ProcurementNotice) => 2,
            (false, BidOpsSourceNoticeTypes.ProcurementInvitation) => 3,
            _ => 99
        };
    }

    private static int ApplyProcurementRawNoticeToAutoCollectGroup(
        IEnumerable<LifecyclePackageLink> links,
        long rawNoticeId,
        string projectCode)
    {
        var now = DateTime.UtcNow;
        var updatedCount = 0;
        foreach (var link in links)
        {
            if (link.LinkStatus != BidOpsLifecycleLinkStatuses.Suggested ||
                link.ProcurementRawNoticeId.HasValue ||
                BidOpsOutcomeRecordPolicy.IsNonAwardLifecycleLink(link))
            {
                continue;
            }

            link.ProcurementRawNoticeId = rawNoticeId;
            ApplyResolvedProjectCode(link, projectCode);
            link.UpdatedAt = now;
            updatedCount += 1;
        }

        return updatedCount;
    }

    private static void AddProcurementAutoCollectItem(
        LifecycleProcurementAutoCollectResultDto result,
        long linkId,
        string projectCode,
        string status,
        string message,
        int candidateCount,
        int updatedLinkCount,
        long? rawNoticeId,
        string detailUrl)
    {
        if (string.Equals(status, "Skipped", StringComparison.OrdinalIgnoreCase))
            result.SkippedCount += 1;
        else if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
            result.FailedCount += 1;

        result.Items.Add(new LifecycleProcurementAutoCollectItemDto
        {
            LinkId = linkId,
            ProjectCode = projectCode,
            Status = status,
            Message = message,
            CandidateCount = candidateCount,
            UpdatedLinkCount = updatedLinkCount,
            RawNoticeId = rawNoticeId,
            DetailUrl = detailUrl
        });
    }

    private static string BuildProcurementAutoCollectMessage(
        LifecycleProcurementAutoCollectResultDto result)
    {
        var reviewed = result.AutoReview?.SucceededCount ?? 0;
        if (result.UpdatedLinkCount == 0 && reviewed == 0)
        {
            return result.EligibleLinkCount == 0
                ? "当前中标/成交公告没有需要自动补采集前置公告的闭环明细。"
                : "未能唯一确定可自动补采集的前置公告，请人工搜索并选择候选。";
        }

        return $"自动补采集完成：关联闭环 {result.UpdatedLinkCount} 条，采集 {result.CollectedCount} 条，复用现有 {result.ExistingLinkedCount} 条，自动审核 {reviewed} 条。";
    }

    private static string AppendManualRemark(
        string existing,
        string action,
        string? remark)
    {
        var entry = string.IsNullOrWhiteSpace(remark)
            ? action
            : $"{action}：{remark.Trim()}";
        return string.IsNullOrWhiteSpace(existing)
            ? Truncate(entry, 1000)
            : Truncate($"{existing.Trim()}\n{entry}", 1000);
    }

    private static bool ProcurementRawNoticeIdMatches(long? current, long? expected)
    {
        return expected.HasValue
            ? current == expected.Value
            : !current.HasValue;
    }

    private async Task<string> ResolveLifecycleLinkProjectCodeAsync(
        LifecyclePackageLink link,
        CancellationToken ct)
    {
        var manualProjectCode = ResolveManualProjectCodeForMatch(
            link.ProjectCode,
            link.ManualRemark,
            link.EvidenceJson);
        if (!string.IsNullOrWhiteSpace(manualProjectCode))
            return manualProjectCode;

        if (link.AwardRawNoticeId.HasValue)
        {
            // 成交/中标公告正文里标注的“采购项目编号/项目编号”是最权威证据，
            // 但人工修正过的编号优先级更高，避免重匹配时又回到旧解析值。
            var awardProjectCode = await ResolveAwardNoticeExplicitProjectCodeAsync(link.AwardRawNoticeId.Value, ct);
            if (!string.IsNullOrWhiteSpace(awardProjectCode))
                return awardProjectCode;
        }

        var code = ResolveProjectCodeForMatch(link.ProjectCode, link.LotNo, link.EvidenceJson);
        if (!string.IsNullOrWhiteSpace(code))
            return code;

        var candidates = new List<string?>
        {
            link.ProjectCode,
            link.LotNo,
            link.EvidenceJson
        };
        if (link.AwardRawNoticeId.HasValue)
        {
            // 成交公告经常把批次号放在附件名里，例如“23FEA1 成交结果公告.pdf”。
            // 旧数据中的 ProjectCode 可能是 SourceNoticeId 派生出的 URL，前置公告搜索必须优先使用这些业务证据。
            var rawNoticeId = link.AwardRawNoticeId.Value;
            var attachmentQuery = await _rawAttachments.QueryDataScopeAsync(
                BidOpsDataResources.RawNotice,
                AtlasDataScopeType.AllTenant,
                ct);
            var attachments = await attachmentQuery
                .Where(x => x.RawNoticeId == rawNoticeId)
                .ToListAsync(ct);
            foreach (var attachment in attachments.OrderBy(x => x.Id))
            {
                candidates.Add(attachment.FileName);
                candidates.Add(attachment.FileUrl);
                candidates.Add(attachment.TextContentStorageKey);
            }

            var outcomeQuery = await _outcomeRecords.QueryDataScopeAsync(
                BidOpsDataResources.OutcomeSupplierRecord,
                AtlasDataScopeType.AllTenant,
                ct);
            var records = await outcomeQuery
                .Where(x => x.RawNoticeId == rawNoticeId)
                .ToListAsync(ct);
            var dto = MapLifecycleLink(link);
            foreach (var record in OrderProjectCodeEvidenceRecords(dto, records))
            {
                candidates.Add(record.ProjectCode);
                candidates.Add(record.LotNo);
                candidates.Add(record.EvidenceText);
            }
        }

        return ResolveProjectCodeForMatch(candidates.ToArray());
    }

    private async Task<string> ResolveAwardNoticeExplicitProjectCodeAsync(
        long rawNoticeId,
        CancellationToken ct)
    {
        var query = await _rawNotices.QueryDataScopeAsync(
            BidOpsDataResources.RawNotice,
            AtlasDataScopeType.AllTenant,
            ct);
        var raw = await query
            .Where(x => x.Id == rawNoticeId)
            .FirstOrDefaultAsync(ct);
        if (raw == null)
            return string.Empty;

        var candidates = new List<string?>
        {
            raw.Title,
            raw.TextPreview
        };
        foreach (var document in await ReadEvidenceDocumentsAsync(raw, ct))
        {
            candidates.Add(document.Source.AttachmentName);
            candidates.Add(document.Text);
        }

        return ResolveExplicitProjectCodeForMatch(candidates.ToArray());
    }

    private static bool ApplyResolvedProjectCode(
        LifecyclePackageLink link,
        string projectCode)
    {
        if (string.IsNullOrWhiteSpace(projectCode))
            return false;

        var normalized = Truncate(projectCode, 128);
        if (string.Equals(link.ProjectCode, normalized, StringComparison.Ordinal))
            return false;

        link.ProjectCode = normalized;
        link.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    private async Task<Dictionary<string, RawNotice>> LoadRawNoticesByUrlHashAsync(
        IEnumerable<string> urls,
        CancellationToken ct)
    {
        var urlHashes = urls
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => _hasher.HashUrl(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (urlHashes.Length == 0)
            return [];

        var query = await _rawNotices.QueryDataScopeAsync(
            BidOpsDataResources.RawNotice,
            AtlasDataScopeType.AllTenant,
            ct);
        var rawNotices = await query
            .Where(x => urlHashes.Contains(x.DetailUrlHash))
            .ToListAsync(ct);
        return rawNotices
            .GroupBy(x => x.DetailUrlHash, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(raw => raw.FetchTime).First(),
                StringComparer.OrdinalIgnoreCase);
    }

    private LifecycleProcurementNoticeCandidateDto MapProcurementNoticeCandidate(
        StateGridEcpPublicNoticeCandidate candidate,
        string projectCode,
        IReadOnlyDictionary<string, RawNotice> rawByUrlHash,
        BidOpsSourceNoticeClassification classification)
    {
        rawByUrlHash.TryGetValue(_hasher.HashUrl(candidate.DetailUrl), out var existingRaw);
        var sourceNoticeType = ResolveSourceNoticeType(candidate);
        return new LifecycleProcurementNoticeCandidateDto
        {
            SourceId = candidate.SourceId,
            ChannelId = candidate.ChannelId,
            NoticeType = candidate.NoticeType,
            SourceNoticeType = sourceNoticeType,
            SourceNoticeColumn = BidOpsSourceNoticeClassifier.GetDisplayColumn(sourceNoticeType),
            ProjectProcessType = classification.ProjectProcessType,
            ProcurementMethod = classification.ProcurementMethod,
            Title = candidate.Title,
            DetailUrl = candidate.DetailUrl,
            Doctype = candidate.Doctype,
            MenuId = candidate.MenuId,
            NoticeId = candidate.NoticeId,
            FirstPageDocId = candidate.FirstPageDocId,
            PublishTime = candidate.PublishTime,
            PublishOrgName = candidate.PublishOrgName,
            ProjectCode = candidate.ProjectCode,
            ExistingRawNoticeId = existingRaw?.Id,
            ExistingRawNoticeStatus = existingRaw?.Status,
            IsExactProjectCodeMatch = ProjectCodeTextMatches(candidate.ProjectCode, projectCode)
        };
    }

    private static IReadOnlyList<LifecycleProcurementNoticeCandidateDto> FilterProcurementNoticeCandidatesByProjectCode(
        IReadOnlyList<LifecycleProcurementNoticeCandidateDto> candidates,
        string projectCode)
    {
        var exactProjectCodeCandidates = candidates
            .Where(x => ProjectCodeTextMatches(x.ProjectCode, projectCode))
            .ToArray();
        if (exactProjectCodeCandidates.Length > 0)
            return exactProjectCodeCandidates;

        return candidates
            .Where(x => ShouldKeepProcurementNoticeCandidateForProjectCode(x, projectCode))
            .ToArray();
    }

    private static bool ShouldKeepProcurementNoticeCandidateForProjectCode(
        LifecycleProcurementNoticeCandidateDto candidate,
        string projectCode)
    {
        if (ProjectCodeTextMatches(candidate.ProjectCode, projectCode))
            return true;

        // 国网接口无命中时可能返回栏目默认列表；候选自带不同 code 时不能展示为当前项目的前置公告。
        if (!string.IsNullOrWhiteSpace(candidate.ProjectCode))
            return false;

        return !string.IsNullOrWhiteSpace(candidate.Title) &&
               candidate.Title.Contains(projectCode, StringComparison.OrdinalIgnoreCase);
    }

    private static LifecyclePackageLinkDto MapLifecycleLink(LifecyclePackageLink link)
    {
        var isNonAwardOutcome = BidOpsOutcomeRecordPolicy.IsNonAwardLifecycleLink(link);
        return new LifecyclePackageLinkDto
        {
            Id = link.Id,
            ProcurementDetailId = link.ProcurementDetailId,
            ProcurementDetailStagingId = link.ProcurementDetailStagingId,
            TenderPackageId = link.TenderPackageId,
            CandidateOutcomeRecordId = link.CandidateOutcomeRecordId,
            AwardOutcomeRecordId = link.AwardOutcomeRecordId,
            ProcurementRawNoticeId = link.ProcurementRawNoticeId,
            CandidateRawNoticeId = link.CandidateRawNoticeId,
            AwardRawNoticeId = link.AwardRawNoticeId,
            ProjectCode = link.ProjectCode,
            ProjectName = link.ProjectName,
            LotNo = link.LotNo,
            LotName = link.LotName,
            PackageNo = link.PackageNo,
            PackageName = link.PackageName,
            SupplierName = link.SupplierName,
            FinalAwardAmount = isNonAwardOutcome ? null : link.FinalAwardAmount,
            FinalAwardAmountSource = isNonAwardOutcome ? "Missing" : link.FinalAwardAmountSource,
            ProcurementPackageAmount = null,
            ProcurementPackageAmountSource = string.Empty,
            Currency = link.Currency,
            MatchScore = link.MatchScore,
            MatchType = link.MatchType,
            LinkStatus = link.LinkStatus,
            RequiresManualReview = link.RequiresManualReview,
            MatchReasonsJson = link.MatchReasonsJson,
            MissingFieldsJson = link.MissingFieldsJson,
            EvidenceJson = link.EvidenceJson,
            ManualRemark = link.ManualRemark,
            ConfirmedBy = link.ConfirmedBy,
            ConfirmedAt = link.ConfirmedAt,
            CreatedAt = link.CreatedAt,
            UpdatedAt = link.UpdatedAt
        };
    }

    private async Task<IReadOnlyList<LifecyclePackageLinkDto>> LoadStatusOnlyOutcomeRowsAsync(
        LifecyclePackageLinkSearchQuery query,
        IReadOnlyCollection<LifecyclePackageLinkDto> existingLinks,
        CancellationToken ct)
    {
        if (!query.RawNoticeId.HasValue)
            return [];

        var linkStatus = query.LinkStatus?.Trim();
        if (!string.IsNullOrWhiteSpace(linkStatus) &&
            !linkStatus.Equals(BidOpsLifecycleLinkStatuses.StatusOnly, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var outcomeQuery = await _outcomeRecords.QueryDataScopeAsync(
            BidOpsDataResources.OutcomeSupplierRecord,
            AtlasDataScopeType.AllTenant,
            ct);
        var outcomeRecords = await outcomeQuery
            .Where(x => x.RawNoticeId == query.RawNoticeId.Value)
            .ToListAsync(ct);

        var linkedOutcomeIds = existingLinks
            .Select(x => x.AwardOutcomeRecordId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToHashSet();

        return outcomeRecords
            .OrderBy(x => x.ExtractionOrder)
            .ThenBy(x => x.Id)
            .Where(BidOpsOutcomeRecordPolicy.IsNonAwardOutcome)
            .Where(x => !linkedOutcomeIds.Contains(x.Id))
            .Select(MapStatusOnlyOutcomeRow)
            .Where(x => MatchesLifecycleLinkQuery(x, query))
            .ToList();
    }

    private static LifecyclePackageLinkDto MapStatusOnlyOutcomeRow(OutcomeSupplierRecord record)
    {
        var statusReason = "公开结果为流标/废标/失败状态，仅展示，不生成闭环后续流程。";
        return new LifecyclePackageLinkDto
        {
            // 展示行没有真实 LifecyclePackageLink，使用负数 ID 避免与持久化闭环链接冲突。
            Id = -record.Id,
            AwardOutcomeRecordId = record.Id,
            AwardRawNoticeId = record.RawNoticeId,
            ProjectCode = record.ProjectCode,
            ProjectName = record.ProjectName,
            LotNo = record.LotNo,
            LotName = record.LotName,
            PackageNo = record.PackageNo,
            PackageName = string.IsNullOrWhiteSpace(record.PackageName) ? record.LotName : record.PackageName,
            SupplierName = record.SupplierName,
            FinalAwardAmount = null,
            FinalAwardAmountSource = "Missing",
            Currency = string.IsNullOrWhiteSpace(record.Currency) ? "CNY" : record.Currency,
            MatchScore = 0m,
            MatchType = BidOpsLifecycleLinkMatchTypes.StatusOnly,
            LinkStatus = BidOpsLifecycleLinkStatuses.StatusOnly,
            RequiresManualReview = false,
            MatchReasonsJson = JsonSerializer.Serialize(new[] { statusReason }, JsonOptions),
            MissingFieldsJson = JsonSerializer.Serialize(Array.Empty<string>(), JsonOptions),
            EvidenceJson = JsonSerializer.Serialize(new
            {
                award = new
                {
                    rawNoticeId = record.RawNoticeId,
                    outcomeSupplierRecordId = record.Id,
                    record.OutcomeType,
                    record.LotNo,
                    record.LotName,
                    record.PackageNo,
                    record.PackageName,
                    record.SupplierName,
                    awardAmount = (decimal?)null,
                    record.EvidenceText
                },
                displayOnly = true,
                reason = statusReason
            }, JsonOptions),
            ManualRemark = statusReason,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
    }

    private static bool MatchesLifecycleLinkQuery(
        LifecyclePackageLinkDto row,
        LifecyclePackageLinkSearchQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            if (!ContainsAnyField(keyword, row.ProjectCode, row.ProjectName, row.LotNo, row.LotName, row.PackageNo, row.PackageName, row.SupplierName))
                return false;
        }

        if (!string.IsNullOrWhiteSpace(query.ProjectCode) && !ContainsField(row.ProjectCode, query.ProjectCode) && !ContainsField(row.ManualRemark, query.ProjectCode))
            return false;
        if (!string.IsNullOrWhiteSpace(query.LotNo) && !ContainsField(row.LotNo, query.LotNo))
            return false;
        if (!string.IsNullOrWhiteSpace(query.LotName) && !ContainsField(row.LotName, query.LotName))
            return false;
        if (!string.IsNullOrWhiteSpace(query.PackageNo) && !ContainsField(row.PackageNo, query.PackageNo))
            return false;
        if (!string.IsNullOrWhiteSpace(query.SupplierName) && !ContainsField(row.SupplierName, query.SupplierName))
            return false;
        if (!string.IsNullOrWhiteSpace(query.LinkStatus) && !row.LinkStatus.Equals(query.LinkStatus.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrWhiteSpace(query.MatchType) && !row.MatchType.Equals(query.MatchType.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;
        if (query.RequiresManualReview.HasValue && row.RequiresManualReview != query.RequiresManualReview.Value)
            return false;

        return true;
    }

    private static bool ContainsAnyField(string keyword, params string?[] values)
    {
        return values.Any(value => ContainsField(value, keyword));
    }

    private static bool ContainsField(string? value, string? keyword)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !string.IsNullOrWhiteSpace(keyword) &&
               value.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task EnrichLifecycleLinkDtosFromOutcomeContextAsync(
        IReadOnlyList<LifecyclePackageLinkDto> links,
        CancellationToken ct)
    {
        var outcomeRawIds = links
            .SelectMany(x => new[] { x.AwardRawNoticeId, x.CandidateRawNoticeId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        List<OutcomeSupplierRecord> outcomeRecords = [];
        if (outcomeRawIds.Length > 0)
        {
            var outcomeQuery = await _outcomeRecords.QueryDataScopeAsync(
                BidOpsDataResources.OutcomeSupplierRecord,
                AtlasDataScopeType.AllTenant,
                ct);
            outcomeRecords = await outcomeQuery
                .Where(x => outcomeRawIds.Contains(x.RawNoticeId))
                .ToListAsync(ct);
        }

        var packageRawIds = links
            .SelectMany(x => new[] { x.AwardRawNoticeId, x.ProcurementRawNoticeId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        if (packageRawIds.Length == 0 && outcomeRecords.Count == 0)
        {
            await EnrichLifecycleAmountCandidatesAsync(links, ct);
            return;
        }

        var packagesByRawNotice = await LoadReviewPackagesByRawNoticeAsync(packageRawIds, ct);
        var procurementDetailsByRawNotice = await LoadReviewProcurementDetailsByRawNoticeAsync(packageRawIds, ct);
        foreach (var link in links)
        {
            var packages = new List<PackageStaging>();
            if (link.AwardRawNoticeId.HasValue &&
                packagesByRawNotice.TryGetValue(link.AwardRawNoticeId.Value, out var awardPackages))
            {
                packages.AddRange(awardPackages);
            }

            if (link.ProcurementRawNoticeId.HasValue &&
                packagesByRawNotice.TryGetValue(link.ProcurementRawNoticeId.Value, out var procurementPackages))
            {
                packages.AddRange(procurementPackages);
            }

            var awardRecords = link.AwardRawNoticeId.HasValue
                ? outcomeRecords
                    .Where(x => x.RawNoticeId == link.AwardRawNoticeId.Value)
                    .OrderBy(x => x.ExtractionOrder)
                    .ThenBy(x => x.Id)
                    .ToList()
                : new List<OutcomeSupplierRecord>();
            var candidateRecords = link.CandidateRawNoticeId.HasValue
                ? outcomeRecords
                    .Where(x => x.RawNoticeId == link.CandidateRawNoticeId.Value)
                    .OrderBy(x => x.ExtractionOrder)
                    .ThenBy(x => x.Id)
                    .ToList()
                : new List<OutcomeSupplierRecord>();
            IReadOnlyList<ProcurementDetailStaging> procurementDetails = link.ProcurementRawNoticeId.HasValue &&
                                                                         procurementDetailsByRawNotice.TryGetValue(link.ProcurementRawNoticeId.Value, out var sourceDetails)
                ? sourceDetails
                : [];
            link.AwardOutcomeSuppliers = awardRecords.Select(MapOutcomeRecord).ToList();
            link.CandidateOutcomeSuppliers = candidateRecords.Select(MapOutcomeRecord).ToList();
            link.ProcurementDetails = procurementDetails.Select(MapProcurementDetailStaging).ToList();

            EnrichLifecycleLinkFromOutcomeContext(
                link,
                awardRecords,
                packages,
                procurementDetails);
        }

        await EnrichLifecycleAmountCandidatesAsync(links, ct);
    }

    private async Task EnrichLifecycleAmountCandidatesAsync(
        IReadOnlyCollection<LifecyclePackageLinkDto> links,
        CancellationToken ct)
    {
        var candidatesByLink = await _amountCandidates.EnsureLifecycleAmountCandidatesAsync(links, ct);
        foreach (var link in links)
        {
            if (BidOpsOutcomeRecordPolicy.IsNonAwardSupplierName(link.SupplierName))
            {
                link.AmountCandidates = [];
                continue;
            }

            link.AmountCandidates = candidatesByLink.TryGetValue(link.Id, out var candidates)
                ? candidates.ToList()
                : [];
        }
    }

    private async Task<Dictionary<long, IReadOnlyList<PackageStaging>>> LoadReviewPackagesByRawNoticeAsync(
        IReadOnlyCollection<long> rawNoticeIds,
        CancellationToken ct)
    {
        if (rawNoticeIds.Count == 0)
            return [];

        var taskQuery = await _reviewTasks.QueryDataScopeAsync(
            BidOpsDataResources.ReviewTask,
            AtlasDataScopeType.AllTenant,
            ct);
        var reviewTasks = await taskQuery
            .Where(x => x.RawNoticeId.HasValue &&
                        rawNoticeIds.Contains(x.RawNoticeId.Value) &&
                        x.BizType == "NoticeStaging")
            .ToListAsync(ct);
        if (reviewTasks.Count == 0)
            return [];

        var noticeStagingIds = reviewTasks.Select(x => x.BizId).Distinct().ToArray();
        var packageQuery = await _packageStaging.QueryDataScopeAsync(
            BidOpsDataResources.ReviewTask,
            AtlasDataScopeType.AllTenant,
            ct);
        var packages = await packageQuery
            .Where(x => noticeStagingIds.Contains(x.NoticeStagingId))
            .ToListAsync(ct);
        return reviewTasks
            .GroupBy(x => x.RawNoticeId!.Value)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var stagingIds = x.Select(task => task.BizId).ToHashSet();
                    return (IReadOnlyList<PackageStaging>)packages
                        .Where(package => stagingIds.Contains(package.NoticeStagingId))
                        .OrderBy(package => package.Id)
                        .ToList();
                });
    }

    private async Task<Dictionary<long, IReadOnlyList<ProcurementDetailStaging>>> LoadReviewProcurementDetailsByRawNoticeAsync(
        IReadOnlyCollection<long> rawNoticeIds,
        CancellationToken ct)
    {
        if (rawNoticeIds.Count == 0)
            return [];

        var detailQuery = await _procurementDetailStaging.QueryDataScopeAsync(
            BidOpsDataResources.ReviewTask,
            AtlasDataScopeType.AllTenant,
            ct);
        var details = await detailQuery
            .Where(x => rawNoticeIds.Contains(x.RawNoticeId))
            .ToListAsync(ct);

        return details
            .GroupBy(x => x.RawNoticeId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<ProcurementDetailStaging>)x
                    .OrderBy(detail => detail.TableIndex ?? int.MaxValue)
                    .ThenBy(detail => detail.RowIndex ?? int.MaxValue)
                    .ThenBy(detail => detail.Id)
                    .ToList());
    }

    private async Task EnrichLifecycleNoticeRefsAsync(
        IReadOnlyList<LifecyclePackageLinkDto> links,
        CancellationToken ct)
    {
        if (links.Count == 0)
            return;

        await EnrichProjectCodesForSourceNoticeLookupAsync(links, ct);

        var inferredProcurementByLinkId = new Dictionary<long, SourceNoticeLookupResult>();
        foreach (var link in links.Where(x => !x.ProcurementRawNoticeId.HasValue))
        {
            var inferred = await FindSourceNoticeCandidateAsync(link.ProjectCode, link.AwardRawNoticeId, ct);
            ApplySourceNoticeClassification(link, inferred.Classification);
            link.SourceNoticeSearchColumns = inferred.SearchColumns.ToList();
            if (inferred.RawNotice != null)
                inferredProcurementByLinkId[link.Id] = inferred;
        }

        var rawIds = links
            .SelectMany(x => new[] { x.ProcurementRawNoticeId, x.CandidateRawNoticeId, x.AwardRawNoticeId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Concat(inferredProcurementByLinkId.Values.Select(x => x.RawNotice!.Id))
            .Distinct()
            .ToArray();
        if (rawIds.Length == 0)
        {
            foreach (var link in links)
                link.ProcurementNoticeMissingReason = ProcurementNoticeMissingReason(link.ProjectCode);
            return;
        }

        var rawQuery = await _rawNotices.QueryDataScopeAsync(
            BidOpsDataResources.RawNotice,
            AtlasDataScopeType.AllTenant,
            ct);
        var rawNotices = await rawQuery
            .Where(x => rawIds.Contains(x.Id))
            .ToListAsync(ct);
        var rawById = rawNotices.ToDictionary(x => x.Id);
        var attachmentsByRawId = await LoadAttachmentDtosByRawNoticeAsync(rawIds, ct);

        foreach (var link in links)
        {
            RawNotice? procurementRaw = null;
            var procurementMatchSource = "Linked";
            if (link.ProcurementRawNoticeId.HasValue)
                rawById.TryGetValue(link.ProcurementRawNoticeId.Value, out procurementRaw);
            else if (inferredProcurementByLinkId.TryGetValue(link.Id, out var inferred))
            {
                procurementRaw = inferred.RawNotice;
                link.ProcurementRawNoticeId = procurementRaw!.Id;
                procurementMatchSource = "InferredByProjectCode";
            }

            if (link.AwardRawNoticeId.HasValue && rawById.TryGetValue(link.AwardRawNoticeId.Value, out var sourceAwardRaw))
            {
                ApplySourceNoticeClassification(
                    link,
                    BidOpsSourceNoticeClassifier.Classify(
                        sourceAwardRaw.Title,
                        sourceAwardRaw.TextPreview,
                        sourceAwardRaw.NoticeType,
                        link.ProjectCode));
            }

            if (procurementRaw != null)
            {
                link.ProcurementNotice = MapLifecycleNoticeRef(procurementRaw, procurementMatchSource);
                link.ProcurementAttachments = attachmentsByRawId.GetValueOrDefault(procurementRaw.Id)?.ToList() ?? [];
                link.ProcurementNoticeMissingReason = string.Empty;
                link.SourceNoticeType = ResolveSourceNoticeType(
                    procurementRaw.NoticeType,
                    procurementRaw.Title,
                    TryGetStateGridMenuId(procurementRaw.DetailUrl));
                link.SourceNoticeColumn = BidOpsSourceNoticeClassifier.GetDisplayColumn(link.SourceNoticeType);
                if (link.SourceNoticeSearchColumns.Count == 0)
                {
                    link.SourceNoticeSearchColumns = BuildSourceNoticeSearchColumns(
                        BidOpsSourceNoticeClassifier.Classify(
                            link.AwardNotice?.Title,
                            string.Empty,
                            link.AwardNotice?.NoticeType,
                            link.ProjectCode),
                        [],
                        link.SourceNoticeType).ToList();
                }
            }
            else
            {
                link.ProcurementNoticeMissingReason = ProcurementNoticeMissingReason(link.ProjectCode);
                if (link.SourceNoticeSearchColumns.Count == 0)
                {
                    link.SourceNoticeSearchColumns = BuildSourceNoticeSearchColumns(
                        BidOpsSourceNoticeClassifier.Classify(
                            link.AwardNotice?.Title,
                            string.Empty,
                            link.AwardNotice?.NoticeType,
                            link.ProjectCode),
                        [],
                        null).ToList();
                }
            }

            if (link.CandidateRawNoticeId.HasValue && rawById.TryGetValue(link.CandidateRawNoticeId.Value, out var candidateRaw))
            {
                link.CandidateNotice = MapLifecycleNoticeRef(candidateRaw, "Linked");
                link.CandidateAttachments = attachmentsByRawId.GetValueOrDefault(candidateRaw.Id)?.ToList() ?? [];
            }

            if (link.AwardRawNoticeId.HasValue && rawById.TryGetValue(link.AwardRawNoticeId.Value, out var awardRaw))
            {
                link.AwardNotice = MapLifecycleNoticeRef(awardRaw, "Linked");
                link.AwardAttachments = attachmentsByRawId.GetValueOrDefault(awardRaw.Id)?.ToList() ?? [];
            }
        }
    }

    private async Task<RawNotice?> FindProcurementNoticeCandidateAsync(
        string projectCode,
        long? awardRawNoticeId,
        CancellationToken ct)
    {
        return (await FindSourceNoticeCandidateAsync(projectCode, awardRawNoticeId, ct)).RawNotice;
    }

    private async Task<SourceNoticeLookupResult> FindSourceNoticeCandidateAsync(
        string projectCode,
        long? awardRawNoticeId,
        CancellationToken ct)
    {
        var code = NormalizeProjectCodeForMatch(projectCode);
        var classification = await ClassifyLifecycleSourceNoticeAsync(projectCode, awardRawNoticeId, ct);
        if (string.IsNullOrWhiteSpace(code))
        {
            return new SourceNoticeLookupResult(
                null,
                classification,
                BuildSourceNoticeSearchColumns(classification, [], null));
        }

        var query = await _rawNotices.QueryDataScopeAsync(
            BidOpsDataResources.RawNotice,
            AtlasDataScopeType.AllTenant,
            ct);
        var candidates = await query
            .Where(x => (!awardRawNoticeId.HasValue || x.Id != awardRawNoticeId.Value) &&
                        (x.SourceNoticeId.Contains(code) ||
                         x.Title.Contains(code) ||
                         x.TextPreview.Contains(code)))
            .ToListAsync(ct);

        var sourceCandidates = candidates
            .Where(LooksLikeTenderNotice)
            .ToList();
        var selected = sourceCandidates
            .OrderByDescending(x => ProjectCodeTextMatches(x.SourceNoticeId, code) ||
                                    ProjectCodeTextMatches(BidOpsEvidenceText.ExtractProjectCode(x.TextPreview), code))
            .ThenBy(x => BidOpsSourceNoticeClassifier.PreferredIndex(classification, ResolveSourceNoticeType(x)))
            .ThenByDescending(x => x.PublishTime ?? x.FetchTime)
            .FirstOrDefault();

        return new SourceNoticeLookupResult(
            selected,
            classification,
            BuildSourceNoticeSearchColumns(
                classification,
                sourceCandidates,
                selected == null ? null : ResolveSourceNoticeType(selected)));
    }

    private async Task<BidOpsSourceNoticeClassification> ClassifyLifecycleSourceNoticeAsync(
        string projectCode,
        long? awardRawNoticeId,
        CancellationToken ct)
    {
        RawNotice? awardRaw = null;
        if (awardRawNoticeId.HasValue)
            awardRaw = await _rawNotices.GetByIdAsync(awardRawNoticeId.Value, ct);

        return BidOpsSourceNoticeClassifier.Classify(
            awardRaw?.Title,
            awardRaw?.TextPreview,
            awardRaw?.NoticeType,
            projectCode);
    }

    private static IReadOnlyList<LifecycleSourceNoticeSearchColumnDto> BuildSourceNoticeSearchColumns(
        BidOpsSourceNoticeClassification classification,
        IReadOnlyList<RawNotice> candidates,
        string? matchedSourceNoticeType)
    {
        var counts = candidates
            .GroupBy(ResolveSourceNoticeType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        return classification.PreferredSourceNoticeTypes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(type => new LifecycleSourceNoticeSearchColumnDto
            {
                SourceNoticeType = type,
                ColumnName = BidOpsSourceNoticeClassifier.GetDisplayColumn(type),
                CandidateCount = counts.GetValueOrDefault(type),
                Matched = string.Equals(type, matchedSourceNoticeType, StringComparison.OrdinalIgnoreCase)
            })
            .ToArray();
    }

    private async Task EnrichProjectCodesForSourceNoticeLookupAsync(
        IReadOnlyList<LifecyclePackageLinkDto> links,
        CancellationToken ct)
    {
        var awardRawIds = links
            .Where(x => string.IsNullOrWhiteSpace(ResolveProjectCodeForMatch(x.ProjectCode, x.LotNo)) &&
                        x.AwardRawNoticeId.HasValue)
            .Select(x => x.AwardRawNoticeId!.Value)
            .Distinct()
            .ToArray();
        if (awardRawIds.Length == 0)
        {
            foreach (var link in links)
            {
                var manualProjectCode = ResolveManualProjectCodeForMatch(
                    link.ProjectCode,
                    link.ManualRemark,
                    link.EvidenceJson);
                if (!string.IsNullOrWhiteSpace(manualProjectCode))
                {
                    link.ProjectCode = Truncate(manualProjectCode, 128);
                    continue;
                }

                var code = ResolveProjectCodeForMatch(link.ProjectCode, link.LotNo);
                if (!string.IsNullOrWhiteSpace(code))
                    link.ProjectCode = Truncate(code, 128);
            }

            return;
        }

        var query = await _outcomeRecords.QueryDataScopeAsync(
            BidOpsDataResources.OutcomeSupplierRecord,
            AtlasDataScopeType.AllTenant,
            ct);
        var records = await query
            .Where(x => awardRawIds.Contains(x.RawNoticeId))
            .ToListAsync(ct);
        var recordsByRaw = records
            .GroupBy(x => x.RawNoticeId)
            .ToDictionary(x => x.Key, x => x.OrderBy(r => r.ExtractionOrder).ThenBy(r => r.Id).ToList());
        var attachmentQuery = await _rawAttachments.QueryDataScopeAsync(
            BidOpsDataResources.RawNotice,
            AtlasDataScopeType.AllTenant,
            ct);
        var attachments = await attachmentQuery
            .Where(x => awardRawIds.Contains(x.RawNoticeId))
            .ToListAsync(ct);
        var attachmentsByRaw = attachments
            .GroupBy(x => x.RawNoticeId)
            .ToDictionary(x => x.Key, x => x.OrderBy(a => a.Id).ToList());

        foreach (var link in links)
        {
            var manualProjectCode = ResolveManualProjectCodeForMatch(
                link.ProjectCode,
                link.ManualRemark,
                link.EvidenceJson);
            if (!string.IsNullOrWhiteSpace(manualProjectCode))
            {
                link.ProjectCode = Truncate(manualProjectCode, 128);
                continue;
            }

            var candidates = new List<string?>
            {
                link.ProjectCode,
                link.LotNo
            };
            if (link.AwardRawNoticeId.HasValue &&
                attachmentsByRaw.TryGetValue(link.AwardRawNoticeId.Value, out var rawAttachments))
            {
                foreach (var attachment in rawAttachments)
                {
                    candidates.Add(attachment.FileName);
                    candidates.Add(attachment.FileUrl);
                    candidates.Add(attachment.TextContentStorageKey);
                }
            }

            if (link.AwardRawNoticeId.HasValue &&
                recordsByRaw.TryGetValue(link.AwardRawNoticeId.Value, out var rawRecords))
            {
                foreach (var record in OrderProjectCodeEvidenceRecords(link, rawRecords))
                {
                    candidates.Add(record.ProjectCode);
                    candidates.Add(record.LotNo);
                    candidates.Add(record.EvidenceText);
                }
            }

            var code = ResolveProjectCodeForMatch(candidates.ToArray());
            if (!string.IsNullOrWhiteSpace(code))
                link.ProjectCode = Truncate(code, 128);
        }
    }

    private static IEnumerable<OutcomeSupplierRecord> OrderProjectCodeEvidenceRecords(
        LifecyclePackageLinkDto link,
        IReadOnlyList<OutcomeSupplierRecord> records)
    {
        var packageNo = NormalizePackageNoForMatch(link.PackageNo);
        var supplier = BidOpsSupplierNameNormalizer.NormalizeForMatch(link.SupplierName);
        return records
            .OrderByDescending(x => !string.IsNullOrWhiteSpace(packageNo) &&
                                    NormalizePackageNoForMatch(x.PackageNo) == packageNo)
            .ThenByDescending(x => !string.IsNullOrWhiteSpace(supplier) &&
                                   SupplierNamesCompatible(link.SupplierName, x.SupplierName))
            .ThenBy(x => x.ExtractionOrder)
            .ThenBy(x => x.Id);
    }

    private static void ApplySourceNoticeClassification(
        LifecyclePackageLinkDto link,
        BidOpsSourceNoticeClassification classification)
    {
        link.ProjectProcessType = classification.ProjectProcessType;
        link.ProcurementMethod = classification.ProcurementMethod;
        link.PreferredSourceNoticeTypes = classification.PreferredSourceNoticeTypes.ToList();
    }

    private static IReadOnlyCollection<string> ResolveStateGridSourceNoticeMenuIds(
        BidOpsSourceNoticeClassification classification)
    {
        return classification.PreferredSourceNoticeTypes
            .Select(type => type is BidOpsSourceNoticeTypes.TenderNotice or BidOpsSourceNoticeTypes.BidInvitation
                ? StateGridTenderNoticeMenuId
                : type is BidOpsSourceNoticeTypes.ProcurementNotice or BidOpsSourceNoticeTypes.ProcurementInvitation
                    ? StateGridProcurementNoticeMenuId
                    : string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<Dictionary<long, IReadOnlyList<RawAttachmentDto>>> LoadAttachmentDtosByRawNoticeAsync(
        IReadOnlyCollection<long> rawNoticeIds,
        CancellationToken ct)
    {
        if (rawNoticeIds.Count == 0)
            return [];

        var query = await _rawAttachments.QueryDataScopeAsync(
            BidOpsDataResources.RawNotice,
            AtlasDataScopeType.AllTenant,
            ct);
        var attachments = await query
            .Where(x => rawNoticeIds.Contains(x.RawNoticeId))
            .ToListAsync(ct);
        return attachments
            .OrderBy(x => x.Id)
            .GroupBy(x => x.RawNoticeId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<RawAttachmentDto>)x.Select(MapRawAttachment).ToList());
    }

    private static IQueryBuilder<LifecyclePackageLink> ApplyLifecycleLinkSorting(
        IQueryBuilder<LifecyclePackageLink> query,
        string? sortBy)
    {
        return NormalizeLifecycleSortBy(sortBy) switch
        {
            "CreatedAsc" => query.OrderBy(x => x.CreatedAt),
            "CreatedDesc" => query.OrderByDescending(x => x.CreatedAt),
            "ProjectCodeAsc" => query.OrderBy(x => x.ProjectCode),
            "ProjectCodeDesc" => query.OrderByDescending(x => x.ProjectCode),
            "LotNoAsc" => query.OrderBy(x => x.LotNo),
            "LotNoDesc" => query.OrderByDescending(x => x.LotNo),
            "LotNameAsc" => query.OrderBy(x => x.LotName),
            "LotNameDesc" => query.OrderByDescending(x => x.LotName),
            "PackageNoAsc" => query.OrderBy(x => x.PackageNo),
            "PackageNoDesc" => query.OrderByDescending(x => x.PackageNo),
            "SupplierNameAsc" => query.OrderBy(x => x.SupplierName),
            "SupplierNameDesc" => query.OrderByDescending(x => x.SupplierName),
            "LinkStatusAsc" => query.OrderBy(x => x.LinkStatus),
            "LinkStatusDesc" => query.OrderByDescending(x => x.LinkStatus),
            "ReviewRequiredAsc" => query.OrderBy(x => x.RequiresManualReview),
            "ReviewRequiredDesc" => query.OrderByDescending(x => x.RequiresManualReview),
            "ScoreAsc" => query.OrderBy(x => x.MatchScore),
            "ScoreDesc" => query.OrderByDescending(x => x.MatchScore),
            "AmountAsc" => query.OrderBy(x => x.FinalAwardAmount),
            "AmountDesc" => query.OrderByDescending(x => x.FinalAwardAmount),
            "ConfirmedAtAsc" => query.OrderBy(x => x.ConfirmedAt),
            "ConfirmedAtDesc" => query.OrderByDescending(x => x.ConfirmedAt),
            "UpdatedAsc" => query.OrderBy(x => x.UpdatedAt ?? x.CreatedAt),
            "UpdatedDesc" => query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt),
            _ => query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
        };
    }

    private static bool RequiresLifecycleDisplayContextSort(string? sortBy)
    {
        return NormalizeLifecycleSortBy(sortBy) is "LotNoAsc" or "LotNoDesc" or "LotNameAsc" or "LotNameDesc";
    }

    private static IReadOnlyList<LifecyclePackageLinkDto> SortLifecycleLinkDtos(
        IEnumerable<LifecyclePackageLinkDto> links,
        string? sortBy)
    {
        if (RequiresLifecycleDisplayContextSort(sortBy))
            return SortLifecycleLinkDtosByDisplayContext(links, sortBy);

        IOrderedEnumerable<LifecyclePackageLinkDto> ordered = NormalizeLifecycleSortBy(sortBy) switch
        {
            "CreatedAsc" => links.OrderBy(x => x.CreatedAt),
            "CreatedDesc" => links.OrderByDescending(x => x.CreatedAt),
            "ProjectCodeAsc" => links.OrderBy(x => NormalizeSortText(x.ProjectCode), StringComparer.OrdinalIgnoreCase),
            "ProjectCodeDesc" => links.OrderByDescending(x => NormalizeSortText(x.ProjectCode), StringComparer.OrdinalIgnoreCase),
            "PackageNoAsc" => links.OrderBy(x => NormalizeSortText(x.PackageNo), StringComparer.OrdinalIgnoreCase),
            "PackageNoDesc" => links.OrderByDescending(x => NormalizeSortText(x.PackageNo), StringComparer.OrdinalIgnoreCase),
            "SupplierNameAsc" => links.OrderBy(x => NormalizeSortText(x.SupplierName), StringComparer.OrdinalIgnoreCase),
            "SupplierNameDesc" => links.OrderByDescending(x => NormalizeSortText(x.SupplierName), StringComparer.OrdinalIgnoreCase),
            "LinkStatusAsc" => links.OrderBy(x => NormalizeSortText(x.LinkStatus), StringComparer.OrdinalIgnoreCase),
            "LinkStatusDesc" => links.OrderByDescending(x => NormalizeSortText(x.LinkStatus), StringComparer.OrdinalIgnoreCase),
            "ReviewRequiredAsc" => links.OrderBy(x => x.RequiresManualReview),
            "ReviewRequiredDesc" => links.OrderByDescending(x => x.RequiresManualReview),
            "ScoreAsc" => links.OrderBy(x => x.MatchScore),
            "ScoreDesc" => links.OrderByDescending(x => x.MatchScore),
            "AmountAsc" => links.OrderBy(x => x.FinalAwardAmount),
            "AmountDesc" => links.OrderByDescending(x => x.FinalAwardAmount),
            "ConfirmedAtAsc" => links.OrderBy(x => x.ConfirmedAt),
            "ConfirmedAtDesc" => links.OrderByDescending(x => x.ConfirmedAt),
            "UpdatedAsc" => links.OrderBy(x => x.UpdatedAt ?? x.CreatedAt),
            _ => links.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
        };

        return ordered
            .ThenBy(x => NormalizeSortText(x.LotName), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => NormalizeSortText(x.PackageNo), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => NormalizeSortText(x.SupplierName), StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(x => x.Id)
            .ToList();
    }

    private static IReadOnlyList<LifecyclePackageLinkDto> SortLifecycleLinkDtosByDisplayContext(
        IEnumerable<LifecyclePackageLinkDto> links,
        string? sortBy)
    {
        var normalized = NormalizeLifecycleSortBy(sortBy);
        return normalized switch
        {
            "LotNoDesc" => OrderLifecycleLinkDtosByText(links, x => x.LotNo, descending: true),
            "LotNameAsc" => OrderLifecycleLinkDtosByText(links, x => x.LotName, descending: false),
            "LotNameDesc" => OrderLifecycleLinkDtosByText(links, x => x.LotName, descending: true),
            _ => OrderLifecycleLinkDtosByText(links, x => x.LotNo, descending: false)
        };
    }

    private static IReadOnlyList<LifecyclePackageLinkDto> OrderLifecycleLinkDtosByText(
        IEnumerable<LifecyclePackageLinkDto> links,
        Func<LifecyclePackageLinkDto, string?> keySelector,
        bool descending)
    {
        var ordered = descending
            ? links.OrderByDescending(x => NormalizeSortText(keySelector(x)), StringComparer.OrdinalIgnoreCase)
            : links.OrderBy(x => NormalizeSortText(keySelector(x)), StringComparer.OrdinalIgnoreCase);

        return ordered
            .ThenBy(x => NormalizeSortText(x.PackageNo), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => NormalizeSortText(x.SupplierName), StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToList();
    }

    private static string NormalizeLifecycleSortBy(string? sortBy)
    {
        return (sortBy ?? string.Empty).Trim();
    }

    private static string NormalizeSortText(string? value)
    {
        return BidOpsTextQuality.CleanExtractedValue(value) ?? string.Empty;
    }

    private static (int PageIndex, int PageSize) NormalizePaging(BidOpsPagedQuery query)
    {
        var pageIndex = query.PageIndex <= 0 ? 1 : query.PageIndex;
        var pageSize = query.PageSize <= 0 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);
        return (pageIndex, pageSize);
    }

    private static string ResolveLifecycleMatchType(LifecyclePackageClosure closure)
    {
        if (!closure.RequiresManualReview && closure.LinkConfidence >= 0.9d)
            return BidOpsLifecycleLinkMatchTypes.Strong;

        return closure.LinkConfidence >= 0.6d
            ? BidOpsLifecycleLinkMatchTypes.Suggested
            : BidOpsLifecycleLinkMatchTypes.Weak;
    }

    private static string ComputeSourceHash(params string[] values)
    {
        var raw = string.Join('\u001f', values.Select(value => value?.Trim() ?? string.Empty));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record LifecycleLinkDraft(LifecyclePackageClosure Closure, string SourceHash);

    private sealed record SourceNoticeLookupResult(
        RawNotice? RawNotice,
        BidOpsSourceNoticeClassification Classification,
        IReadOnlyList<LifecycleSourceNoticeSearchColumnDto> SearchColumns);

    private static void AddFailure(
        BidOpsReverseClosureDebugResult result,
        string code,
        string stage,
        string message,
        long? rawNoticeId = null,
        long? rawAttachmentId = null,
        string? recommendedAction = null)
    {
        if (result.Failures.Any(x =>
                string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Stage, stage, StringComparison.OrdinalIgnoreCase) &&
                x.RawNoticeId == rawNoticeId &&
                string.Equals(x.Message, message, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        result.Failures.Add(new BidOpsReverseClosureFailure(
            code,
            stage,
            message,
            rawNoticeId,
            rawAttachmentId,
            recommendedAction));
    }

    private static string FailureCodeForMissingField(string missing)
    {
        if (missing.Contains("candidate", StringComparison.OrdinalIgnoreCase))
            return "CandidateNoticeNotFound";
        if (missing.Contains("tender", StringComparison.OrdinalIgnoreCase))
            return "TenderNoticeNotFound";
        if (missing.Contains("package", StringComparison.OrdinalIgnoreCase))
            return "PackageLinkAmbiguous";
        if (missing.Contains("base", StringComparison.OrdinalIgnoreCase))
            return "BaseAmountMissing";
        if (missing.Contains("RateSemantics", StringComparison.OrdinalIgnoreCase) ||
            missing.Contains("rate", StringComparison.OrdinalIgnoreCase))
            return "RateSemanticsAmbiguous";
        if (missing.Contains("ManualReview", StringComparison.OrdinalIgnoreCase) ||
            missing.Contains("manual", StringComparison.OrdinalIgnoreCase))
            return "ManualReviewRequired";
        if (missing.Contains("amount", StringComparison.OrdinalIgnoreCase))
            return "AmountCannotBeInferred";

        return "ManualReviewRequired";
    }

    private long RequireTenantId()
    {
        return _current.TenantId
               ?? throw new AtlasException("Tenant context is required for BidOps lifecycle closure.");
    }

    private long RequireUserId()
    {
        return _current.UserId
               ?? throw new AtlasException("Authenticated user context is required for BidOps lifecycle closure.");
    }

    private async Task<BidOpsReverseClosureDebugResult> ReverseCloseRawNoticeCoreAsync(
        RawNotice awardRaw,
        string inputUrl,
        CancellationToken ct)
    {
        var result = new BidOpsReverseClosureDebugResult
        {
            InputAwardNoticeUrl = inputUrl,
            AwardNotice = new RawNoticeDebugRef(
                awardRaw.Id,
                awardRaw.Title,
                NormalizeAwardNoticeType(awardRaw.NoticeType, awardRaw.Title, awardRaw.DetailUrl),
                awardRaw.DetailUrl,
                awardRaw.PublishTime,
                awardRaw.FetchTime,
                awardRaw.Status.ToString())
        };

        var awardDocuments = await ReadEvidenceDocumentsAsync(awardRaw, ct);
        if (awardDocuments.Count == 0)
        {
            result.Warnings.Add("award raw notice text unavailable");
            AddFailure(
                result,
                "AwardEvidenceNotFound",
                "AwardExtraction",
                "Award notice text is unavailable.",
                awardRaw.Id,
                recommendedAction: "Run attachment/text extraction for the award notice, then retry reverse closure.");
        }

        var reviewPackages = await LoadReviewPackagesForRawNoticeAsync(awardRaw.Id, ct);
        var outcomeAwardEvidence = await LoadOutcomeAwardEvidenceAsync(awardRaw, reviewPackages, ct);
        var awardEvidence = MergeAwardEvidence(
            BidOpsAwardEvidenceParser.Extract(awardDocuments),
            outcomeAwardEvidence);
        result.AwardEvidence.AddRange(awardEvidence);
        var actionableAwardEvidence = awardEvidence
            .Where(award => !BidOpsOutcomeRecordPolicy.IsNonAwardAwardEvidence(award))
            .ToList();
        if (awardEvidence.Count == 0)
        {
            result.Warnings.Add("parser template not matched for award evidence");
            result.Warnings.Add("award amount missing");
            AddFailure(
                result,
                "AwardEvidenceNotFound",
                "AwardExtraction",
                "No award supplier evidence was extracted from the award notice.",
                awardRaw.Id,
                recommendedAction: "Inspect the award notice table format and add a parser fixture/template.");
            return result;
        }
        if (actionableAwardEvidence.Count == 0)
        {
            result.Warnings.Add("award notice only contains non-award status rows such as 流标/废标/失败; lifecycle closure suggestions were skipped.");
            return result;
        }

        var relatedRaw = await LoadRelatedRawNoticesAsync(awardRaw, actionableAwardEvidence, ct);
        var candidateDocuments = new List<(RawNotice Raw, IReadOnlyList<BidOpsEvidenceDocument> Documents)>();
        var tenderDocuments = new List<(RawNotice Raw, IReadOnlyList<BidOpsEvidenceDocument> Documents)>();
        foreach (var raw in relatedRaw)
        {
            if (LooksLikeCandidateNotice(raw))
            {
                var documents = await ReadEvidenceDocumentsAsync(raw, ct);
                var match = BidOpsNoticeCorrelationService.Match(raw, documents, actionableAwardEvidence, "Candidate", awardRaw.PublishTime);
                if (match.Confidence > 0 || match.MissingReason == null)
                    result.CandidateNoticeMatches.Add(match);
                if (match.Confidence >= 0.45)
                    candidateDocuments.Add((raw, documents));
            }
            else if (LooksLikeTenderNotice(raw))
            {
                var documents = await ReadEvidenceDocumentsAsync(raw, ct);
                var match = BidOpsNoticeCorrelationService.Match(raw, documents, actionableAwardEvidence, "Tender", awardRaw.PublishTime);
                if (match.Confidence > 0 || match.MissingReason == null)
                    result.TenderNoticeMatches.Add(match);
                if (match.Confidence >= 0.4)
                    tenderDocuments.Add((raw, documents));
            }
        }

        var candidateEvidence = candidateDocuments
            .SelectMany(x => BidOpsCandidateEvidenceParser.Extract(x.Documents))
            .ToList();
        var tenderEvidence = tenderDocuments
            .SelectMany(x => BidOpsTenderPackageEvidenceParser.Extract(x.Documents))
            .ToList();

        result.CandidateEvidence.AddRange(candidateEvidence);
        result.TenderPackageEvidence.AddRange(tenderEvidence);
        if (candidateEvidence.Count == 0)
        {
            result.Warnings.Add("candidate notice not found or parser template not matched");
            AddFailure(
                result,
                result.CandidateNoticeMatches.Count == 0 ? "CandidateNoticeNotFound" : "CandidateTemplateNotMatched",
                "CandidateReverseLookup",
                result.CandidateNoticeMatches.Count == 0
                    ? "No candidate notice matched the award notice metadata."
                    : "Candidate notice metadata matched, but no candidate supplier table was extracted.",
                awardRaw.Id,
                recommendedAction: result.CandidateNoticeMatches.Count == 0
                    ? "Scan/import earlier candidate announcements for the same project code or project name."
                    : "Inspect candidate notice attachments and add a parser fixture/template.");
        }
        if (tenderEvidence.Count == 0)
        {
            result.Warnings.Add("tender notice not found or parser template not matched");
            AddFailure(
                result,
                result.TenderNoticeMatches.Count == 0 ? "TenderNoticeNotFound" : "TenderTemplateNotMatched",
                "TenderReverseLookup",
                result.TenderNoticeMatches.Count == 0
                    ? "No tender/procurement notice matched the award notice metadata."
                    : "Tender/procurement notice metadata matched, but no package table was extracted.",
                awardRaw.Id,
                recommendedAction: result.TenderNoticeMatches.Count == 0
                    ? "Scan/import earlier tender/procurement announcements for the same project code or project name."
                    : "Inspect tender attachments and add a procurement table template.");
        }

        result.Closures.AddRange(BuildClosures(actionableAwardEvidence, candidateEvidence, tenderEvidence));
        if (result.Closures.Count == 0)
        {
            result.Warnings.Add("no lifecycle package closure could be suggested");
            AddFailure(
                result,
                "PackageLinkAmbiguous",
                "PackageLifecycleLink",
                "No package-level lifecycle closure could be suggested from the extracted evidence.",
                awardRaw.Id,
                recommendedAction: "Provide explicit project code, lot number/name, package number, or supplier evidence.");
        }

        foreach (var closure in result.Closures)
        {
            foreach (var missing in closure.MissingFields)
            {
                result.Warnings.Add($"{closure.PackageNo ?? "unknown package"}: {missing}");
                AddFailure(
                    result,
                    FailureCodeForMissingField(missing),
                    "PackageLifecycleLink",
                    $"{closure.PackageNo ?? "unknown package"}: {missing}",
                    closure.Award.Evidence.RawNoticeId,
                    recommendedAction: "Review the closure suggestion and provide the missing evidence before using it for supplier analytics.");
            }
        }

        result.Warnings = result.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        result.CandidateNoticeMatches = result.CandidateNoticeMatches
            .OrderByDescending(x => x.Confidence)
            .ThenByDescending(x => x.PublishTime)
            .Take(20)
            .ToList();
        result.TenderNoticeMatches = result.TenderNoticeMatches
            .OrderByDescending(x => x.Confidence)
            .ThenByDescending(x => x.PublishTime)
            .Take(20)
            .ToList();
        return result;
    }

    private async Task<RawNotice?> FindRawNoticeByUrlAsync(string url, CancellationToken ct)
    {
        var hash = _hasher.HashUrl(url);
        var builder = await _rawNotices.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        return await builder
            .Where(x => x.DetailUrlHash == hash || x.DetailUrl == url)
            .OrderByDescending(x => x.FetchTime)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<IReadOnlyList<RawNotice>> LoadRelatedRawNoticesAsync(
        RawNotice awardRaw,
        IReadOnlyList<AwardEvidence> awardEvidence,
        CancellationToken ct)
    {
        var builder = await _rawNotices.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        builder = builder
            .Where(x => x.Id != awardRaw.Id)
            .Where(x => awardRaw.PublishTime == null ||
                        x.PublishTime == null ||
                        x.PublishTime <= awardRaw.PublishTime.Value.AddDays(7));

        var projectCode = awardEvidence
            .Select(x => x.ProjectCode)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            var code = NormalizeProjectCodeForMatch(projectCode);
            var codeMatches = await builder
                .Where(x => x.SourceNoticeId.Contains(code) ||
                            x.Title.Contains(code) ||
                            x.TextPreview.Contains(code))
                .OrderByDescending(x => x.PublishTime ?? x.FetchTime)
                .Take(RelatedNoticeScanLimit)
                .ToListAsync(ct);
            if (codeMatches.Count > 0)
                return codeMatches;
        }

        var projectName = awardEvidence
            .Select(x => x.ProjectName)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var name = projectName.Trim();
            var titleMatches = await builder
                .Where(x => x.Title.Contains(name))
                .OrderByDescending(x => x.PublishTime ?? x.FetchTime)
                .Take(RelatedNoticeScanLimit)
                .ToListAsync(ct);
            if (titleMatches.Count > 0)
                return titleMatches;
        }

        return await builder
            .OrderByDescending(x => x.PublishTime ?? x.FetchTime)
            .Take(RelatedNoticeScanLimit)
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<PackageStaging>> LoadReviewPackagesForRawNoticeAsync(
        long rawNoticeId,
        CancellationToken ct)
    {
        var taskQuery = await _reviewTasks.QueryDataScopeAsync(
            BidOpsDataResources.ReviewTask,
            AtlasDataScopeType.AllTenant,
            ct);
        var reviewTask = await taskQuery
            .Where(x => x.RawNoticeId == rawNoticeId && x.BizType == "NoticeStaging")
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (reviewTask == null)
            return [];

        var packageQuery = await _packageStaging.QueryDataScopeAsync(
            BidOpsDataResources.ReviewTask,
            AtlasDataScopeType.AllTenant,
            ct);
        return await packageQuery
            .Where(x => x.NoticeStagingId == reviewTask.BizId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<AwardEvidence>> LoadOutcomeAwardEvidenceAsync(
        RawNotice raw,
        IReadOnlyList<PackageStaging> reviewPackages,
        CancellationToken ct)
    {
        var query = await _outcomeRecords.QueryDataScopeAsync(
            BidOpsDataResources.OutcomeSupplierRecord,
            AtlasDataScopeType.AllTenant,
            ct);
        var records = await query
            .Where(x => x.RawNoticeId == raw.Id)
            .ToListAsync(ct);
        records = records
            .OrderBy(x => x.ExtractionOrder)
            .ThenBy(x => x.Id)
            .ToList();
        if (records.Count == 0)
            return [];

        return BuildOutcomeAwardEvidence(raw, records, reviewPackages);
    }

    private async Task<IReadOnlyList<BidOpsEvidenceDocument>> ReadEvidenceDocumentsAsync(
        RawNotice raw,
        CancellationToken ct)
    {
        var documents = new List<BidOpsEvidenceDocument>();
        var rawText = await TryReadStoredTextAsync(raw.TextContentStorageKey, raw.TextPreview, raw.Id, ct);
        if (!string.IsNullOrWhiteSpace(rawText))
        {
            documents.Add(new BidOpsEvidenceDocument(
                new EvidenceSourceRef(
                    raw.Id,
                    null,
                    NormalizeAwardNoticeType(raw.NoticeType, raw.Title, raw.DetailUrl),
                    raw.DetailUrl,
                    null,
                    null,
                    null,
                    null,
                    null),
                raw.Title,
                raw.NoticeType,
                raw.PublishTime,
                rawText));
        }

        var attachmentBuilder = await _rawAttachments.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        var attachments = await attachmentBuilder
            .Where(x => x.RawNoticeId == raw.Id &&
                        x.TextExtractStatus == TextExtractStatus.Succeeded &&
                        x.TextContentStorageKey != string.Empty)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        foreach (var attachment in attachments)
        {
            var text = await TryReadStoredTextAsync(attachment.TextContentStorageKey, string.Empty, raw.Id, ct);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            documents.Add(new BidOpsEvidenceDocument(
                new EvidenceSourceRef(
                    raw.Id,
                    attachment.Id,
                    NormalizeAwardNoticeType(raw.NoticeType, raw.Title, raw.DetailUrl),
                    raw.DetailUrl,
                    attachment.FileName,
                    null,
                    null,
                    null,
                    null),
                raw.Title,
                raw.NoticeType,
                raw.PublishTime,
                text));
        }

        return documents;
    }

    private async Task<string> TryReadStoredTextAsync(
        string storageKey,
        string fallback,
        long rawNoticeId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return fallback;

        try
        {
            await using var stream = await _fileStore.OpenReadAsync(storageKey, ct);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync(ct);
            return text.Length <= StoredTextReadLimit ? text : text[..StoredTextReadLimit];
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                ex,
                "BidOps reverse lifecycle closure could not read stored text {StorageKey} for raw notice {RawNoticeId}.",
                storageKey,
                rawNoticeId);
            return fallback;
        }
    }

    private static BidOpsNoticeMatch MatchNotice(
        RawNotice raw,
        IReadOnlyList<BidOpsEvidenceDocument> documents,
        IReadOnlyList<AwardEvidence> awards,
        string stage)
    {
        var text = string.Join('\n', documents.Select(x => x.Text));
        var reasons = new List<string>();
        var confidence = 0d;
        var projectCodes = awards
            .Select(x => NormalizeProjectCodeForMatch(x.ProjectCode))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var projectNames = awards.Select(x => x.ProjectName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var packageNos = awards.Select(x => x.NormalizedPackageNo).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var noticeProjectCode = BidOpsEvidenceText.ExtractProjectCode(text);
        if (!string.IsNullOrWhiteSpace(noticeProjectCode) &&
            projectCodes.Any(x => ProjectCodeTextMatches(x, noticeProjectCode)))
        {
            confidence += 0.45;
            reasons.Add("ProjectCode exact match");
        }

        var noticeProjectName = FirstNonEmpty(BidOpsEvidenceText.ExtractProjectName(text), raw.Title);
        if (projectNames.Any(x => BidOpsEvidenceText.Similarity(x, noticeProjectName) >= 0.72))
        {
            confidence += 0.25;
            reasons.Add("ProjectName similarity match");
        }

        if (packageNos.Any(x => ContainsNormalizedPackageNo(text, x)))
        {
            confidence += 0.15;
            reasons.Add("PackageNo normalized hint matched");
        }

        if (stage == "Candidate" && LooksLikeCandidateNotice(raw))
        {
            confidence += 0.15;
            reasons.Add("Notice type/title indicates candidate announcement");
        }
        else if (stage == "Tender" && LooksLikeTenderNotice(raw))
        {
            confidence += 0.12;
            reasons.Add("Notice type/title indicates tender/procurement announcement");
        }

        var missingReason = confidence <= 0 ? "project code/name/package hint not matched" : null;
        return new BidOpsNoticeMatch(
            raw.Id,
            raw.Title,
            raw.NoticeType,
            raw.DetailUrl,
            raw.PublishTime,
            Math.Min(1d, confidence),
            reasons,
            missingReason);
    }

    private static IReadOnlyList<LifecyclePackageClosure> BuildClosures(
        IReadOnlyList<AwardEvidence> awards,
        IReadOnlyList<CandidateEvidence> candidates,
        IReadOnlyList<TenderPackageEvidence> tenders)
    {
        var closures = new List<LifecyclePackageClosure>();
        foreach (var award in awards)
        {
            if (BidOpsOutcomeRecordPolicy.IsNonAwardAwardEvidence(award))
                continue;

            var packageCandidates = candidates
                .Where(candidate => PackageContextMatches(
                    award.LotNo,
                    award.LotName,
                    award.NormalizedPackageNo,
                    candidate.LotNo,
                    null,
                    candidate.NormalizedPackageNo))
                .Where(candidate => ProjectMatches(award.ProjectCode, award.ProjectName, candidate.ProjectCode, candidate.ProjectName))
                .OrderBy(candidate => candidate.Rank ?? 99)
                .ThenByDescending(candidate => candidate.FinalQuoteAmount.HasValue)
                .ToList();
            var matchedCandidate = packageCandidates
                .Where(candidate => SupplierMatches(award.AwardedSupplierName, candidate.SupplierName))
                .OrderBy(candidate => candidate.Rank == 1 ? 0 : 1)
                .ThenBy(candidate => candidate.Rank ?? 99)
                .FirstOrDefault();
            var packageTenders = tenders
                .Where(tender => PackageContextMatches(
                    award.LotNo,
                    award.LotName,
                    award.NormalizedPackageNo,
                    tender.LotNo,
                    tender.LotName,
                    tender.NormalizedPackageNo))
                .Where(tender => ProjectMatches(award.ProjectCode, award.ProjectName, tender.ProjectCode, tender.ProjectName))
                .ToList();
            var tender = MergeTenderEvidence(packageTenders, award, matchedCandidate);

            var reasons = new List<string>();
            var missing = new List<string>();
            var confidence = 0.35d;
            if (!string.IsNullOrWhiteSpace(award.ProjectCode))
            {
                confidence += 0.15;
                reasons.Add("ProjectCode present on award evidence");
            }
            else
            {
                missing.Add("project code missing");
            }

            if (!string.IsNullOrWhiteSpace(award.NormalizedPackageNo))
            {
                confidence += 0.15;
                reasons.Add("PackageNo normalized from award evidence");
            }
            else
            {
                missing.Add("package number ambiguous");
            }

            if (matchedCandidate != null)
            {
                confidence += 0.2;
                reasons.Add("Awarded supplier matched candidate");
                if (matchedCandidate.Rank == 1)
                {
                    confidence += 0.1;
                    reasons.Add("Awarded supplier matched candidate rank 1");
                }
            }
            else if (packageCandidates.Count > 0)
            {
                confidence += 0.08;
                missing.Add("awarded supplier did not match candidate supplier");
            }
            else
            {
                missing.Add("candidate notice not found");
            }

            if (tender != null)
            {
                confidence += 0.08;
                reasons.Add("Tender/procurement package evidence matched");
                if (tender.BudgetAmount == null && tender.MaxPrice == null && tender.GuidePrice == null)
                    missing.Add("tender amount missing");
            }
            else
            {
                missing.Add("tender notice not found");
            }

            var pricingDecision = BidOpsPricingInferenceService.Infer(award, matchedCandidate, packageCandidates, tender);
            if (pricingDecision.AmountValue.HasValue)
                confidence += pricingDecision.AmountKind == BidOpsAmountKinds.DirectAwardAmount ? 0.08 : 0.07;
            else
                missing.Add("award amount missing");
            reasons.AddRange(pricingDecision.Reasons);
            missing.AddRange(pricingDecision.MissingReasons);
            var amountSource = pricingDecision.AmountKind == BidOpsAmountKinds.Unknown
                ? "Missing"
                : pricingDecision.AmountKind;

            closures.Add(new LifecyclePackageClosure(
                ProjectCode: FirstNonEmpty(award.ProjectCode, matchedCandidate?.ProjectCode, tender?.ProjectCode),
                ProjectName: FirstNonEmpty(award.ProjectName, matchedCandidate?.ProjectName, tender?.ProjectName),
                ProjectUnit: FirstNonEmpty(award.ProjectUnit),
                LotNo: FirstNonEmpty(award.LotNo, matchedCandidate?.LotNo, tender?.LotNo),
                LotName: FirstNonEmpty(award.LotName, tender?.LotName),
                PackageNo: FirstNonEmpty(award.PackageNo, matchedCandidate?.PackageNo, tender?.PackageNo),
                NormalizedPackageNo: FirstNonEmpty(award.NormalizedPackageNo, matchedCandidate?.NormalizedPackageNo, tender?.NormalizedPackageNo),
                PackageName: FirstNonEmpty(award.PackageName, matchedCandidate?.PackageName, tender?.PackageName),
                Tender: tender,
                Candidates: packageCandidates,
                Award: award,
                FinalAwardAmount: pricingDecision.AmountValue,
                FinalAwardAmountSource: amountSource,
                AmountEvidence: pricingDecision.AmountSourceNoticeId.HasValue ? pricingDecision.AmountSourceStage == BidOpsAmountSourceStages.CandidateNotice ? matchedCandidate?.Evidence : award.RateEvidence?.Evidence ?? award.Evidence : null,
                LinkConfidence: Math.Min(1d, Math.Round(confidence, 3)),
                MatchReasons: reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                MissingFields: missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                RequiresManualReview: pricingDecision.RequiresManualReview || confidence < 0.9d || missing.Count > 0,
                PricingDecision: pricingDecision,
                MatchedCandidate: matchedCandidate));
        }

        return closures;
    }

    private static TenderPackageEvidence? MergeTenderEvidence(
        IReadOnlyList<TenderPackageEvidence> tenders,
        AwardEvidence award,
        CandidateEvidence? candidate)
    {
        if (tenders.Count == 0)
            return null;

        var ordered = tenders
            .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.ScopeText))
            .ThenByDescending(x => x.BudgetAmount.HasValue || x.MaxPrice.HasValue)
            .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.QualificationText))
            .ToList();
        var primary = ordered[0];
        return primary with
        {
            ProjectCode = FirstNonEmpty(primary.ProjectCode, candidate?.ProjectCode, award.ProjectCode),
            ProjectName = FirstNonEmpty(primary.ProjectName, candidate?.ProjectName, award.ProjectName),
            LotNo = FirstNonEmpty(primary.LotNo, candidate?.LotNo, award.LotNo),
            LotName = FirstNonEmpty(primary.LotName, award.LotName),
            PackageNo = FirstNonEmpty(primary.PackageNo, candidate?.PackageNo, award.PackageNo),
            NormalizedPackageNo = FirstNonEmpty(primary.NormalizedPackageNo, candidate?.NormalizedPackageNo, award.NormalizedPackageNo),
            PackageName = FirstNonEmpty(primary.PackageName, candidate?.PackageName, award.PackageName),
            ScopeText = FirstNonEmpty(Prepend(primary.ScopeText, ordered.Select(x => x.ScopeText))),
            BudgetAmount = primary.BudgetAmount ?? ordered.Select(x => x.BudgetAmount).FirstOrDefault(x => x.HasValue),
            MaxPrice = primary.MaxPrice ?? ordered.Select(x => x.MaxPrice).FirstOrDefault(x => x.HasValue),
            GuidePrice = primary.GuidePrice ?? ordered.Select(x => x.GuidePrice).FirstOrDefault(x => x.HasValue),
            QualificationText = FirstNonEmpty(Prepend(primary.QualificationText, ordered.Select(x => x.QualificationText))),
            PerformanceRequirement = FirstNonEmpty(Prepend(primary.PerformanceRequirement, ordered.Select(x => x.PerformanceRequirement))),
            PersonnelRequirement = FirstNonEmpty(Prepend(primary.PersonnelRequirement, ordered.Select(x => x.PersonnelRequirement)))
        };
    }

    private static bool PackageMatches(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PackageContextMatches(
        string? awardLotNo,
        string? awardLotName,
        string? awardPackageNo,
        string? otherLotNo,
        string? otherLotName,
        string? otherPackageNo)
    {
        if (!PackageMatches(awardPackageNo, otherPackageNo))
            return false;

        var awardHasLotContext = !string.IsNullOrWhiteSpace(awardLotNo) || !string.IsNullOrWhiteSpace(awardLotName);
        var otherHasLotContext = !string.IsNullOrWhiteSpace(otherLotNo) || !string.IsNullOrWhiteSpace(otherLotName);
        if (!awardHasLotContext || !otherHasLotContext)
            return true;

        var matchedAnyLotContext = false;
        if (!string.IsNullOrWhiteSpace(awardLotNo) && !string.IsNullOrWhiteSpace(otherLotNo))
        {
            if (!string.Equals(
                    BidOpsTextQuality.CleanExtractedValue(awardLotNo),
                    BidOpsTextQuality.CleanExtractedValue(otherLotNo),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            matchedAnyLotContext = true;
        }

        if (!string.IsNullOrWhiteSpace(awardLotName) && !string.IsNullOrWhiteSpace(otherLotName))
        {
            if (!string.Equals(
                    BidOpsTextQuality.CleanExtractedValue(awardLotName),
                    BidOpsTextQuality.CleanExtractedValue(otherLotName),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            matchedAnyLotContext = true;
        }

        return matchedAnyLotContext;
    }

    private static bool ProjectMatches(
        string? awardProjectCode,
        string? awardProjectName,
        string? otherProjectCode,
        string? otherProjectName)
    {
        if (!string.IsNullOrWhiteSpace(awardProjectCode) &&
            !string.IsNullOrWhiteSpace(otherProjectCode))
        {
            return ProjectCodeTextMatches(awardProjectCode, otherProjectCode);
        }

        if (!string.IsNullOrWhiteSpace(awardProjectName) &&
            !string.IsNullOrWhiteSpace(otherProjectName))
        {
            return BidOpsEvidenceText.Similarity(awardProjectName, otherProjectName) >= 0.55;
        }

        return true;
    }

    private static bool SupplierMatches(string? left, string? right)
    {
        return SupplierNamesCompatible(left, right);
    }

    private static bool SupplierNamesCompatible(string? left, string? right)
    {
        var normalizedLeft = BidOpsSupplierNameNormalizer.NormalizeForMatch(left);
        var normalizedRight = BidOpsSupplierNameNormalizer.NormalizeForMatch(right);
        if (string.IsNullOrWhiteSpace(normalizedLeft) ||
            string.IsNullOrWhiteSpace(normalizedRight))
        {
            return false;
        }

        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase) ||
               IsLikelyPrefixTruncatedSupplierName(normalizedLeft, normalizedRight);
    }

    private static bool IsLikelyPrefixTruncatedSupplierName(string left, string right)
    {
        var longer = left.Length >= right.Length ? left : right;
        var shorter = left.Length < right.Length ? left : right;
        return shorter.Length >= 8 &&
               longer.Length - shorter.Length is > 0 and <= 2 &&
               longer.EndsWith(shorter, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsNormalizedPackageNo(string text, string? normalizedPackageNo)
    {
        if (string.IsNullOrWhiteSpace(normalizedPackageNo))
            return false;

        return text.Split(['\r', '\n', '。', '；', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(BidOpsPackageNoNormalizer.Normalize)
            .Any(x => string.Equals(x, normalizedPackageNo, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeCandidateNotice(RawNotice raw)
    {
        return ContainsAny($"{raw.NoticeType} {raw.Title}", "Candidate", "候选人", "推荐成交候选", "推荐中标候选", "公示");
    }

    private static bool LooksLikeTenderNotice(RawNotice raw)
    {
        var signal = $"{raw.NoticeType} {raw.Title}";
        if (ContainsAny(signal, "Candidate", "Award", "中标", "成交结果", "候选"))
            return false;

        return ContainsAny(
            signal,
            "Tender",
            "Procurement",
            "招标公告",
            "投标邀请",
            "采购公告",
            "采购邀请",
            "公开谈判采购",
            "竞争性谈判采购",
            "询价采购");
    }

    private static bool IsProcurementNoticeCandidate(StateGridEcpPublicNoticeCandidate candidate)
    {
        if (!string.Equals(candidate.Doctype, "doci-bid", StringComparison.OrdinalIgnoreCase))
            return false;

        var signal = $"{candidate.NoticeType} {candidate.Title}";
        return ContainsAny(signal, "Tender", "Procurement", "招标", "采购", "谈判", "询价");
    }

    private static string NormalizeProcurementNoticeType(string? noticeType, string menuId)
    {
        if (!string.IsNullOrWhiteSpace(noticeType) &&
            ContainsAny(noticeType, "TenderAnnouncement", "ProcurementAnnouncement"))
        {
            return noticeType.Trim();
        }

        return string.Equals(menuId, StateGridProcurementNoticeMenuId, StringComparison.OrdinalIgnoreCase)
            ? "ProcurementAnnouncement"
            : "TenderAnnouncement";
    }

    private static string ResolveSourceNoticeType(RawNotice raw)
    {
        return ResolveSourceNoticeType(raw.NoticeType, raw.Title, TryGetStateGridMenuId(raw.DetailUrl));
    }

    private static string ResolveSourceNoticeType(StateGridEcpPublicNoticeCandidate candidate)
    {
        return ResolveSourceNoticeType(candidate.NoticeType, candidate.Title, candidate.MenuId);
    }

    private static string ResolveSourceNoticeType(string? noticeType, string? title, string? menuId)
    {
        var signal = $"{noticeType} {title}";
        if (ContainsAny(signal, "资格预审"))
            return BidOpsSourceNoticeTypes.PrequalificationNotice;
        if (ContainsAny(signal, "变更公告", "澄清", "更正"))
            return BidOpsSourceNoticeTypes.ChangeNotice;
        if (ContainsAny(signal, "投标邀请", "邀请书", "BidInvitation"))
            return BidOpsSourceNoticeTypes.BidInvitation;
        if (ContainsAny(signal, "采购邀请", "采购邀请函", "ProcurementInvitation"))
            return BidOpsSourceNoticeTypes.ProcurementInvitation;
        if (string.Equals(menuId, StateGridTenderNoticeMenuId, StringComparison.OrdinalIgnoreCase) ||
            ContainsAny(signal, "TenderAnnouncement", "TenderNotice", "招标公告", "招标"))
        {
            return BidOpsSourceNoticeTypes.TenderNotice;
        }

        if (string.Equals(menuId, StateGridProcurementNoticeMenuId, StringComparison.OrdinalIgnoreCase) ||
            ContainsAny(signal, "ProcurementAnnouncement", "ProcurementNotice", "采购公告", "采购"))
        {
            return BidOpsSourceNoticeTypes.ProcurementNotice;
        }

        return BidOpsSourceNoticeTypes.Unknown;
    }

    private static string TryGetStateGridMenuId(string? detailUrl)
    {
        return !string.IsNullOrWhiteSpace(detailUrl) &&
               StateGridEcpWcmParser.TryParsePortalDetailUrl(detailUrl, out _, out _, out var menuId)
            ? menuId
            : string.Empty;
    }

    private static bool ProjectCodeTextMatches(string? left, string? right)
    {
        var normalizedLeft = NormalizeProjectCodeForMatch(left);
        var normalizedRight = NormalizeProjectCodeForMatch(right);
        return !string.IsNullOrWhiteSpace(normalizedLeft) &&
               !string.IsNullOrWhiteSpace(normalizedRight) &&
               string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProjectCodeForMatch(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value)
            .Replace('－', '-')
            .Replace('—', '-')
            .Replace('–', '-')
            .Replace('／', '/');
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        var fromLotNo = BidOpsEvidenceText.ExtractProjectCodeFromLotNo(cleaned);
        if (!string.IsNullOrWhiteSpace(fromLotNo))
            return fromLotNo;

        cleaned = Regex.Replace(
            cleaned,
            @"^(?:code|项目编号|项目编码|项目代码|采购编号|采购项目编号|采购项目编码|招标编号|招标项目编号|招标项目编码|批次编号)\s*[:：=]\s*",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var match = Regex.Match(cleaned, @"[A-Za-z0-9][A-Za-z0-9_.\-/]{2,}", RegexOptions.CultureInvariant);
        var normalized = match.Success
            ? match.Value.ToUpperInvariant()
            : cleaned.Trim(' ', '\t', '。', '.', '；', ';', '，', ',', '、', '（', '(', '）', ')').ToUpperInvariant();
        return IsValidProjectCodeForMatch(normalized) ? normalized : string.Empty;
    }

    private static string ResolveExplicitProjectCodeForMatch(params string?[] values)
    {
        foreach (var value in values)
        {
            var explicitCode = BidOpsEvidenceText.ExtractProjectCode(value);
            var normalizedExplicit = NormalizeProjectCodeForMatch(explicitCode);
            if (!string.IsNullOrWhiteSpace(normalizedExplicit))
                return normalizedExplicit;
        }

        return string.Empty;
    }

    private static string ResolveManualProjectCodeForMatch(
        string? projectCode,
        string? manualRemark,
        string? evidenceJson)
    {
        var overrideCode = ResolveManualProjectCodeFromEvidence(evidenceJson);
        if (!string.IsNullOrWhiteSpace(overrideCode))
            return overrideCode;

        if (!string.IsNullOrWhiteSpace(manualRemark) &&
            manualRemark.Contains(ManualProjectCodeRemarkMarker, StringComparison.Ordinal))
        {
            var normalized = ResolveManualProjectCodeFromRemark(manualRemark);
            if (!string.IsNullOrWhiteSpace(normalized))
                return normalized;
        }

        return string.Empty;
    }

    private static string ResolveManualProjectCodeFromRemark(string manualRemark)
    {
        var matches = Regex.Matches(
            manualRemark,
            Regex.Escape(ManualProjectCodeRemarkMarker) + @"\s*([A-Za-z0-9][A-Za-z0-9_.\-/－—–／]{2,})",
            RegexOptions.CultureInvariant);
        if (matches.Count == 0)
            return string.Empty;

        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var normalized = NormalizeProjectCodeForMatch(matches[i].Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(normalized))
                return normalized;
        }

        return string.Empty;
    }

    private static string ResolveManualProjectCodeFromEvidence(string? evidenceJson)
    {
        if (string.IsNullOrWhiteSpace(evidenceJson))
            return string.Empty;

        try
        {
            var root = JsonNode.Parse(evidenceJson)?.AsObject();
            var value = root?["manualProjectCodeOverride"]?["projectCode"]?.GetValue<string>();
            return NormalizeProjectCodeForMatch(value);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static bool HasManualProjectCodeOverride(LifecyclePackageLinkDto link)
    {
        return !string.IsNullOrWhiteSpace(ResolveManualProjectCodeForMatch(
            link.ProjectCode,
            link.ManualRemark,
            link.EvidenceJson));
    }

    private static string ResolveProjectCodeForMatch(params string?[] values)
    {
        var explicitCode = ResolveExplicitProjectCodeForMatch(values);
        if (!string.IsNullOrWhiteSpace(explicitCode))
            return explicitCode;

        foreach (var value in values)
        {
            var lotCode = BidOpsEvidenceText.ExtractProjectCodeFromLotNo(value);
            if (!string.IsNullOrWhiteSpace(lotCode))
                return lotCode;
        }

        foreach (var value in values)
        {
            var normalized = NormalizeProjectCodeForMatch(value);
            if (!string.IsNullOrWhiteSpace(normalized))
                return normalized;
        }

        return string.Empty;
    }

    private static bool IsValidProjectCodeForMatch(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            IsInvalidProjectCodeForMatch(value))
        {
            return false;
        }

        // 历史闭环行可能把“包”“包1”等包号字段误写进 ProjectCode；项目/采购编号必须是可用于国网检索的 ASCII 编码。
        return Regex.IsMatch(value, @"^[A-Z0-9][A-Z0-9_.\-/]{2,}$", RegexOptions.CultureInvariant);
    }

    private static bool IsInvalidProjectCodeForMatch(string value)
    {
        return value.Equals("URL", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("SOURCEURL", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("PROJECTCODE", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("LISTPUBLISHTIME", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("PUBLISHTIME", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("NOTICEID", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("FIRSTPAGEDOCID", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("MENUID", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAwardNoticeType(string noticeType, string title, string detailUrl)
    {
        var signal = $"{noticeType} {title} {detailUrl}";
        if (ContainsAny(signal, "doci-win", "Award", "Win", "中标结果", "成交结果", "中标公告", "成交公告"))
            return "AwardNotice";
        if (ContainsAny(signal, "Candidate", "候选"))
            return "CandidateNotice";
        if (ContainsAny(signal, "Tender", "Procurement", "招标公告", "采购公告"))
            return "TenderAnnouncement";
        return noticeType;
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static LifecycleNoticeRefDto MapLifecycleNoticeRef(RawNotice raw, string matchSource)
    {
        return new LifecycleNoticeRefDto
        {
            RawNoticeId = raw.Id,
            Title = raw.Title,
            NoticeType = raw.NoticeType,
            DetailUrl = raw.DetailUrl,
            PublishTime = raw.PublishTime,
            MatchSource = matchSource
        };
    }

    private static RawAttachmentDto MapRawAttachment(RawAttachment attachment)
    {
        return new RawAttachmentDto
        {
            Id = attachment.Id,
            RawNoticeId = attachment.RawNoticeId,
            FileName = attachment.FileName,
            FileUrl = attachment.FileUrl,
            FileType = attachment.FileType,
            FileSize = attachment.FileSize,
            DownloadStatus = attachment.DownloadStatus,
            TextExtractStatus = attachment.TextExtractStatus,
            HasLocalFile = attachment.StorageKey != string.Empty,
            HasExtractedText = attachment.TextContentStorageKey != string.Empty,
            CreatedAt = attachment.CreatedAt
        };
    }

    private static OutcomeSupplierRecordDto MapOutcomeRecord(OutcomeSupplierRecord record)
    {
        return new OutcomeSupplierRecordDto
        {
            Id = record.Id,
            RawNoticeId = record.RawNoticeId,
            NoticeId = record.NoticeId,
            TenderPackageId = record.TenderPackageId,
            BuyerId = record.BuyerId,
            SupplierId = record.SupplierId,
            SourceUrl = record.SourceUrl,
            NoticeTitle = record.NoticeTitle,
            NoticeType = record.NoticeType,
            ProjectName = record.ProjectName,
            ProjectCode = record.ProjectCode,
            BuyerName = record.BuyerName,
            Region = record.Region,
            PublishTime = record.PublishTime,
            LotNo = record.LotNo,
            LotName = record.LotName,
            PackageNo = record.PackageNo,
            PackageName = record.PackageName,
            Category = record.Category,
            SupplierName = record.SupplierName,
            OutcomeType = record.OutcomeType,
            Rank = record.Rank,
            AwardAmount = BidOpsOutcomeRecordPolicy.DisplayAwardAmount(record),
            ProcurementAgencyServiceFeeAmount = record.ProcurementAgencyServiceFeeAmount,
            ExtractionOrder = record.ExtractionOrder,
            Currency = record.Currency,
            EvidenceText = record.EvidenceText,
            ExtractionConfidence = record.ExtractionConfidence,
            CreatedAt = record.CreatedAt
        };
    }

    private static ProcurementDetailStagingDto MapProcurementDetailStaging(ProcurementDetailStaging detail)
    {
        return new ProcurementDetailStagingDto
        {
            Id = detail.Id,
            NoticeStagingId = detail.NoticeStagingId,
            PackageStagingId = detail.PackageStagingId,
            RawNoticeId = detail.RawNoticeId,
            RawAttachmentId = detail.RawAttachmentId,
            TableIndex = detail.TableIndex,
            RowIndex = detail.RowIndex,
            SourceSheetName = detail.SourceSheetName,
            ProjectCode = detail.ProjectCode,
            ProjectName = detail.ProjectName,
            ProcurementApplicationNo = detail.ProcurementApplicationNo,
            LineItemNo = detail.LineItemNo,
            MaterialCode = detail.MaterialCode,
            LotSequence = detail.LotSequence,
            LotNo = detail.LotNo,
            LotName = detail.LotName,
            EcpLotName = detail.EcpLotName,
            PackageNo = detail.PackageNo,
            PackageName = detail.PackageName,
            PackageType = detail.PackageType,
            Category = detail.Category,
            ProcurementMethod = detail.ProcurementMethod,
            BuyerName = detail.BuyerName,
            ProjectUnit = detail.ProjectUnit,
            ConstructionUnit = detail.ConstructionUnit,
            ProcurementContent = detail.ProcurementContent,
            ScopeText = detail.ScopeText,
            ProjectOverview = detail.ProjectOverview,
            Location = detail.Location,
            VoltageLevel = detail.VoltageLevel,
            ProcurementAmount = detail.ProcurementAmount,
            BudgetAmount = detail.BudgetAmount,
            ItemEstimatedAmount = detail.ItemEstimatedAmount,
            PackageEstimatedAmount = detail.PackageEstimatedAmount,
            MaxPrice = detail.MaxPrice,
            MaxPriceRatePercent = detail.MaxPriceRatePercent,
            TaxRatePercent = detail.TaxRatePercent,
            ResponseGuaranteeAmount = detail.ResponseGuaranteeAmount,
            QuoteMode = detail.QuoteMode,
            SettlementMode = detail.SettlementMode,
            PlannedStartDate = detail.PlannedStartDate,
            PlannedCompletionDate = detail.PlannedCompletionDate,
            ServicePeriodDays = detail.ServicePeriodDays,
            ServicePeriodText = detail.ServicePeriodText,
            QualificationRequirement = detail.QualificationRequirement,
            PerformanceRequirement = detail.PerformanceRequirement,
            PersonnelRequirement = detail.PersonnelRequirement,
            OtherRequirement = detail.OtherRequirement,
            JointVentureAllowed = detail.JointVentureAllowed,
            SubcontractAllowed = detail.SubcontractAllowed,
            AwardLimit = detail.AwardLimit,
            TechnicalSpecId = detail.TechnicalSpecId,
            ContractTemplate = detail.ContractTemplate,
            BusinessWeight = detail.BusinessWeight,
            TechnicalWeight = detail.TechnicalWeight,
            PriceWeight = detail.PriceWeight,
            PriceCalculationMethod = detail.PriceCalculationMethod,
            PriceParameter = detail.PriceParameter,
            Remarks = detail.Remarks,
            OriginalHeaderJson = detail.OriginalHeaderJson,
            OriginalRowJson = detail.OriginalRowJson,
            NormalizedFieldsJson = detail.NormalizedFieldsJson,
            AiConfidence = detail.AiConfidence,
            ReviewStatus = detail.ReviewStatus
        };
    }

    private static string ProcurementNoticeMissingReason(string projectCode)
    {
        return string.IsNullOrWhiteSpace(projectCode)
            ? "未匹配到前置公告 RawNotice；当前闭环建议缺少招标编号/采购编号，无法按编号反查前置公告。"
            : $"未匹配到前置公告 RawNotice；请先采集或导入招标编号/采购编号 {projectCode} 对应的前置公告。";
    }

    private static IReadOnlyList<AwardEvidence> MergeAwardEvidence(
        IReadOnlyList<AwardEvidence> parsed,
        IReadOnlyList<AwardEvidence> outcomeRecords)
    {
        if (outcomeRecords.Count == 0)
            return parsed.ToList();
        if (parsed.Count == 0)
            return outcomeRecords.ToList();

        var merged = new List<AwardEvidence>();
        var matchedParsed = new HashSet<int>();
        foreach (var outcome in outcomeRecords)
        {
            var parsedIndex = parsed
                .Select((item, index) => (item, index))
                .Where(x => !matchedParsed.Contains(x.index))
                .Where(x => AwardEvidenceMatches(outcome, x.item))
                .Select(x => (int?)x.index)
                .FirstOrDefault();
            if (!parsedIndex.HasValue)
            {
                merged.Add(outcome);
                continue;
            }

            matchedParsed.Add(parsedIndex.Value);
            merged.Add(EnrichAwardEvidence(parsed[parsedIndex.Value], outcome));
        }

        for (var i = 0; i < parsed.Count; i++)
        {
            if (!matchedParsed.Contains(i) &&
                ShouldAppendUnmatchedParsedAwardEvidence(parsed[i]))
            {
                merged.Add(parsed[i]);
            }
        }

        return PruneLessSpecificAwardEvidence(merged);
    }

    private static bool ShouldAppendUnmatchedParsedAwardEvidence(AwardEvidence parsed)
    {
        if (HasExplicitAwardLotNo(parsed))
            return true;

        if (parsed.AwardAmount.HasValue &&
            !string.IsNullOrWhiteSpace(parsed.PackageNo) &&
            !string.IsNullOrWhiteSpace(parsed.AwardedSupplierName))
        {
            return true;
        }

        // PDF 文本错列时会把“分标名称/包号/成交人”挤成类似“业形象及文化宣传 包 1 山东...有限”的弱行。
        // 已有 OutcomeSupplierRecord 时，这类没有真实分标编号和包名的弱证据不能再补进闭环候选。
        var packageName = NormalizeMatchText(parsed.PackageName);
        var lotName = NormalizeMatchText(parsed.LotName);
        return !string.IsNullOrWhiteSpace(packageName) &&
               !string.Equals(lotName, "包", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasExplicitAwardLotNo(AwardEvidence parsed)
    {
        return !string.IsNullOrWhiteSpace(BidOpsEvidenceText.ExtractProjectCodeFromLotNo(parsed.LotNo));
    }

    private static IReadOnlyList<AwardEvidence> PruneLessSpecificAwardEvidence(IReadOnlyList<AwardEvidence> evidence)
    {
        if (evidence.Count < 2)
            return evidence;

        return evidence
            .Where(current => !evidence.Any(other => IsLessSpecificAwardEvidenceDuplicate(current, other)))
            .ToList();
    }

    private static bool IsLessSpecificAwardEvidenceDuplicate(AwardEvidence current, AwardEvidence other)
    {
        if (ReferenceEquals(current, other) ||
            !AwardPackageIdentityMatches(current, other))
        {
            return false;
        }

        // 同分标同包下，PDF/回退解析可能把“普通合伙”等法人类型截掉；闭环只保留供应商名称更完整的证据。
        var currentSupplier = BidOpsSupplierNameNormalizer.NormalizeForMatch(current.AwardedSupplierName);
        var otherSupplier = BidOpsSupplierNameNormalizer.NormalizeForMatch(other.AwardedSupplierName);
        return currentSupplier.Length >= 8 &&
               otherSupplier.Length >= currentSupplier.Length + 3 &&
               otherSupplier.Contains(currentSupplier, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AwardPackageIdentityMatches(AwardEvidence left, AwardEvidence right)
    {
        var leftLotNo = NormalizeMatchText(left.LotNo);
        var rightLotNo = NormalizeMatchText(right.LotNo);
        var leftPackageNo = NormalizePackageNoForMatch(left.PackageNo ?? left.NormalizedPackageNo);
        var rightPackageNo = NormalizePackageNoForMatch(right.PackageNo ?? right.NormalizedPackageNo);

        return !string.IsNullOrWhiteSpace(leftLotNo) &&
               !string.IsNullOrWhiteSpace(rightLotNo) &&
               !string.IsNullOrWhiteSpace(leftPackageNo) &&
               !string.IsNullOrWhiteSpace(rightPackageNo) &&
               string.Equals(leftLotNo, rightLotNo, StringComparison.Ordinal) &&
               string.Equals(leftPackageNo, rightPackageNo, StringComparison.Ordinal);
    }

    private static IReadOnlyList<AwardEvidence> BuildOutcomeAwardEvidence(
        RawNotice raw,
        IReadOnlyList<OutcomeSupplierRecord> records,
        IReadOnlyList<PackageStaging> reviewPackages)
    {
        return records
            .Where(x => !string.IsNullOrWhiteSpace(x.SupplierName))
            .Select(record =>
            {
                var matchedPackage = MatchReviewPackage(record, reviewPackages);
                var lotNo = FirstNonEmpty(record.LotNo, matchedPackage?.LotNo);
                var lotName = FirstNonEmpty(record.LotName, matchedPackage?.LotName);
                var packageNo = FirstNonEmpty(record.PackageNo, matchedPackage?.PackageNo);
                var packageName = FirstNonEmpty(record.PackageName, matchedPackage?.PackageName);
                var evidenceText = FirstNonEmpty(record.EvidenceText, matchedPackage?.PackageName, raw.Title);
                var awardAmount = BidOpsOutcomeRecordPolicy.IsNonAwardOutcome(record)
                    ? null
                    : record.AwardAmount;
                return new AwardEvidence(
                    ResolveProjectCodeForMatch(record.ProjectCode, record.LotNo, matchedPackage?.LotNo, evidenceText),
                    FirstNonEmpty(record.ProjectName, raw.Title),
                    null,
                    lotNo,
                    lotName,
                    packageNo,
                    BidOpsPackageNoNormalizer.Normalize(packageNo),
                    packageName,
                    record.SupplierName,
                    awardAmount,
                    awardAmount.HasValue ? BidOpsAmountKinds.DirectAwardAmount : "Missing",
                    new EvidenceSourceRef(
                        raw.Id,
                        null,
                        NormalizeAwardNoticeType(raw.NoticeType, raw.Title, raw.DetailUrl),
                        raw.DetailUrl,
                        null,
                        null,
                        record.ExtractionOrder,
                        null,
                        evidenceText),
                    Convert.ToDouble(record.ExtractionConfidence));
            })
            .ToList();
    }

    private static AwardEvidence EnrichAwardEvidence(AwardEvidence parsed, AwardEvidence outcome)
    {
        var supplierName = PreferMoreCompleteSupplierName(parsed.AwardedSupplierName, outcome.AwardedSupplierName);
        return parsed with
        {
            ProjectCode = FirstNonEmpty(parsed.ProjectCode, outcome.ProjectCode),
            ProjectName = FirstNonEmpty(parsed.ProjectName, outcome.ProjectName),
            ProjectUnit = FirstNonEmpty(parsed.ProjectUnit, outcome.ProjectUnit),
            LotNo = FirstNonEmpty(parsed.LotNo, outcome.LotNo),
            LotName = FirstNonEmpty(parsed.LotName, outcome.LotName),
            PackageNo = FirstNonEmpty(parsed.PackageNo, outcome.PackageNo),
            NormalizedPackageNo = FirstNonEmpty(parsed.NormalizedPackageNo, outcome.NormalizedPackageNo),
            PackageName = FirstNonEmpty(parsed.PackageName, outcome.PackageName),
            AwardedSupplierName = supplierName,
            AwardAmount = parsed.AwardAmount ?? outcome.AwardAmount,
            AmountSource = parsed.AwardAmount.HasValue ? parsed.AmountSource : outcome.AmountSource,
            Evidence = PreferAwardEvidenceSource(parsed, outcome, supplierName),
            Confidence = Math.Max(parsed.Confidence, outcome.Confidence)
        };
    }

    private static string PreferMoreCompleteSupplierName(string parsedSupplierName, string outcomeSupplierName)
    {
        var parsed = NormalizeMatchText(parsedSupplierName);
        var outcome = NormalizeMatchText(outcomeSupplierName);
        if (string.IsNullOrWhiteSpace(outcome))
            return parsedSupplierName;
        if (string.IsNullOrWhiteSpace(parsed))
            return outcomeSupplierName;

        // OutcomeSupplierRecord 来自审核/重解析后的规范化行；当 parsed 行只截到供应商短名时，保留更完整的成交人名称。
        return outcome.Length > parsed.Length &&
               outcome.Contains(parsed, StringComparison.OrdinalIgnoreCase)
            ? outcomeSupplierName
            : parsedSupplierName;
    }

    private static EvidenceSourceRef PreferAwardEvidenceSource(AwardEvidence parsed, AwardEvidence outcome, string supplierName)
    {
        if (parsed.AwardAmount.HasValue)
            return parsed.Evidence;

        var parsedText = NormalizeMatchText(parsed.Evidence.EvidenceText);
        var outcomeText = NormalizeMatchText(outcome.Evidence.EvidenceText);
        var usesOutcomeSupplierName = string.Equals(
            supplierName,
            outcome.AwardedSupplierName,
            StringComparison.Ordinal);

        // 若合并时采用了 outcome 的完整成交人名称，证据链也应指向完整行，避免页面继续展示 PDF/回退解析的短碎片。
        if (usesOutcomeSupplierName &&
            outcomeText.Length > parsedText.Length)
        {
            return outcome.Evidence;
        }

        return parsed.Evidence;
    }

    private static bool AwardEvidenceMatches(AwardEvidence left, AwardEvidence right)
    {
        if (!SupplierNamesCompatible(left.AwardedSupplierName, right.AwardedSupplierName))
        {
            return false;
        }

        var leftPackageNo = NormalizePackageNoForMatch(left.PackageNo ?? left.NormalizedPackageNo);
        var rightPackageNo = NormalizePackageNoForMatch(right.PackageNo ?? right.NormalizedPackageNo);
        if (!string.IsNullOrWhiteSpace(leftPackageNo) && !string.IsNullOrWhiteSpace(rightPackageNo))
            return string.Equals(leftPackageNo, rightPackageNo, StringComparison.Ordinal);

        var leftLotName = NormalizeMatchText(left.LotName);
        var rightLotName = NormalizeMatchText(right.LotName);
        if (!string.IsNullOrWhiteSpace(leftLotName) && !string.IsNullOrWhiteSpace(rightLotName))
            return string.Equals(leftLotName, rightLotName, StringComparison.Ordinal);

        return false;
    }

    private static PackageStaging? MatchReviewPackage(
        OutcomeSupplierRecord record,
        IReadOnlyList<PackageStaging> packages)
    {
        if (packages.Count == 0)
            return null;

        var packageNo = NormalizePackageNoForMatch(record.PackageNo);
        var lotNo = NormalizeMatchText(record.LotNo);
        var lotName = NormalizeMatchText(record.LotName);
        var packageName = NormalizeMatchText(record.PackageName);

        if (!string.IsNullOrWhiteSpace(packageNo))
        {
            var byPackageNo = packages
                .Where(x => NormalizePackageNoForMatch(x.PackageNo) == packageNo)
                .ToList();
            if (!string.IsNullOrWhiteSpace(lotName))
            {
                var byLotName = byPackageNo
                    .Where(x => NormalizeMatchText(x.LotName) == lotName)
                    .ToList();
                if (byLotName.Count == 1)
                    return byLotName[0];
            }

            if (!string.IsNullOrWhiteSpace(lotNo))
            {
                var byLotNo = byPackageNo
                    .Where(x => NormalizeMatchText(x.LotNo) == lotNo)
                    .ToList();
                if (byLotNo.Count == 1)
                    return byLotNo[0];
            }

            if (byPackageNo.Count == 1)
                return byPackageNo[0];
        }

        if (!string.IsNullOrWhiteSpace(packageName))
        {
            var byPackageName = packages
                .Where(x => NormalizeMatchText(x.PackageName) == packageName)
                .ToList();
            if (byPackageName.Count == 1)
                return byPackageName[0];
        }

        return null;
    }

    private static OutcomeSupplierRecord? MatchOutcomeRecordForLifecycleLink(
        LifecyclePackageLinkDto link,
        IEnumerable<OutcomeSupplierRecord> records,
        IReadOnlyList<PackageStaging> packages)
    {
        var allRecords = records.ToList();
        var awardEvidenceText = ExtractLifecycleAwardEvidenceText(link.EvidenceJson);
        if (!string.IsNullOrWhiteSpace(awardEvidenceText))
        {
            var byEvidence = allRecords
                .Where(x => EvidenceTextMatches(awardEvidenceText, x.EvidenceText))
                .ToList();
            if (byEvidence.Count == 1)
                return byEvidence[0];

            if (byEvidence.Count > 1)
            {
                var packageForEvidence = NormalizePackageNoForMatch(link.PackageNo);
                if (!string.IsNullOrWhiteSpace(packageForEvidence))
                {
                    var byEvidencePackage = byEvidence
                        .Where(x => NormalizePackageNoForMatch(x.PackageNo) == packageForEvidence)
                        .ToList();
                    if (byEvidencePackage.Count == 1)
                        return byEvidencePackage[0];
                }
            }
        }

        var packageNo = NormalizePackageNoForMatch(link.PackageNo);
        var lotName = NormalizeMatchText(link.LotName);
        var candidates = allRecords
            .Where(x => SupplierNamesCompatible(link.SupplierName, x.SupplierName))
            .ToList();
        if (candidates.Count == 0)
            return null;

        if (link.AwardOutcomeRecordId.HasValue)
        {
            var byId = candidates
                .Where(x => x.Id == link.AwardOutcomeRecordId.Value)
                .ToList();
            if (byId.Count == 1)
                return byId[0];
        }

        if (!string.IsNullOrWhiteSpace(packageNo))
        {
            var byPackage = candidates
                .Where(x => NormalizePackageNoForMatch(x.PackageNo) == packageNo)
                .ToList();
            if (!string.IsNullOrWhiteSpace(lotName))
            {
                var byLotName = byPackage
                    .Where(x => NormalizeMatchText(x.LotName) == lotName)
                    .ToList();
                if (byLotName.Count == 1)
                    return byLotName[0];
            }

            if (byPackage.Count == 1)
                return byPackage[0];

            var withPackageContext = byPackage
                .Select(x => new { Record = x, Package = MatchReviewPackage(x, packages) })
                .Where(x => x.Package != null)
                .ToList();
            if (withPackageContext.Count == 1)
                return withPackageContext[0].Record;
        }

        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static void EnrichLifecycleLinkFromOutcomeContext(
        LifecyclePackageLinkDto link,
        IEnumerable<OutcomeSupplierRecord> records,
        IReadOnlyList<PackageStaging> packages,
        IReadOnlyList<ProcurementDetailStaging> procurementDetails)
    {
        var recordList = records
            .OrderBy(x => x.ExtractionOrder)
            .ThenBy(x => x.Id)
            .ToList();
        link.AwardOutcomeSuppliers = recordList.Select(MapOutcomeRecord).ToList();
        link.ProcurementDetails = procurementDetails.Select(MapProcurementDetailStaging).ToList();

        var record = MatchOutcomeRecordForLifecycleLink(link, recordList, packages);
        var package = record == null
            ? MatchReviewPackageForLifecycleLink(link, packages)
            : MatchReviewPackage(record, packages);
        var projectCode = ResolveManualProjectCodeForMatch(
            link.ProjectCode,
            link.ManualRemark,
            link.EvidenceJson);
        if (string.IsNullOrWhiteSpace(projectCode))
        {
            projectCode = ResolveProjectCodeForMatch(link.ProjectCode, record?.ProjectCode, record?.LotNo, package?.LotNo, link.LotNo);
        }

        if (!string.IsNullOrWhiteSpace(projectCode))
            link.ProjectCode = Truncate(projectCode, 128);

        link.SupplierName = Truncate(FirstNonEmpty(record?.SupplierName, link.SupplierName), 300);
        link.LotNo = Truncate(FirstNonEmpty(link.LotNo, record?.LotNo, package?.LotNo), 128);
        link.LotName = Truncate(FirstSpecificLotName(link.LotName, record?.LotName, package?.LotName), 300);
        link.PackageNo = Truncate(FirstNonEmpty(link.PackageNo, record?.PackageNo, package?.PackageNo), 128);
        link.PackageName = Truncate(FirstNonEmpty(link.PackageName, record?.PackageName, package?.PackageName), 500);

        if (BidOpsOutcomeRecordPolicy.IsNonAwardSupplierName(link.SupplierName))
        {
            link.FinalAwardAmount = null;
            link.FinalAwardAmountSource = "Missing";
            return;
        }

        var amount = ResolveProcurementPackageAmount(link, record, package, procurementDetails, packages);
        if (amount.HasValue)
        {
            link.ProcurementDetailStagingId ??= amount.Value.ProcurementDetailStagingId;
            link.ProcurementPackageAmount = amount.Value.Amount;
            link.ProcurementPackageAmountSource = amount.Value.Source;
            if (!link.FinalAwardAmount.HasValue)
            {
                link.FinalAwardAmount = amount.Value.Amount;
                link.FinalAwardAmountSource = BidOpsAmountKinds.DefaultedFromProcurementPackageAmount;
            }
        }
    }

    private static PackageStaging? MatchReviewPackageForLifecycleLink(
        LifecyclePackageLinkDto link,
        IReadOnlyList<PackageStaging> packages)
    {
        if (packages.Count == 0)
            return null;

        var packageNo = NormalizePackageNoForMatch(link.PackageNo);
        var lotNo = NormalizeMatchText(link.LotNo);
        var lotName = NormalizeMatchText(link.LotName);
        var packageName = NormalizeMatchText(link.PackageName);

        if (!string.IsNullOrWhiteSpace(packageNo))
        {
            var byPackageNo = packages
                .Where(x => NormalizePackageNoForMatch(x.PackageNo) == packageNo)
                .ToList();

            if (!string.IsNullOrWhiteSpace(lotName) && !IsGenericLotName(lotName))
            {
                var byLotName = byPackageNo
                    .Where(x => NormalizeMatchText(x.LotName) == lotName)
                    .ToList();
                if (byLotName.Count == 1)
                    return byLotName[0];
            }

            if (!string.IsNullOrWhiteSpace(lotNo))
            {
                var byLotNo = byPackageNo
                    .Where(x => NormalizeMatchText(x.LotNo) == lotNo)
                    .ToList();
                if (byLotNo.Count == 1)
                    return byLotNo[0];
            }

            if (!string.IsNullOrWhiteSpace(packageName))
            {
                var byPackageName = byPackageNo
                    .Where(x => NormalizeMatchText(x.PackageName) == packageName)
                    .ToList();
                if (byPackageName.Count == 1)
                    return byPackageName[0];
            }

            if (byPackageNo.Count == 1)
                return byPackageNo[0];
        }

        if (!string.IsNullOrWhiteSpace(packageName))
        {
            var byPackageName = packages
                .Where(x => NormalizeMatchText(x.PackageName) == packageName)
                .ToList();
            if (byPackageName.Count == 1)
                return byPackageName[0];
        }

        return null;
    }

    private static ProcurementPackageAmountMatch? ResolveProcurementPackageAmount(
        LifecyclePackageLinkDto link,
        OutcomeSupplierRecord? record,
        PackageStaging? package,
        IReadOnlyList<ProcurementDetailStaging> procurementDetails,
        IReadOnlyList<PackageStaging> packages)
    {
        var detailMatches = MatchProcurementDetailsForLifecycleLink(link, record, package, procurementDetails);
        if (TryResolveAmountFromProcurementDetails(detailMatches, out var detailAmount))
            return detailAmount;

        package ??= MatchReviewPackageForLifecycleLink(link, packages);
        if (package != null && TryResolveAmountFromPackage(package, out var packageAmount))
            return packageAmount;

        return ExtractProcurementPackageAmountFromEvidence(link.EvidenceJson);
    }

    private static IReadOnlyList<ProcurementDetailStaging> MatchProcurementDetailsForLifecycleLink(
        LifecyclePackageLinkDto link,
        OutcomeSupplierRecord? record,
        PackageStaging? package,
        IReadOnlyList<ProcurementDetailStaging> procurementDetails)
    {
        if (procurementDetails.Count == 0)
            return [];

        var packageNo = NormalizePackageNoForMatch(FirstNonEmpty(package?.PackageNo, record?.PackageNo, link.PackageNo));
        var lotNames = new[]
            {
                link.LotName,
                record?.LotName,
                package?.LotName
            }
            .Select(NormalizeMatchText)
            .Where(x => !string.IsNullOrWhiteSpace(x) && !IsGenericLotName(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var lotNos = new[]
            {
                link.LotNo,
                record?.LotNo,
                package?.LotNo
            }
            .Select(NormalizeMatchText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var packageNames = new[]
            {
                link.PackageName,
                record?.PackageName,
                package?.PackageName
            }
            .Select(NormalizeMatchText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var baseMatches = procurementDetails
            .Where(detail =>
                (package != null && detail.PackageStagingId == package.Id) ||
                (!string.IsNullOrWhiteSpace(packageNo) &&
                 NormalizePackageNoForMatch(detail.PackageNo) == packageNo))
            .ToList();
        if (baseMatches.Count == 0)
            return [];

        if (lotNames.Length > 0)
        {
            var byLotName = baseMatches
                .Where(detail => lotNames.Contains(NormalizeMatchText(detail.LotName)) ||
                                 lotNames.Contains(NormalizeMatchText(detail.EcpLotName)))
                .ToList();
            if (byLotName.Count > 0)
                return byLotName;
        }

        if (lotNos.Length > 0)
        {
            var byLotNo = baseMatches
                .Where(detail => lotNos.Contains(NormalizeMatchText(detail.LotNo)))
                .ToList();
            if (byLotNo.Count > 0)
                return byLotNo;
        }

        if (packageNames.Length > 0)
        {
            var byPackageName = baseMatches
                .Where(detail => packageNames.Contains(NormalizeMatchText(detail.PackageName)))
                .ToList();
            if (byPackageName.Count > 0)
                return byPackageName;
        }

        return baseMatches.Count == 1 ? baseMatches : [];
    }

    private static bool TryResolveAmountFromProcurementDetails(
        IReadOnlyList<ProcurementDetailStaging> details,
        out ProcurementPackageAmountMatch amount)
    {
        amount = default;
        if (details.Count == 0)
            return false;

        return TryResolveUniqueAmount(
                   details,
                   detail => detail.PackageEstimatedAmount,
                   "ProcurementDetailStaging.PackageEstimatedAmount",
                   detail => detail.Id,
                   out amount) ||
               TryResolveUniqueAmount(
                   details,
                   detail => detail.BudgetAmount,
                   "ProcurementDetailStaging.BudgetAmount",
                   detail => detail.Id,
                   out amount) ||
               TryResolveUniqueAmount(
                   details,
                   detail => detail.MaxPrice,
                   "ProcurementDetailStaging.MaxPrice",
                   detail => detail.Id,
                   out amount) ||
               TryResolveUniqueAmount(
                   details,
                   detail => detail.ProcurementAmount,
                   "ProcurementDetailStaging.ProcurementAmount",
                   detail => detail.Id,
                   out amount) ||
               TryResolveUniqueAmount(
                   details,
                   detail => detail.ItemEstimatedAmount,
                   "ProcurementDetailStaging.ItemEstimatedAmount",
                   detail => detail.Id,
                   out amount);
    }

    private static bool TryResolveAmountFromPackage(
        PackageStaging package,
        out ProcurementPackageAmountMatch amount)
    {
        amount = default;
        if (package.BudgetAmount is > 0)
        {
            amount = new ProcurementPackageAmountMatch(package.BudgetAmount.Value, "PackageStaging.BudgetAmount", null);
            return true;
        }

        if (package.MaxPrice is > 0)
        {
            amount = new ProcurementPackageAmountMatch(package.MaxPrice.Value, "PackageStaging.MaxPrice", null);
            return true;
        }

        return false;
    }

    private static bool TryResolveUniqueAmount<T>(
        IReadOnlyList<T> items,
        Func<T, decimal?> selector,
        string source,
        Func<T, long?> idSelector,
        out ProcurementPackageAmountMatch amount)
    {
        amount = default;
        var amounts = items
            .Select(item => new { Item = item, Amount = selector(item) })
            .Where(x => x.Amount is > 0)
            .ToList();
        var distinctAmounts = amounts
            .Select(x => Math.Round(x.Amount!.Value, 2))
            .Distinct()
            .ToList();
        if (distinctAmounts.Count != 1)
            return false;

        amount = new ProcurementPackageAmountMatch(
            distinctAmounts[0],
            source,
            amounts.Select(x => idSelector(x.Item)).FirstOrDefault(x => x.HasValue));
        return true;
    }

    private static ProcurementPackageAmountMatch? ExtractProcurementPackageAmountFromEvidence(string? evidenceJson)
    {
        if (string.IsNullOrWhiteSpace(evidenceJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(evidenceJson);
            if (!TryGetProperty(document.RootElement, "tender", out var tender) ||
                tender.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (TryReadPositiveDecimal(tender, "guidePrice", out var guidePrice))
                return new ProcurementPackageAmountMatch(guidePrice, "TenderEvidence.GuidePrice", null);
            if (TryReadPositiveDecimal(tender, "budgetAmount", out var budgetAmount))
                return new ProcurementPackageAmountMatch(budgetAmount, "TenderEvidence.BudgetAmount", null);
            if (TryReadPositiveDecimal(tender, "maxPrice", out var maxPrice))
                return new ProcurementPackageAmountMatch(maxPrice, "TenderEvidence.MaxPrice", null);
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool TryReadPositiveDecimal(JsonElement element, string propertyName, out decimal value)
    {
        value = default;
        if (!TryGetProperty(element, propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.Number &&
            property.TryGetDecimal(out value) &&
            value > 0)
        {
            value = Math.Round(value, 2);
            return true;
        }

        if (property.ValueKind == JsonValueKind.String &&
            decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value) &&
            value > 0)
        {
            value = Math.Round(value, 2);
            return true;
        }

        return false;
    }

    private readonly record struct ProcurementPackageAmountMatch(
        decimal Amount,
        string Source,
        long? ProcurementDetailStagingId);

    private static string? FirstSpecificLotName(params string?[] values)
    {
        string? genericFallback = null;
        foreach (var value in values)
        {
            var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
            if (string.IsNullOrWhiteSpace(cleaned))
                continue;

            if (IsGenericLotName(cleaned))
            {
                genericFallback ??= cleaned;
                continue;
            }

            return cleaned;
        }

        return genericFallback;
    }

    private static bool IsGenericLotName(string value)
    {
        var normalized = NormalizeMatchText(value);
        return normalized is "未分标段" or "不分标段" or "未分标" or "不分标" or "无分标";
    }

    private static string ExtractLifecycleAwardEvidenceText(string? evidenceJson)
    {
        if (string.IsNullOrWhiteSpace(evidenceJson))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(evidenceJson);
            return FirstNonEmpty(
                       ReadJsonString(document.RootElement, "award", "evidence", "evidenceText"),
                       ReadJsonString(document.RootElement, "amount", "evidenceText")) ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string? ReadJsonString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(current, segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
            return true;

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool EvidenceTextMatches(string left, string right)
    {
        var normalizedLeft = NormalizeMatchText(left);
        var normalizedRight = NormalizeMatchText(right);
        return !string.IsNullOrWhiteSpace(normalizedLeft) &&
               !string.IsNullOrWhiteSpace(normalizedRight) &&
               string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal);
    }

    private static string NormalizePackageNoForMatch(string? value)
    {
        return BidOpsPackageNoNormalizer.Normalize(value);
    }

    private static string NormalizeMatchText(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        return new string(cleaned.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
            if (!string.IsNullOrWhiteSpace(cleaned))
                return cleaned;
        }

        return null;
    }

    private static string Truncate(string? value, int maxLength)
    {
        value = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string?[] Prepend(string? value, IEnumerable<string?> values)
    {
        return new[] { value }.Concat(values).ToArray();
    }
}
