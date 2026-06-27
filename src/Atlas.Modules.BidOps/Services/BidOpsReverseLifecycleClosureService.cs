using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _jobs;
    private readonly ICurrentIdentity _current;
    private readonly IIdGenerator _idGenerator;
    private readonly IBidOpsFileStore _fileStore;
    private readonly IBidOpsCrawlService _crawl;
    private readonly IStateGridEcpCrawler _stateGridCrawler;
    private readonly IBidOpsRuntimeControlService _runtimeControl;
    private readonly IBidOpsLifecycleFieldEnrichmentAiService _fieldEnrichmentAi;
    private readonly BidOpsContentHasher _hasher;
    private readonly ILogger<BidOpsReverseLifecycleClosureService> _logger;

    public BidOpsReverseLifecycleClosureService(
        IRepository<RawNotice> rawNotices,
        IRepository<RawAttachment> rawAttachments,
        IRepository<LifecyclePackageLink> lifecycleLinks,
        IRepository<OutcomeSupplierRecord> outcomeRecords,
        IRepository<ReviewTask> reviewTasks,
        IRepository<PackageStaging> packageStaging,
        IUnitOfWork unitOfWork,
        IBackgroundJobClient jobs,
        ICurrentIdentity current,
        IIdGenerator idGenerator,
        IBidOpsFileStore fileStore,
        IBidOpsCrawlService crawl,
        IStateGridEcpCrawler stateGridCrawler,
        IBidOpsRuntimeControlService runtimeControl,
        IBidOpsLifecycleFieldEnrichmentAiService fieldEnrichmentAi,
        BidOpsContentHasher hasher,
        ILogger<BidOpsReverseLifecycleClosureService> logger)
    {
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _rawAttachments = rawAttachments ?? throw new ArgumentNullException(nameof(rawAttachments));
        _lifecycleLinks = lifecycleLinks ?? throw new ArgumentNullException(nameof(lifecycleLinks));
        _outcomeRecords = outcomeRecords ?? throw new ArgumentNullException(nameof(outcomeRecords));
        _reviewTasks = reviewTasks ?? throw new ArgumentNullException(nameof(reviewTasks));
        _packageStaging = packageStaging ?? throw new ArgumentNullException(nameof(packageStaging));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _current = current ?? throw new ArgumentNullException(nameof(current));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _crawl = crawl ?? throw new ArgumentNullException(nameof(crawl));
        _stateGridCrawler = stateGridCrawler ?? throw new ArgumentNullException(nameof(stateGridCrawler));
        _runtimeControl = runtimeControl ?? throw new ArgumentNullException(nameof(runtimeControl));
        _fieldEnrichmentAi = fieldEnrichmentAi ?? throw new ArgumentNullException(nameof(fieldEnrichmentAi));
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
            builder = builder.Where(x => x.ProjectCode.Contains(projectCode));
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
        }

        var total = await builder.CountAsync(ct);
        if (query.RawNoticeId.HasValue &&
            RequiresLifecycleDisplayContextSort(query.SortBy) &&
            total <= MaxDisplayContextSortRows)
        {
            var allLinks = await builder.ToListAsync(ct);
            var allItems = allLinks.Select(MapLifecycleLink).ToList();
            await EnrichLifecycleLinkDtosFromOutcomeContextAsync(allItems, ct);
            var pageItems = SortLifecycleLinkDtosByDisplayContext(allItems, query.SortBy)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            await EnrichLifecycleNoticeRefsAsync(pageItems, ct);

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
        await EnrichLifecycleLinkDtosFromOutcomeContextAsync(items, ct);
        await EnrichLifecycleNoticeRefsAsync(items, ct);

        return new PagedResult<LifecyclePackageLinkDto>(
            total,
            items,
            pageIndex,
            pageSize);
    }

    public async Task<IReadOnlyList<LifecycleProcurementNoticeCandidateDto>> SearchProcurementNoticeCandidatesAsync(
        long linkId,
        CancellationToken ct = default)
    {
        var link = await LoadLifecycleLinkForReadAsync(linkId, ct);
        var projectCode = BidOpsTextQuality.CleanExtractedValue(link.ProjectCode);
        if (string.IsNullOrWhiteSpace(projectCode))
            throw new AtlasException("Lifecycle link does not have a project/procurement code for State Grid search.");

        var candidates = await _stateGridCrawler.SearchPublicNoticesAsync(
            new StateGridEcpNoticeSearchRequest(projectCode, PageSize: 20),
            ct);
        var procurementCandidates = candidates
            .Where(IsProcurementNoticeCandidate)
            .ToList();
        var rawByUrlHash = await LoadRawNoticesByUrlHashAsync(procurementCandidates.Select(x => x.DetailUrl), ct);

        return procurementCandidates
            .Select(candidate => MapProcurementNoticeCandidate(candidate, projectCode, rawByUrlHash))
            .OrderByDescending(x => x.IsExactProjectCodeMatch)
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
        var linkProjectCode = BidOpsTextQuality.CleanExtractedValue(link.ProjectCode);
        if (string.IsNullOrWhiteSpace(linkProjectCode))
            throw new AtlasException("Lifecycle link does not have a project/procurement code.");

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
            link.ProcurementRawNoticeId = existing.Id;
            link.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);
            return new LifecycleProcurementNoticeImportResultDto
            {
                RawNoticeId = existing.Id,
                Message = "Procurement notice already exists locally and was linked to the lifecycle record."
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

        return new LifecycleProcurementNoticeImportResultDto
        {
            ImportJob = job,
            Message = $"Procurement notice import queued as job {job.JobId}."
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
                    persistLinks),
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
        link.LinkStatus = BidOpsLifecycleLinkStatuses.Confirmed;
        link.RequiresManualReview = request.RequiresManualReview ?? false;
        if (request.FinalAwardAmount.HasValue)
            link.FinalAwardAmount = request.FinalAwardAmount.Value;
        if (!string.IsNullOrWhiteSpace(request.FinalAwardAmountSource))
            link.FinalAwardAmountSource = Truncate(request.FinalAwardAmountSource, 128);
        if (!link.ProcurementRawNoticeId.HasValue)
        {
            var procurementRaw = await FindProcurementNoticeCandidateAsync(link.ProjectCode, link.AwardRawNoticeId, ct);
            if (procurementRaw != null)
                link.ProcurementRawNoticeId = procurementRaw.Id;
        }

        link.ManualRemark = Truncate(request.Remark, 1000);
        link.ConfirmedBy = _current.UserId;
        link.ConfirmedAt = DateTime.UtcNow;
        link.UpdatedAt = link.ConfirmedAt;
        await _unitOfWork.SaveChangesAsync(ct);
        var dto = MapLifecycleLink(link);
        await EnrichLifecycleLinkDtosFromOutcomeContextAsync([dto], ct);
        await EnrichLifecycleNoticeRefsAsync([dto], ct);
        return dto;
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
        await EnrichLifecycleLinkDtosFromOutcomeContextAsync([dto], ct);
        await EnrichLifecycleNoticeRefsAsync([dto], ct);
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
        IReadOnlyList<PackageStaging> packages)
    {
        EnrichLifecycleLinkFromOutcomeContext(link, records, packages);
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
        await EnrichLifecycleLinkDtosFromOutcomeContextAsync([dtoBefore], ct);
        var evidence = await BuildLifecycleFieldEvidenceInputsAsync(link, ct);
        var result = await _fieldEnrichmentAi.EnrichAsync(
            new BidOpsLifecycleFieldEnrichmentRequest(
                link.Id,
                FirstNonEmpty(dtoBefore.ProjectCode, link.ProjectCode) ?? string.Empty,
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
        await EnrichLifecycleLinkDtosFromOutcomeContextAsync([dto], ct);
        await EnrichLifecycleNoticeRefsAsync([dto], ct);
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
        var query = await _lifecycleLinks.QueryDataScopeTrackingAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);
        var existingLinks = await query
            .Where(x => hashes.Contains(x.SourceHash))
            .ToListAsync(ct);
        var existingByHash = existingLinks.ToDictionary(x => x.SourceHash, StringComparer.OrdinalIgnoreCase);
        var savedLinks = new List<LifecyclePackageLink>();
        foreach (var draft in drafts)
        {
            if (!existingByHash.TryGetValue(draft.SourceHash, out var link))
            {
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
        link.UpdatedAt = DateTime.UtcNow;
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
                    if (!link.FinalAwardAmount.HasValue && field.NumericValue.HasValue)
                    {
                        link.FinalAwardAmount = Math.Round(field.NumericValue.Value, 2);
                        link.FinalAwardAmountSource = Truncate(ResolveAmountSource(field), 128);
                    }
                    break;
                case "finalawardamountsource":
                    if (string.IsNullOrWhiteSpace(link.FinalAwardAmountSource))
                        link.FinalAwardAmountSource = Truncate(value, 128);
                    break;
            }
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
            catch (JsonException)
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
        IReadOnlyDictionary<string, RawNotice> rawByUrlHash)
    {
        rawByUrlHash.TryGetValue(_hasher.HashUrl(candidate.DetailUrl), out var existingRaw);
        return new LifecycleProcurementNoticeCandidateDto
        {
            SourceId = candidate.SourceId,
            ChannelId = candidate.ChannelId,
            NoticeType = candidate.NoticeType,
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
            IsExactProjectCodeMatch = ProjectCodeTextMatches(candidate.ProjectCode, projectCode) ||
                                      (!string.IsNullOrWhiteSpace(candidate.Title) &&
                                       candidate.Title.Contains(projectCode, StringComparison.OrdinalIgnoreCase))
        };
    }

    private static LifecyclePackageLinkDto MapLifecycleLink(LifecyclePackageLink link)
    {
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
            FinalAwardAmount = link.FinalAwardAmount,
            FinalAwardAmountSource = link.FinalAwardAmountSource,
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

    private async Task EnrichLifecycleLinkDtosFromOutcomeContextAsync(
        IReadOnlyList<LifecyclePackageLinkDto> links,
        CancellationToken ct)
    {
        var rawIds = links
            .Select(x => x.AwardRawNoticeId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        if (rawIds.Length == 0)
            return;

        var outcomeQuery = await _outcomeRecords.QueryDataScopeAsync(
            BidOpsDataResources.OutcomeSupplierRecord,
            AtlasDataScopeType.AllTenant,
            ct);
        var outcomeRecords = await outcomeQuery
            .Where(x => rawIds.Contains(x.RawNoticeId))
            .ToListAsync(ct);
        if (outcomeRecords.Count == 0)
            return;

        var packagesByRawNotice = await LoadReviewPackagesByRawNoticeAsync(rawIds, ct);
        foreach (var link in links)
        {
            if (!link.AwardRawNoticeId.HasValue)
                continue;

            var packages = packagesByRawNotice.TryGetValue(link.AwardRawNoticeId.Value, out var rawPackages)
                ? rawPackages
                : [];
            EnrichLifecycleLinkFromOutcomeContext(
                link,
                outcomeRecords.Where(x => x.RawNoticeId == link.AwardRawNoticeId.Value),
                packages);
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

    private async Task EnrichLifecycleNoticeRefsAsync(
        IReadOnlyList<LifecyclePackageLinkDto> links,
        CancellationToken ct)
    {
        if (links.Count == 0)
            return;

        var inferredProcurementByLinkId = new Dictionary<long, RawNotice>();
        foreach (var link in links.Where(x => !x.ProcurementRawNoticeId.HasValue))
        {
            var inferred = await FindProcurementNoticeCandidateAsync(link.ProjectCode, link.AwardRawNoticeId, ct);
            if (inferred != null)
                inferredProcurementByLinkId[link.Id] = inferred;
        }

        var rawIds = links
            .SelectMany(x => new[] { x.ProcurementRawNoticeId, x.CandidateRawNoticeId, x.AwardRawNoticeId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Concat(inferredProcurementByLinkId.Values.Select(x => x.Id))
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
            else if (inferredProcurementByLinkId.TryGetValue(link.Id, out procurementRaw))
            {
                link.ProcurementRawNoticeId = procurementRaw.Id;
                procurementMatchSource = "InferredByProjectCode";
            }

            if (procurementRaw != null)
            {
                link.ProcurementNotice = MapLifecycleNoticeRef(procurementRaw, procurementMatchSource);
                link.ProcurementAttachments = attachmentsByRawId.GetValueOrDefault(procurementRaw.Id)?.ToList() ?? [];
                link.ProcurementNoticeMissingReason = string.Empty;
            }
            else
            {
                link.ProcurementNoticeMissingReason = ProcurementNoticeMissingReason(link.ProjectCode);
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
        var code = BidOpsTextQuality.CleanExtractedValue(projectCode);
        if (string.IsNullOrWhiteSpace(code))
            return null;

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
        return candidates
            .Where(LooksLikeTenderNotice)
            .OrderByDescending(x => x.PublishTime ?? x.FetchTime)
            .FirstOrDefault();
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

        var relatedRaw = await LoadRelatedRawNoticesAsync(awardRaw, awardEvidence, ct);
        var candidateDocuments = new List<(RawNotice Raw, IReadOnlyList<BidOpsEvidenceDocument> Documents)>();
        var tenderDocuments = new List<(RawNotice Raw, IReadOnlyList<BidOpsEvidenceDocument> Documents)>();
        foreach (var raw in relatedRaw)
        {
            if (LooksLikeCandidateNotice(raw))
            {
                var documents = await ReadEvidenceDocumentsAsync(raw, ct);
                var match = BidOpsNoticeCorrelationService.Match(raw, documents, awardEvidence, "Candidate", awardRaw.PublishTime);
                if (match.Confidence > 0 || match.MissingReason == null)
                    result.CandidateNoticeMatches.Add(match);
                if (match.Confidence >= 0.45)
                    candidateDocuments.Add((raw, documents));
            }
            else if (LooksLikeTenderNotice(raw))
            {
                var documents = await ReadEvidenceDocumentsAsync(raw, ct);
                var match = BidOpsNoticeCorrelationService.Match(raw, documents, awardEvidence, "Tender", awardRaw.PublishTime);
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

        result.Closures.AddRange(BuildClosures(awardEvidence, candidateEvidence, tenderEvidence));
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
            var code = projectCode.Trim();
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
        var projectCodes = awards.Select(x => x.ProjectCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var projectNames = awards.Select(x => x.ProjectName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var packageNos = awards.Select(x => x.NormalizedPackageNo).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var noticeProjectCode = BidOpsEvidenceText.ExtractProjectCode(text);
        if (!string.IsNullOrWhiteSpace(noticeProjectCode) &&
            projectCodes.Any(x => string.Equals(x, noticeProjectCode, StringComparison.OrdinalIgnoreCase)))
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
            return string.Equals(awardProjectCode, otherProjectCode, StringComparison.OrdinalIgnoreCase);
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

        return ContainsAny(signal, "Tender", "Procurement", "招标公告", "采购公告", "公开谈判采购", "竞争性谈判采购", "询价采购");
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

        return string.Equals(menuId, "2018032900295987", StringComparison.OrdinalIgnoreCase)
            ? "ProcurementAnnouncement"
            : "TenderAnnouncement";
    }

    private static bool ProjectCodeTextMatches(string? left, string? right)
    {
        var normalizedLeft = BidOpsTextQuality.CleanExtractedValue(left);
        var normalizedRight = BidOpsTextQuality.CleanExtractedValue(right);
        return !string.IsNullOrWhiteSpace(normalizedLeft) &&
               !string.IsNullOrWhiteSpace(normalizedRight) &&
               string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
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

    private static string ProcurementNoticeMissingReason(string projectCode)
    {
        return string.IsNullOrWhiteSpace(projectCode)
            ? "未匹配到采购公告 RawNotice；当前闭环建议缺少采购编号，无法按采购编号反查采购公告。"
            : $"未匹配到采购公告 RawNotice；请先采集或导入采购编号 {projectCode} 对应的采购公告。";
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
            if (!matchedParsed.Contains(i))
                merged.Add(parsed[i]);
        }

        return merged;
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
                return new AwardEvidence(
                    FirstNonEmpty(record.ProjectCode, raw.SourceNoticeId),
                    FirstNonEmpty(record.ProjectName, raw.Title),
                    null,
                    lotNo,
                    lotName,
                    packageNo,
                    BidOpsPackageNoNormalizer.Normalize(packageNo),
                    packageName,
                    record.SupplierName,
                    record.AwardAmount,
                    record.AwardAmount.HasValue ? BidOpsAmountKinds.DirectAwardAmount : "Missing",
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
            AwardAmount = parsed.AwardAmount ?? outcome.AwardAmount,
            AmountSource = parsed.AwardAmount.HasValue ? parsed.AmountSource : outcome.AmountSource,
            Confidence = Math.Max(parsed.Confidence, outcome.Confidence)
        };
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
        IReadOnlyList<PackageStaging> packages)
    {
        var record = MatchOutcomeRecordForLifecycleLink(link, records, packages);
        if (record == null)
            return;

        var package = MatchReviewPackage(record, packages);
        link.SupplierName = Truncate(FirstNonEmpty(record.SupplierName, link.SupplierName), 300);
        link.LotNo = Truncate(FirstNonEmpty(link.LotNo, record.LotNo, package?.LotNo), 128);
        link.LotName = Truncate(FirstNonEmpty(link.LotName, record.LotName, package?.LotName), 300);
        link.PackageNo = Truncate(FirstNonEmpty(link.PackageNo, record.PackageNo, package?.PackageNo), 128);
        link.PackageName = Truncate(FirstNonEmpty(link.PackageName, record.PackageName, package?.PackageName), 500);
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
