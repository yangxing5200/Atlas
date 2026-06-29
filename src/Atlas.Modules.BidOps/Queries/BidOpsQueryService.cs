using System.Text;
using System.Text.Json;
using Atlas.Core.Authorization;
using Atlas.Data.Abstractions;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Buyers;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Queries;

public sealed class BidOpsQueryService : IBidOpsQueryService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;
    private const int RawTextContentMaxCharacters = 120_000;
    private const int AttachmentTextContentMaxCharacters = 120_000;
    private const int OutcomePreviewTextMaxCharacters = 300_000;
    private const string PipelineStatusCompleted = "Completed";
    private const string PipelineStatusPending = "Pending";
    private const string PipelineStatusFailed = "Failed";
    private const string PipelineStatusSkipped = "Skipped";

    private readonly IRepository<CrawlSource> _sources;
    private readonly IRepository<CrawlChannel> _channels;
    private readonly IRepository<CrawlRunLog> _runLogs;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<RawAttachment> _rawAttachments;
    private readonly IRepository<ReviewTask> _reviewTasks;
    private readonly IRepository<NoticeStaging> _noticeStaging;
    private readonly IRepository<PackageStaging> _packageStaging;
    private readonly IRepository<RequirementStaging> _requirementStaging;
    private readonly IRepository<ReviewQualityIssue> _reviewQualityIssues;
    private readonly IRepository<ReviewCorrectionSample> _reviewCorrectionSamples;
    private readonly IRepository<Buyer> _buyers;
    private readonly IRepository<OutcomeSupplierRecord> _outcomeRecords;
    private readonly IRepository<LifecyclePackageLink> _lifecycleLinks;
    private readonly IRepository<Notice> _notices;
    private readonly IRepository<TenderPackage> _packages;
    private readonly IRepository<ProcurementDetailStaging> _procurementDetailStaging;
    private readonly IRepository<ProcurementDetail> _procurementDetails;
    private readonly IRepository<RequirementItem> _requirements;
    private readonly IBidOpsFileStore _fileStore;
    private readonly ILogger<BidOpsQueryService> _logger;

    public BidOpsQueryService(
        IRepository<CrawlSource> sources,
        IRepository<CrawlChannel> channels,
        IRepository<CrawlRunLog> runLogs,
        IRepository<RawNotice> rawNotices,
        IRepository<RawAttachment> rawAttachments,
        IRepository<ReviewTask> reviewTasks,
        IRepository<NoticeStaging> noticeStaging,
        IRepository<PackageStaging> packageStaging,
        IRepository<RequirementStaging> requirementStaging,
        IRepository<ReviewQualityIssue> reviewQualityIssues,
        IRepository<ReviewCorrectionSample> reviewCorrectionSamples,
        IRepository<Buyer> buyers,
        IRepository<OutcomeSupplierRecord> outcomeRecords,
        IRepository<LifecyclePackageLink> lifecycleLinks,
        IRepository<Notice> notices,
        IRepository<TenderPackage> packages,
        IRepository<ProcurementDetailStaging> procurementDetailStaging,
        IRepository<ProcurementDetail> procurementDetails,
        IRepository<RequirementItem> requirements,
        IBidOpsFileStore fileStore,
        ILogger<BidOpsQueryService> logger)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _runLogs = runLogs ?? throw new ArgumentNullException(nameof(runLogs));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _rawAttachments = rawAttachments ?? throw new ArgumentNullException(nameof(rawAttachments));
        _reviewTasks = reviewTasks ?? throw new ArgumentNullException(nameof(reviewTasks));
        _noticeStaging = noticeStaging ?? throw new ArgumentNullException(nameof(noticeStaging));
        _packageStaging = packageStaging ?? throw new ArgumentNullException(nameof(packageStaging));
        _requirementStaging = requirementStaging ?? throw new ArgumentNullException(nameof(requirementStaging));
        _reviewQualityIssues = reviewQualityIssues ?? throw new ArgumentNullException(nameof(reviewQualityIssues));
        _reviewCorrectionSamples = reviewCorrectionSamples ?? throw new ArgumentNullException(nameof(reviewCorrectionSamples));
        _buyers = buyers ?? throw new ArgumentNullException(nameof(buyers));
        _outcomeRecords = outcomeRecords ?? throw new ArgumentNullException(nameof(outcomeRecords));
        _lifecycleLinks = lifecycleLinks ?? throw new ArgumentNullException(nameof(lifecycleLinks));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _procurementDetailStaging = procurementDetailStaging ?? throw new ArgumentNullException(nameof(procurementDetailStaging));
        _procurementDetails = procurementDetails ?? throw new ArgumentNullException(nameof(procurementDetails));
        _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<CrawlSourceDto>> SearchSourcesAsync(BidOpsPagedQuery query, CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _sources.QueryDataScopeAsync(BidOpsDataResources.CrawlSource, AtlasDataScopeType.AllTenant, ct);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword));
        }

        var total = await builder.CountAsync(ct);
        var items = await builder
            .OrderBy(x => x.Code)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .SelectToListAsync(x => new CrawlSourceDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                SourceType = x.SourceType,
                BaseUrl = x.BaseUrl,
                Enabled = x.Enabled,
                RateLimitPerMinute = x.RateLimitPerMinute,
                CrawlIntervalMinutes = x.CrawlIntervalMinutes,
                MaxRetryCount = x.MaxRetryCount,
                NeedLogin = x.NeedLogin,
                RespectRobots = x.RespectRobots,
                RobotsPolicyNote = x.RobotsPolicyNote,
                PauseReason = x.PauseReason
            }, ct);

        return new PagedResult<CrawlSourceDto>(total, items, pageIndex, pageSize);
    }

    public async Task<PagedResult<CrawlChannelDto>> SearchChannelsAsync(BidOpsPagedQuery query, CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _channels.QueryDataScopeAsync(BidOpsDataResources.CrawlSource, AtlasDataScopeType.AllTenant, ct);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword));
        }

        var total = await builder.CountAsync(ct);
        var items = await builder
            .OrderBy(x => x.Code)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .SelectToListAsync(x => new CrawlChannelDto
            {
                Id = x.Id,
                SourceId = x.SourceId,
                Code = x.Code,
                Name = x.Name,
                NoticeType = x.NoticeType,
                ListUrl = x.ListUrl,
                Region = x.Region,
                Industry = x.Industry,
                Enabled = x.Enabled,
                ScheduleMode = x.ScheduleMode,
                ScanIntervalMinutes = x.ScanIntervalMinutes,
                DailyScanTime = x.DailyScanTime,
                LastScanTime = x.LastScanTime,
                LastSuccessTime = x.LastSuccessTime,
                LastError = x.LastError
            }, ct);

        return new PagedResult<CrawlChannelDto>(total, items, pageIndex, pageSize);
    }

    public async Task<PagedResult<CrawlRunLogDto>> SearchCrawlRunLogsAsync(
        CrawlRunLogSearchQuery query,
        CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _runLogs.QueryDataScopeAsync(BidOpsDataResources.CrawlRunLog, AtlasDataScopeType.AllTenant, ct);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x =>
                x.Operation.Contains(keyword) ||
                x.Status.Contains(keyword) ||
                x.Message.Contains(keyword));
        }

        if (query.SourceId.HasValue)
            builder = builder.Where(x => x.SourceId == query.SourceId.Value);

        if (query.ChannelId.HasValue)
            builder = builder.Where(x => x.ChannelId == query.ChannelId.Value);

        if (query.BackgroundJobId.HasValue)
            builder = builder.Where(x => x.BackgroundJobId == query.BackgroundJobId.Value);

        if (!string.IsNullOrWhiteSpace(query.Operation))
        {
            var operation = query.Operation.Trim();
            builder = builder.Where(x => x.Operation == operation);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            builder = builder.Where(x => x.Status == status);
        }

        var total = await builder.CountAsync(ct);
        var items = await builder
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .SelectToListAsync(x => new CrawlRunLogDto
            {
                Id = x.Id,
                SourceId = x.SourceId,
                ChannelId = x.ChannelId,
                BackgroundJobId = x.BackgroundJobId,
                Operation = x.Operation,
                Status = x.Status,
                Message = x.Message,
                DurationMs = x.DurationMs,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }, ct);

        return new PagedResult<CrawlRunLogDto>(total, items, pageIndex, pageSize);
    }

    public async Task<CrawlRunLogDto?> GetCrawlRunLogAsync(long id, CancellationToken ct = default)
    {
        var builder = await _runLogs.QueryDataScopeAsync(BidOpsDataResources.CrawlRunLog, AtlasDataScopeType.AllTenant, ct);
        var log = await builder.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
        return log == null ? null : MapCrawlRunLog(log);
    }

    public async Task<PagedResult<RawNoticeDto>> SearchRawNoticesAsync(RawNoticeSearchQuery query, CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _rawNotices.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x => x.Title.Contains(keyword) || x.DetailUrl.Contains(keyword));
        }

        if (query.Status.HasValue)
            builder = builder.Where(x => x.Status == query.Status.Value);

        var total = await builder.CountAsync(ct);
        var items = await builder
            .OrderByDescending(x => x.FetchTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .SelectToListAsync(x => new RawNoticeDto
            {
                Id = x.Id,
                SourceId = x.SourceId,
                ChannelId = x.ChannelId,
                Title = x.Title,
                DetailUrl = x.DetailUrl,
                NoticeType = x.NoticeType,
                PublishTime = x.PublishTime,
                FetchTime = x.FetchTime,
                ContentHash = x.ContentHash,
                TextPreview = x.TextPreview,
                Status = x.Status,
                LastError = x.LastError,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }, ct);

        return new PagedResult<RawNoticeDto>(total, items, pageIndex, pageSize);
    }

    public async Task<RawNoticeDto?> GetRawNoticeAsync(long id, CancellationToken ct = default)
    {
        var raw = await _rawNotices.GetByIdAsync(id, ct);
        return raw == null ? null : Map(raw);
    }

    public async Task<RawNoticePipelineDto?> GetRawNoticePipelineAsync(long id, CancellationToken ct = default)
    {
        var raw = await _rawNotices.GetByIdAsync(id, ct);
        if (raw == null)
            return null;

        var attachments = await ListRawAttachmentEntitiesAsync(raw.Id, ct);
        var noticeStaging = await GetNoticeStagingByRawNoticeAsync(raw.Id, ct);
        List<PackageStaging> packageStagings = noticeStaging == null
            ? []
            : await ListPackageStagingsAsync(noticeStaging.Id, ct);
        List<RequirementStaging> requirementStagings = packageStagings.Count == 0
            ? []
            : await ListRequirementStagingsAsync(packageStagings.Select(x => x.Id).ToArray(), ct);
        var reviewTask = await GetLatestReviewTaskForRawNoticeAsync(raw.Id, noticeStaging?.Id, ct);
        var notice = await GetNoticeByRawNoticeAsync(raw.Id, ct);
        List<TenderPackage> formalPackages = notice == null
            ? []
            : await ListTenderPackagesByNoticeAsync(notice.Id, ct);
        List<RequirementItem> formalRequirements = formalPackages.Count == 0
            ? []
            : await ListRequirementItemsByPackageIdsAsync(formalPackages.Select(x => x.Id).ToArray(), ct);

        var packageCount = formalPackages.Count > 0 ? formalPackages.Count : packageStagings.Count;
        var requirementCount = formalRequirements.Count > 0 ? formalRequirements.Count : requirementStagings.Count;

        return new RawNoticePipelineDto
        {
            RawNoticeId = raw.Id,
            Title = raw.Title,
            RawStatus = raw.Status,
            FetchTime = raw.FetchTime,
            DetailUrl = raw.DetailUrl,
            AttachmentCount = attachments.Count,
            AttachmentDownloadedCount = attachments.Count(x => x.DownloadStatus == DownloadStatus.Succeeded),
            AttachmentTextExtractedCount = attachments.Count(x => x.TextExtractStatus == TextExtractStatus.Succeeded),
            ReviewTaskId = reviewTask?.Id,
            ReviewTaskStatus = reviewTask?.Status,
            NoticeStagingId = noticeStaging?.Id,
            NoticeStagingStatus = noticeStaging?.ReviewStatus,
            NoticeId = notice?.Id,
            PackageCount = packageCount,
            RequirementCount = requirementCount,
            Steps =
            [
                BuildRawFetchedStep(raw),
                BuildAttachmentStep(attachments),
                BuildStructuredParsingStep(raw, noticeStaging, packageStagings.Count, requirementStagings.Count),
                BuildReviewStep(reviewTask, notice),
                BuildFormalImportStep(raw, notice, formalPackages.Count, formalRequirements.Count)
            ]
        };
    }

    public async Task<IReadOnlyList<RawAttachmentDto>> ListRawAttachmentsAsync(long rawNoticeId, CancellationToken ct = default)
    {
        var raw = await _rawNotices.GetByIdAsync(rawNoticeId, ct);
        return raw == null
            ? Array.Empty<RawAttachmentDto>()
            : await ListRawAttachmentsCoreAsync(rawNoticeId, ct);
    }

    public async Task<RawAttachmentTextDto?> GetRawAttachmentTextAsync(
        long rawNoticeId,
        long attachmentId,
        CancellationToken ct = default)
    {
        var raw = await _rawNotices.GetByIdAsync(rawNoticeId, ct);
        if (raw == null)
            return null;

        var builder = await _rawAttachments.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        var attachment = await builder
            .Where(x => x.Id == attachmentId && x.RawNoticeId == rawNoticeId)
            .FirstOrDefaultAsync(ct);
        if (attachment == null)
            return null;

        return new RawAttachmentTextDto
        {
            Id = attachment.Id,
            RawNoticeId = attachment.RawNoticeId,
            FileName = attachment.FileName,
            TextContent = await ReadAttachmentTextContentAsync(attachment, ct)
        };
    }

    public async Task<RawAttachmentFileResult?> OpenRawAttachmentFileAsync(
        long rawNoticeId,
        long attachmentId,
        CancellationToken ct = default)
    {
        var raw = await _rawNotices.GetByIdAsync(rawNoticeId, ct);
        if (raw == null)
            return null;

        var builder = await _rawAttachments.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        var attachment = await builder
            .Where(x => x.Id == attachmentId && x.RawNoticeId == rawNoticeId)
            .FirstOrDefaultAsync(ct);
        if (attachment == null || string.IsNullOrWhiteSpace(attachment.StorageKey))
            return null;

        try
        {
            var stream = await _fileStore.OpenReadAsync(attachment.StorageKey, ct);
            return new RawAttachmentFileResult
            {
                Id = attachment.Id,
                RawNoticeId = attachment.RawNoticeId,
                FileName = string.IsNullOrWhiteSpace(attachment.FileName)
                    ? $"raw-attachment-{attachment.Id}"
                    : attachment.FileName,
                ContentType = GuessAttachmentContentType(attachment.FileName, attachment.FileType),
                FileSize = attachment.FileSize,
                Content = stream
            };
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                ex,
                "Failed to open BidOps attachment file for RawAttachmentId {RawAttachmentId} from {StorageKey}.",
                attachment.Id,
                attachment.StorageKey);
            return null;
        }
    }

    public async Task<PagedResult<ReviewTaskDto>> SearchReviewTasksAsync(ReviewTaskSearchQuery query, CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _reviewTasks.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            var keywordNoticeStagingIds = await FindReviewNoticeStagingIdsBySearchTermAsync(
                keyword,
                includeNoticeSummaryFields: true,
                ct);
            builder = keywordNoticeStagingIds.Count == 0
                ? builder.Where(x => x.TaskTitle.Contains(keyword))
                : builder.Where(x =>
                    x.TaskTitle.Contains(keyword) ||
                    (x.BizType == "NoticeStaging" && keywordNoticeStagingIds.Contains(x.BizId)));
        }

        if (query.Status.HasValue)
            builder = builder.Where(x => x.Status == query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.ProjectCode))
        {
            var projectCode = query.ProjectCode.Trim();
            var projectCodeNoticeStagingIds = await FindReviewNoticeStagingIdsBySearchTermAsync(
                projectCode,
                includeNoticeSummaryFields: false,
                ct);
            builder = projectCodeNoticeStagingIds.Count == 0
                ? builder.Where(x => false)
                : builder.Where(x => x.BizType == "NoticeStaging" && projectCodeNoticeStagingIds.Contains(x.BizId));
        }

        if (!string.IsNullOrWhiteSpace(query.NoticeType))
        {
            var noticeType = query.NoticeType.Trim();
            var noticeQuery = await _noticeStaging.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
            var noticeStagingIds = await noticeQuery
                .Where(x => x.NoticeType == noticeType)
                .SelectToListAsync(x => x.Id, ct);

            builder = noticeStagingIds.Count == 0
                ? builder.Where(x => false)
                : builder.Where(x => x.BizType == "NoticeStaging" && noticeStagingIds.Contains(x.BizId));
        }

        if (TryParseEnum<ReviewQualityRiskLevel>(query.RiskLevel, out var riskLevel))
            builder = builder.Where(x => x.RiskLevel == riskLevel);

        if (query.MinQualityScore.HasValue)
            builder = builder.Where(x => x.QualityScore >= query.MinQualityScore.Value);

        if (query.MaxQualityScore.HasValue)
            builder = builder.Where(x => x.QualityScore <= query.MaxQualityScore.Value);

        if (query.HasHighRiskIssue.HasValue)
        {
            builder = query.HasHighRiskIssue.Value
                ? builder.Where(x => x.HighRiskIssueCount > 0)
                : builder.Where(x => x.HighRiskIssueCount == 0);
        }

        if (TryParseEnum<ReviewRecommendation>(query.ReviewRecommendation, out var recommendation))
            builder = builder.Where(x => x.ReviewRecommendation == recommendation);

        if (!string.IsNullOrWhiteSpace(query.IssueType))
        {
            var issueType = query.IssueType.Trim();
            var issueQuery = await _reviewQualityIssues.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
            var reviewTaskIds = await issueQuery
                .Where(x => x.IssueType == issueType && !x.IsResolved)
                .SelectToListAsync(x => x.ReviewTaskId, ct);

            builder = reviewTaskIds.Count == 0
                ? builder.Where(x => false)
                : builder.Where(x => reviewTaskIds.Contains(x.Id));
        }

        var total = await builder.CountAsync(ct);
        var tasks = await builder
            .OrderByDescending(x => (int)x.RiskLevel * 10000 + x.HighRiskIssueCount * 100 + (100 - x.QualityScore))
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        var items = tasks.Select(Map).ToList();
        await EnrichReviewTaskSummariesAsync(items, ct);

        return new PagedResult<ReviewTaskDto>(total, items, pageIndex, pageSize);
    }

    public async Task<ReviewCorrectionAnalysisDto> GetReviewCorrectionAnalysisAsync(
        ReviewCorrectionAnalysisQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var builder = await _reviewCorrectionSamples.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        if (!string.IsNullOrWhiteSpace(query.SourceKind))
        {
            var sourceKind = query.SourceKind.Trim();
            builder = builder.Where(x => x.SourceKind == sourceKind);
        }

        if (!string.IsNullOrWhiteSpace(query.NoticeType))
        {
            var noticeType = query.NoticeType.Trim();
            builder = builder.Where(x => x.NoticeType == noticeType);
        }

        if (!string.IsNullOrWhiteSpace(query.FieldName))
        {
            var fieldName = query.FieldName.Trim();
            builder = builder.Where(x => x.FieldName == fieldName);
        }

        if (query.From.HasValue)
            builder = builder.Where(x => x.CreatedAt >= query.From.Value);
        if (query.To.HasValue)
            builder = builder.Where(x => x.CreatedAt <= query.To.Value);

        var samples = await builder
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            samples = samples
                .Where(x =>
                    x.FieldName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    x.OriginalHeader.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    x.OriginalValue.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    x.CorrectedValue.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    x.ReviewerPrompt.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    x.Reason.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return new ReviewCorrectionAnalysisDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            TotalSamples = samples.Count,
            TopFields = BuildBuckets(samples.Select(x => x.FieldName)),
            TopOriginalHeaders = BuildBuckets(samples.Select(x => x.OriginalHeader)),
            AmountUnitIssues = BuildBuckets(samples
                .Where(IsAmountCorrectionSample)
                .Select(x => FirstNonEmpty(x.OriginalHeader, x.FieldName))),
            RequirementIssues = BuildBuckets(samples
                .Where(IsRequirementCorrectionSample)
                .Select(x => FirstNonEmpty(x.FieldName, x.OriginalHeader))),
            ReparsePromptPatterns = BuildBuckets(samples
                .Where(x => x.SourceKind == BidOpsReviewCorrectionSourceKinds.ReparsePrompt)
                .Select(x => SummarizePromptPattern(x.ReviewerPrompt))),
            RecentSamples = samples
                .Take(20)
                .Select(MapReviewCorrectionSample)
                .ToList()
        };
    }

    public async Task<ReviewEfficiencyMetricsDto> GetReviewEfficiencyMetricsAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var taskBuilder = await _reviewTasks.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        var tasks = await taskBuilder.ToListAsync(ct);
        var pendingTasks = tasks
            .Where(x => x.Status is ReviewTaskStatus.Pending or ReviewTaskStatus.InReview or ReviewTaskStatus.ReparseRequired)
            .ToList();
        var reviewedToday = tasks
            .Where(x => x.ReviewedAt.HasValue && x.ReviewedAt.Value >= today)
            .ToList();

        var correctionBuilder = await _reviewCorrectionSamples.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        var todaySamples = await correctionBuilder
            .Where(x => x.CreatedAt >= today)
            .ToListAsync(ct);

        var averageHandlingMinutes = reviewedToday.Count == 0
            ? 0m
            : (decimal)reviewedToday.Average(x => Math.Max(0, (x.ReviewedAt!.Value - x.CreatedAt).TotalMinutes));

        return new ReviewEfficiencyMetricsDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            TodayNewReviewTasks = tasks.Count(x => x.CreatedAt >= today),
            PendingReviewTasks = pendingTasks.Count,
            LowRiskCount = pendingTasks.Count(x => x.RiskLevel == ReviewQualityRiskLevel.Low),
            MediumRiskCount = pendingTasks.Count(x => x.RiskLevel == ReviewQualityRiskLevel.Medium),
            HighRiskCount = pendingTasks.Count(x => x.RiskLevel == ReviewQualityRiskLevel.High),
            LowRiskRatio = pendingTasks.Count == 0
                ? 0
                : Math.Round(pendingTasks.Count(x => x.RiskLevel == ReviewQualityRiskLevel.Low) * 100m / pendingTasks.Count, 2),
            BulkApprovedToday = todaySamples.Count(x => x.SourceKind == BidOpsReviewCorrectionSourceKinds.BulkApprove),
            AverageHandlingMinutes = Math.Round((decimal)averageHandlingMinutes, 2),
            ReparsePromptSamplesToday = todaySamples.Count(x => x.SourceKind == BidOpsReviewCorrectionSourceKinds.ReparsePrompt)
        };
    }

    public async Task<PagedResult<ProcessingFailureDto>> SearchProcessingFailuresAsync(
        ProcessingFailureSearchQuery query,
        CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _rawNotices.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        builder = builder.Where(x => x.Status == RawNoticeStatus.Failed);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x =>
                x.Title.Contains(keyword) ||
                x.DetailUrl.Contains(keyword) ||
                x.LastError.Contains(keyword));
        }

        var total = await builder.CountAsync(ct);
        var items = await builder
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .SelectToListAsync(x => new ProcessingFailureDto
            {
                RawNoticeId = x.Id,
                Title = x.Title,
                DetailUrl = x.DetailUrl,
                NoticeType = x.NoticeType,
                PublishTime = x.PublishTime,
                FetchTime = x.FetchTime,
                RawStatus = x.Status,
                LastError = x.LastError
            }, ct);

        return new PagedResult<ProcessingFailureDto>(total, items, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<long>> GetReviewTaskBackgroundJobIdsAsync(long id, CancellationToken ct = default)
    {
        var task = await _reviewTasks.GetByIdAsync(id, ct);
        if (task == null)
            return [];

        var sampleQuery = await _reviewCorrectionSamples.QueryDataScopeAsync(
            BidOpsDataResources.ReviewTask,
            AtlasDataScopeType.AllTenant,
            ct);
        var evidenceItems = await sampleQuery
            .Where(x =>
                x.ReviewTaskId == id &&
                (x.SourceKind == BidOpsReviewCorrectionSourceKinds.ReparsePrompt ||
                 x.SourceKind == BidOpsReviewCorrectionSourceKinds.ApprovalOutcomeExtract) &&
                x.OriginalRowJson.Contains("backgroundJobId"))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.OriginalRowJson)
            .ToListAsync(ct);

        var jobIds = new List<long>();
        var seen = new HashSet<long>();
        foreach (var evidenceJson in evidenceItems)
        {
            if (TryReadBackgroundJobId(evidenceJson, out var jobId) && seen.Add(jobId))
                jobIds.Add(jobId);
        }

        return jobIds;
    }

    public async Task<ReviewTaskDetailDto?> GetReviewTaskDetailAsync(long id, CancellationToken ct = default)
    {
        var task = await _reviewTasks.GetByIdAsync(id, ct);
        if (task == null)
            return null;

        NoticeStaging? notice = null;
        if (task.BizType == "NoticeStaging")
            notice = await _noticeStaging.GetByIdAsync(task.BizId, ct);

        var raw = task.RawNoticeId.HasValue
            ? await _rawNotices.GetByIdAsync(task.RawNoticeId.Value, ct)
            : null;

        var packageDtos = new List<PackageStagingDto>();
        var procurementDetailDtos = new List<ProcurementDetailStagingDto>();
        if (notice != null)
        {
            var packagesQuery = await _packageStaging.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
            var packages = await packagesQuery.Where(x => x.NoticeStagingId == notice.Id).ToListAsync(ct);
            var packageIds = packages.Select(x => x.Id).ToArray();
            var requirementsQuery = await _requirementStaging.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
            var requirements = await requirementsQuery.Where(x => packageIds.Contains(x.PackageStagingId)).ToListAsync(ct);

            packageDtos = packages
                .Select(package => new PackageStagingDto
                {
                    Id = package.Id,
                    NoticeStagingId = package.NoticeStagingId,
                    LotNo = package.LotNo,
                    LotName = package.LotName,
                    PackageNo = package.PackageNo,
                    PackageName = package.PackageName,
                    Category = package.Category,
                    BudgetAmount = package.BudgetAmount,
                    AiConfidence = package.AiConfidence,
                    ReviewStatus = package.ReviewStatus,
                    Requirements = requirements
                        .Where(requirement => requirement.PackageStagingId == package.Id)
                        .Select(requirement => new RequirementStagingDto
                        {
                            Id = requirement.Id,
                            PackageStagingId = requirement.PackageStagingId,
                            RequirementType = requirement.RequirementType,
                            OriginalText = requirement.OriginalText,
                            IsMandatory = requirement.IsMandatory,
                            IsRejectRisk = requirement.IsRejectRisk,
                            RequiredEvidenceType = requirement.RequiredEvidenceType,
                            RiskLevel = requirement.RiskLevel,
                            AiExplanation = requirement.AiExplanation,
                            AiConfidence = requirement.AiConfidence
                        })
                        .ToList()
                })
                .ToList();

            var procurementDetailQuery = await _procurementDetailStaging.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
            var procurementDetails = await procurementDetailQuery
                .Where(x => x.NoticeStagingId == notice.Id)
                .ToListAsync(ct);
            procurementDetailDtos = procurementDetails
                .OrderBy(x => x.TableIndex ?? int.MaxValue)
                .ThenBy(x => x.RowIndex ?? int.MaxValue)
                .ThenBy(x => x.Id)
                .Select(MapProcurementDetailStaging)
                .ToList();
        }

        var rawDto = raw == null ? null : Map(raw);
        if (rawDto != null && raw != null)
            rawDto.TextContent = await ReadRawTextContentAsync(raw, ct);
        IReadOnlyList<RawAttachmentDto> attachments = raw == null
            ? Array.Empty<RawAttachmentDto>()
            : await ListRawAttachmentsCoreAsync(raw.Id, ct);
        var outcomeRecords = raw == null
            ? []
            : await ListOutcomeSupplierRecordsForReviewAsync(raw.Id, ct);
        if (outcomeRecords.Count == 0 && raw != null)
            outcomeRecords = await BuildOutcomeSupplierPreviewRecordsAsync(raw, ct);

        var buyer = await BuildReviewBuyerInfoAsync(notice, raw, packageDtos.Count, outcomeRecords, ct);
        var qualityIssues = await ListReviewQualityIssuesAsync(task.Id, ct);

        var taskDto = Map(task);
        if (notice != null)
            ApplyReviewTaskSummary(taskDto, notice, packageDtos);

        return new ReviewTaskDetailDto
        {
            Task = taskDto,
            RawNotice = rawDto,
            Notice = notice == null ? null : Map(notice),
            Buyer = buyer,
            OutcomeSuppliers = outcomeRecords.Select(MapOutcomeRecord).ToList(),
            Packages = packageDtos,
            ProcurementDetails = procurementDetailDtos,
            QualityIssues = qualityIssues.Select(MapReviewQualityIssue).ToList(),
            Attachments = attachments.ToList()
        };
    }

    private async Task<List<ReviewQualityIssue>> ListReviewQualityIssuesAsync(long reviewTaskId, CancellationToken ct)
    {
        var builder = await _reviewQualityIssues.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        var issues = await builder
            .Where(x => x.ReviewTaskId == reviewTaskId)
            .ToListAsync(ct);
        return issues
            .OrderBy(x => x.IsResolved)
            .ThenByDescending(x => x.Severity)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToList();
    }

    private async Task<List<long>> FindReviewNoticeStagingIdsBySearchTermAsync(
        string searchTerm,
        bool includeNoticeSummaryFields,
        CancellationToken ct)
    {
        var term = searchTerm.Trim();
        if (string.IsNullOrWhiteSpace(term))
            return [];

        var ids = new HashSet<long>();

        var noticeQuery = await _noticeStaging.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        var noticeIds = includeNoticeSummaryFields
            ? await noticeQuery
                .Where(x =>
                    x.ProjectCode.Contains(term) ||
                    x.ProjectName.Contains(term) ||
                    x.BuyerName.Contains(term))
                .SelectToListAsync(x => x.Id, ct)
            : await noticeQuery
                .Where(x => x.ProjectCode.Contains(term))
                .SelectToListAsync(x => x.Id, ct);
        foreach (var id in noticeIds)
            ids.Add(id);

        var procurementDetailQuery = await _procurementDetailStaging.QueryDataScopeAsync(
            BidOpsDataResources.ReviewTask,
            AtlasDataScopeType.AllTenant,
            ct);
        var procurementNoticeIds = await procurementDetailQuery
            .Where(x => x.ProjectCode.Contains(term))
            .SelectToListAsync(x => x.NoticeStagingId, ct);
        foreach (var id in procurementNoticeIds)
            ids.Add(id);

        var outcomeQuery = await _outcomeRecords.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        var outcomeRawNoticeIds = await outcomeQuery
            .Where(x => x.ProjectCode.Contains(term))
            .SelectToListAsync(x => x.RawNoticeId, ct);
        if (outcomeRawNoticeIds.Count > 0)
        {
            var rawNoticeIds = outcomeRawNoticeIds.Distinct().ToArray();
            var outcomeNoticeQuery = await _noticeStaging.QueryDataScopeAsync(
                BidOpsDataResources.ReviewTask,
                AtlasDataScopeType.AllTenant,
                ct);
            var outcomeNoticeIds = await outcomeNoticeQuery
                .Where(x => rawNoticeIds.Contains(x.RawNoticeId))
                .SelectToListAsync(x => x.Id, ct);
            foreach (var id in outcomeNoticeIds)
                ids.Add(id);
        }

        return ids.ToList();
    }

    private async Task EnrichReviewTaskSummariesAsync(List<ReviewTaskDto> tasks, CancellationToken ct)
    {
        var noticeStagingIds = tasks
            .Where(x => x.BizType == "NoticeStaging")
            .Select(x => x.BizId)
            .Distinct()
            .ToArray();
        if (noticeStagingIds.Length == 0)
            return;

        var noticeQuery = await _noticeStaging.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        var notices = await noticeQuery
            .Where(x => noticeStagingIds.Contains(x.Id))
            .ToListAsync(ct);
        var noticesById = notices.ToDictionary(x => x.Id);

        var packageQuery = await _packageStaging.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        var packages = await packageQuery
            .Where(x => noticeStagingIds.Contains(x.NoticeStagingId))
            .ToListAsync(ct);
        var packageIds = packages.Select(x => x.Id).ToArray();

        var requirements = new List<RequirementStaging>();
        if (packageIds.Length > 0)
        {
            var requirementQuery = await _requirementStaging.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
            requirements = await requirementQuery
                .Where(x => packageIds.Contains(x.PackageStagingId))
                .ToListAsync(ct);
        }

        var packagesByNoticeId = packages.GroupBy(x => x.NoticeStagingId).ToDictionary(x => x.Key, x => x.ToList());
        var requirementsByPackageId = requirements.GroupBy(x => x.PackageStagingId).ToDictionary(x => x.Key, x => x.ToList());

        foreach (var task in tasks)
        {
            if (!noticesById.TryGetValue(task.BizId, out var notice))
                continue;

            var taskPackages = packagesByNoticeId.GetValueOrDefault(notice.Id) ?? [];
            var taskPackageDtos = taskPackages
                .Select(package => new PackageStagingDto
                {
                    Id = package.Id,
                    NoticeStagingId = package.NoticeStagingId,
                    Requirements = requirementsByPackageId.GetValueOrDefault(package.Id)?
                        .Select(requirement => new RequirementStagingDto
                        {
                            Id = requirement.Id,
                            IsRejectRisk = requirement.IsRejectRisk
                        })
                        .ToList() ?? []
                })
                .ToList();
            ApplyReviewTaskSummary(task, notice, taskPackageDtos);
        }
    }

    private static void ApplyReviewTaskSummary(
        ReviewTaskDto task,
        NoticeStaging notice,
        IReadOnlyCollection<PackageStagingDto> packages)
    {
        task.ProjectName = notice.ProjectName;
        task.ProjectCode = notice.ProjectCode;
        task.BuyerName = notice.BuyerName;
        task.Region = notice.Region;
        task.NoticeType = notice.NoticeType;
        task.PublishTime = notice.PublishTime;
        task.SignupDeadline = notice.SignupDeadline;
        task.BidDeadline = notice.BidDeadline;
        task.OpenBidTime = notice.OpenBidTime;
        task.AiConfidence = notice.AiConfidence;
        task.PackageCount = packages.Count;
        task.RequirementCount = packages.Sum(x => x.Requirements.Count);
        task.RejectRiskCount = packages.Sum(x => x.Requirements.Count(requirement => requirement.IsRejectRisk));
    }

    private static bool TryParseEnum<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value) &&
               Enum.TryParse(value.Trim(), ignoreCase: true, out result);
    }

    private static List<ReviewCorrectionBucketDto> BuildBuckets(IEnumerable<string?> values, int take = 10)
    {
        return values
            .Select(x => string.IsNullOrWhiteSpace(x) ? "未标注" : x.Trim())
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ReviewCorrectionBucketDto
            {
                Key = x.Key,
                Count = x.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Key)
            .Take(take)
            .ToList();
    }

    private static bool IsAmountCorrectionSample(ReviewCorrectionSample sample)
    {
        var text = string.Join(' ', sample.FieldName, sample.OriginalHeader, sample.Reason, sample.OriginalValue, sample.CorrectedValue);
        return ContainsAny(text, "Amount", "Price", "金额", "限价", "报价", "万元", "折扣", "费率", "%", "％");
    }

    private static bool IsRequirementCorrectionSample(ReviewCorrectionSample sample)
    {
        var text = string.Join(' ', sample.FieldName, sample.OriginalHeader, sample.Reason, sample.OriginalValue, sample.CorrectedValue);
        return ContainsAny(text, "Requirement", "Qualification", "Performance", "Personnel", "资质", "资格", "业绩", "人员");
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "未标注";
    }

    private static string SummarizePromptPattern(string? prompt)
    {
        var value = string.IsNullOrWhiteSpace(prompt) ? "未填写提示词" : prompt.Trim();
        return value.Length <= 80 ? value : value[..80];
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> ReadRawTextContentAsync(RawNotice raw, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw.TextContentStorageKey))
            return BidOpsRawNoticeTextFormatter.ToDisplayText(raw.TextPreview);

        try
        {
            await using var stream = await _fileStore.OpenReadAsync(raw.TextContentStorageKey, ct);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync(ct);
            if (text.Length > RawTextContentMaxCharacters)
                text = text[..RawTextContentMaxCharacters];

            return BidOpsRawNoticeTextFormatter.ToDisplayText(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to read BidOps raw text content for RawNoticeId {RawNoticeId} from {StorageKey}.",
                raw.Id,
                raw.TextContentStorageKey);
            return BidOpsRawNoticeTextFormatter.ToDisplayText(raw.TextPreview);
        }
    }

    private async Task<List<OutcomeSupplierRecord>> ListOutcomeSupplierRecordsForReviewAsync(
        long rawNoticeId,
        CancellationToken ct)
    {
        var builder = await _outcomeRecords.QueryDataScopeAsync(BidOpsDataResources.OutcomeSupplierRecord, AtlasDataScopeType.AllTenant, ct);
        var records = await builder
            .Where(x => x.RawNoticeId == rawNoticeId)
            .ToListAsync(ct);
        return records
            .OrderBy(x => x.ExtractionOrder)
            .ThenBy(x => x.Id)
            .ToList();
    }

    private async Task<List<OutcomeSupplierRecord>> BuildOutcomeSupplierPreviewRecordsAsync(
        RawNotice raw,
        CancellationToken ct)
    {
        var text = await ReadRawSourceTextForOutcomePreviewAsync(raw, ct);
        if (!BidOpsOutcomeSupplierTextParser.LooksLikeOutcomeNotice(raw.Title, raw.NoticeType, text))
            return [];

        var extracts = BidOpsOutcomeSupplierExtractBuilder.Extract(
            raw.Title,
            raw.NoticeType,
            raw.DetailUrl,
            raw.PublishTime,
            text,
            raw.Id);
        if (extracts.Count == 0)
            return [];

        var records = new List<OutcomeSupplierRecord>();
        foreach (var extract in extracts)
        {
            var supplierName = Truncate(BidOpsTextQuality.CleanExtractedValue(extract.SupplierName), 300);
            var supplierNameNormalized = Truncate(BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(supplierName), 191);
            if (string.IsNullOrWhiteSpace(supplierName) || string.IsNullOrWhiteSpace(supplierNameNormalized))
                continue;

            records.Add(new OutcomeSupplierRecord
            {
                TenantId = raw.TenantId,
                RawNoticeId = raw.Id,
                SourceUrl = Truncate(raw.DetailUrl, 1500),
                NoticeTitle = Truncate(raw.Title, 500),
                NoticeType = Truncate(raw.NoticeType, 64),
                ProjectName = Truncate(extract.ProjectName, 500),
                ProjectCode = Truncate(extract.ProjectCode, 128),
                BuyerName = Truncate(extract.BuyerName, 300),
                PublishTime = raw.PublishTime,
                LotNo = Truncate(extract.LotNo, 128),
                LotName = Truncate(extract.LotName, 300),
                PackageNo = Truncate(extract.PackageNo, 128),
                PackageName = Truncate(ClearIfSameMeaningfulValue(extract.PackageName, extract.ProjectName), 500),
                Category = Truncate(extract.Category, 128),
                SupplierName = supplierName,
                SupplierNameNormalized = supplierNameNormalized,
                OutcomeType = NormalizeOutcomeType(extract.OutcomeType),
                Rank = extract.Rank,
                AwardAmount = extract.AwardAmount,
                ProcurementAgencyServiceFeeAmount = extract.ProcurementAgencyServiceFeeAmount,
                ExtractionOrder = records.Count,
                Currency = "CNY",
                EvidenceText = Truncate(extract.EvidenceText, 2000),
                ExtractionConfidence = ClampConfidence(extract.Confidence),
                CreatedAt = raw.FetchTime
            });
        }

        return records
            .OrderBy(x => x.ExtractionOrder)
            .ThenBy(x => x.Id)
            .ToList();
    }

    private async Task<string> ReadRawSourceTextForOutcomePreviewAsync(RawNotice raw, CancellationToken ct)
    {
        var builder = new StringBuilder();

        AppendOutcomePreviewText(
            builder,
            await TryReadStoredTextForOutcomePreviewAsync(raw.TextContentStorageKey, raw.TextPreview, raw.Id, ct));
        AppendOutcomePreviewText(
            builder,
            await TryReadStoredTextForOutcomePreviewAsync(raw.HtmlSnapshotStorageKey, string.Empty, raw.Id, ct));

        var attachmentBuilder = await _rawAttachments.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        var attachments = await attachmentBuilder
            .Where(x => x.RawNoticeId == raw.Id &&
                        x.TextExtractStatus == TextExtractStatus.Succeeded &&
                        x.TextContentStorageKey != string.Empty)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

        foreach (var attachment in attachments)
        {
            var attachmentText = await TryReadStoredTextForOutcomePreviewAsync(
                attachment.TextContentStorageKey,
                string.Empty,
                raw.Id,
                ct);
            if (string.IsNullOrWhiteSpace(attachmentText))
                continue;

            AppendOutcomePreviewText(builder, $"Attachment: {attachment.FileName}");
            AppendOutcomePreviewText(builder, attachmentText);
            if (builder.Length >= OutcomePreviewTextMaxCharacters)
                break;
        }

        return builder.ToString();
    }

    private async Task<string> TryReadStoredTextForOutcomePreviewAsync(
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
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to read BidOps outcome preview text for RawNoticeId {RawNoticeId} from {StorageKey}.",
                rawNoticeId,
                storageKey);
            return fallback;
        }
    }

    private static void AppendOutcomePreviewText(StringBuilder builder, string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || builder.Length >= OutcomePreviewTextMaxCharacters)
            return;

        if (builder.Length > 0)
            builder.AppendLine();

        var remaining = OutcomePreviewTextMaxCharacters - builder.Length;
        if (text.Length > remaining)
            text = text[..remaining];

        builder.AppendLine(text);
    }

    private async Task<ReviewBuyerInfoDto?> BuildReviewBuyerInfoAsync(
        NoticeStaging? notice,
        RawNotice? raw,
        int packageCount,
        IReadOnlyList<OutcomeSupplierRecord> outcomeRecords,
        CancellationToken ct)
    {
        var buyerName = FirstMeaningful(
            notice?.BuyerName,
            outcomeRecords.Select(x => x.BuyerName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(buyerName))
            return null;

        var normalized = BidOpsOrganizationNameNormalizer.NormalizeForMatch(buyerName);
        Buyer? buyer = null;
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            var buyerQuery = await _buyers.QueryDataScopeAsync(BidOpsDataResources.Buyer, AtlasDataScopeType.AllTenant, ct);
            buyer = await buyerQuery.Where(x => x.NameNormalized == normalized).FirstOrDefaultAsync(ct);
        }

        var latestOutcome = outcomeRecords
            .OrderByDescending(x => x.PublishTime ?? x.CreatedAt)
            .FirstOrDefault();

        return new ReviewBuyerInfoDto
        {
            BuyerId = buyer?.Id,
            BuyerName = buyer?.Name ?? buyerName,
            Exists = buyer != null,
            WillCreateOnApproval = buyer == null && normalized.Length >= 4,
            ProjectName = FirstMeaningful(notice?.ProjectName, latestOutcome?.ProjectName, raw?.Title),
            ProjectCode = FirstMeaningful(notice?.ProjectCode, latestOutcome?.ProjectCode),
            NoticeTitle = FirstMeaningful(raw?.Title, latestOutcome?.NoticeTitle),
            SourceUrl = FirstMeaningful(raw?.DetailUrl, latestOutcome?.SourceUrl),
            Region = FirstMeaningful(notice?.Region, latestOutcome?.Region),
            PublishTime = notice?.PublishTime ?? latestOutcome?.PublishTime ?? raw?.PublishTime,
            BudgetAmount = notice?.BudgetAmount,
            PackageCount = packageCount
        };
    }

    private async Task<IReadOnlyList<RawAttachmentDto>> ListRawAttachmentsCoreAsync(long rawNoticeId, CancellationToken ct)
    {
        var builder = await _rawAttachments.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        return await builder
            .Where(x => x.RawNoticeId == rawNoticeId)
            .OrderBy(x => x.Id)
            .SelectToListAsync(x => new RawAttachmentDto
            {
                Id = x.Id,
                RawNoticeId = x.RawNoticeId,
                FileName = x.FileName,
                FileUrl = x.FileUrl,
                FileType = x.FileType,
                FileSize = x.FileSize,
                DownloadStatus = x.DownloadStatus,
                TextExtractStatus = x.TextExtractStatus,
                HasLocalFile = x.StorageKey != string.Empty,
                HasExtractedText = x.TextContentStorageKey != string.Empty,
                CreatedAt = x.CreatedAt
            }, ct);
    }

    private async Task<string> ReadAttachmentTextContentAsync(RawAttachment attachment, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(attachment.TextContentStorageKey))
            return string.Empty;

        try
        {
            await using var stream = await _fileStore.OpenReadAsync(attachment.TextContentStorageKey, ct);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync(ct);
            return text.Length <= AttachmentTextContentMaxCharacters
                ? text
                : text[..AttachmentTextContentMaxCharacters];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to read BidOps attachment text for RawAttachmentId {RawAttachmentId} from {StorageKey}.",
                attachment.Id,
                attachment.TextContentStorageKey);
            return string.Empty;
        }
    }

    private static string GuessAttachmentContentType(string fileName, string fileType)
    {
        var extension = string.IsNullOrWhiteSpace(fileType) || fileType == "file"
            ? Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant()
            : fileType.Trim().TrimStart('.').ToLowerInvariant();

        return extension switch
        {
            "pdf" => "application/pdf",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "doc" => "application/msword",
            "xlsx" or "xlsm" or "xltx" or "xltm" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "xls" => "application/vnd.ms-excel",
            "zip" => "application/zip",
            "html" or "htm" => "text/html; charset=utf-8",
            "txt" => "text/plain; charset=utf-8",
            _ => "application/octet-stream"
        };
    }

    private async Task<List<RawAttachment>> ListRawAttachmentEntitiesAsync(long rawNoticeId, CancellationToken ct)
    {
        var builder = await _rawAttachments.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        return await builder
            .Where(x => x.RawNoticeId == rawNoticeId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
    }

    private async Task<NoticeStaging?> GetNoticeStagingByRawNoticeAsync(long rawNoticeId, CancellationToken ct)
    {
        var builder = await _noticeStaging.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        return await builder
            .Where(x => x.RawNoticeId == rawNoticeId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<List<PackageStaging>> ListPackageStagingsAsync(long noticeStagingId, CancellationToken ct)
    {
        var builder = await _packageStaging.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        return await builder
            .Where(x => x.NoticeStagingId == noticeStagingId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
    }

    private async Task<List<RequirementStaging>> ListRequirementStagingsAsync(long[] packageStagingIds, CancellationToken ct)
    {
        if (packageStagingIds.Length == 0)
            return [];

        var builder = await _requirementStaging.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        return await builder
            .Where(x => packageStagingIds.Contains(x.PackageStagingId))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
    }

    private async Task<ReviewTask?> GetLatestReviewTaskForRawNoticeAsync(
        long rawNoticeId,
        long? noticeStagingId,
        CancellationToken ct)
    {
        var builder = await _reviewTasks.QueryDataScopeAsync(BidOpsDataResources.ReviewTask, AtlasDataScopeType.AllTenant, ct);
        builder = noticeStagingId.HasValue
            ? builder.Where(x =>
                x.RawNoticeId == rawNoticeId ||
                (x.BizType == "NoticeStaging" && x.BizId == noticeStagingId.Value))
            : builder.Where(x => x.RawNoticeId == rawNoticeId);

        return await builder
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<Notice?> GetNoticeByRawNoticeAsync(long rawNoticeId, CancellationToken ct)
    {
        var builder = await _notices.QueryDataScopeAsync(BidOpsDataResources.Notice, AtlasDataScopeType.AllTenant, ct);
        return await builder
            .Where(x => x.RawNoticeId == rawNoticeId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<List<TenderPackage>> ListTenderPackagesByNoticeAsync(long noticeId, CancellationToken ct)
    {
        var builder = await _packages.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        return await builder
            .Where(x => x.NoticeId == noticeId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
    }

    private async Task<List<RequirementItem>> ListRequirementItemsByPackageIdsAsync(long[] packageIds, CancellationToken ct)
    {
        if (packageIds.Length == 0)
            return [];

        var builder = await _requirements.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        return await builder
            .Where(x => packageIds.Contains(x.PackageId))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
    }

    private static RawNoticePipelineStepDto BuildRawFetchedStep(RawNotice raw)
    {
        return new RawNoticePipelineStepDto
        {
            Code = "RawFetched",
            Title = "公告采集",
            Status = PipelineStatusCompleted,
            Description = string.IsNullOrWhiteSpace(raw.DetailUrl)
                ? "原始公告已进入 BidOps。"
                : $"原始公告已从公开地址采集：{raw.DetailUrl}",
            OccurredAt = raw.FetchTime,
            TotalCount = 1,
            SucceededCount = 1
        };
    }

    private static RawNoticePipelineStepDto BuildAttachmentStep(IReadOnlyCollection<RawAttachment> attachments)
    {
        if (attachments.Count == 0)
        {
            return new RawNoticePipelineStepDto
            {
                Code = "AttachmentProcessing",
                Title = "附件处理",
                Status = PipelineStatusSkipped,
                Description = "未发现公开附件，附件处理步骤跳过。"
            };
        }

        var downloadedCount = attachments.Count(x => x.DownloadStatus == DownloadStatus.Succeeded);
        var textExtractedCount = attachments.Count(x => x.TextExtractStatus == TextExtractStatus.Succeeded);
        var failedCount = attachments.Count(x =>
            x.DownloadStatus == DownloadStatus.Failed ||
            x.TextExtractStatus == TextExtractStatus.Failed);
        var pendingCount = attachments.Count(x =>
            x.DownloadStatus == DownloadStatus.Pending ||
            x.TextExtractStatus == TextExtractStatus.Pending);
        var status = failedCount > 0
            ? PipelineStatusFailed
            : pendingCount > 0
                ? PipelineStatusPending
                : PipelineStatusCompleted;

        return new RawNoticePipelineStepDto
        {
            Code = "AttachmentProcessing",
            Title = "附件处理",
            Status = status,
            Description = $"发现 {attachments.Count} 个公开附件，{downloadedCount} 个下载成功，{textExtractedCount} 个完成文本提取。",
            OccurredAt = attachments.Max(x => x.UpdatedAt ?? x.CreatedAt),
            TotalCount = attachments.Count,
            SucceededCount = textExtractedCount,
            FailedCount = failedCount,
            PendingCount = pendingCount,
            Error = failedCount > 0 ? $"有 {failedCount} 个附件下载或文本提取失败。" : string.Empty
        };
    }

    private static RawNoticePipelineStepDto BuildStructuredParsingStep(
        RawNotice raw,
        NoticeStaging? noticeStaging,
        int packageCount,
        int requirementCount)
    {
        if (noticeStaging != null)
        {
            return new RawNoticePipelineStepDto
            {
                Code = "StructuredParsing",
                Title = "结构化解析",
                Status = PipelineStatusCompleted,
                Description = $"已生成审核数据，包件 {packageCount} 个，要求项 {requirementCount} 条。",
                OccurredAt = noticeStaging.CreatedAt,
                TotalCount = 1,
                SucceededCount = 1
            };
        }

        return new RawNoticePipelineStepDto
        {
            Code = "StructuredParsing",
            Title = "结构化解析",
            Status = raw.Status == RawNoticeStatus.Failed ? PipelineStatusFailed : PipelineStatusPending,
            Description = raw.Status == RawNoticeStatus.Failed ? "原始公告失败，未生成审核数据。" : "等待 Worker 生成可审核的结构化数据。",
            TotalCount = 1,
            FailedCount = raw.Status == RawNoticeStatus.Failed ? 1 : 0,
            PendingCount = raw.Status == RawNoticeStatus.Failed ? 0 : 1,
            Error = raw.Status == RawNoticeStatus.Failed ? raw.LastError : string.Empty
        };
    }

    private static RawNoticePipelineStepDto BuildReviewStep(ReviewTask? reviewTask, Notice? notice)
    {
        if (reviewTask == null)
        {
            var completedByNotice = notice != null;
            return new RawNoticePipelineStepDto
            {
                Code = "HumanReview",
                Title = "人工审核",
                Status = completedByNotice ? PipelineStatusCompleted : PipelineStatusPending,
                Description = completedByNotice ? "正式公告已入库，审核链路已完成。" : "尚未生成审核任务。",
                TotalCount = completedByNotice ? 1 : 0,
                SucceededCount = completedByNotice ? 1 : 0,
                PendingCount = completedByNotice ? 0 : 1
            };
        }

        var status = reviewTask.Status switch
        {
            ReviewTaskStatus.Approved or ReviewTaskStatus.Merged => PipelineStatusCompleted,
            ReviewTaskStatus.Ignored => PipelineStatusSkipped,
            _ => PipelineStatusPending
        };

        return new RawNoticePipelineStepDto
        {
            Code = "HumanReview",
            Title = "人工审核",
            Status = status,
            Description = reviewTask.Status switch
            {
                ReviewTaskStatus.Approved => "审核已通过。",
                ReviewTaskStatus.Merged => "审核任务已合并处理。",
                ReviewTaskStatus.Ignored => "审核任务已忽略。",
                ReviewTaskStatus.InReview => "审核任务处理中。",
                ReviewTaskStatus.ReparseRequired => "审核要求重新解析。",
                _ => "等待人工审核。"
            },
            OccurredAt = reviewTask.ReviewedAt ?? reviewTask.UpdatedAt ?? reviewTask.CreatedAt,
            TotalCount = 1,
            SucceededCount = status == PipelineStatusCompleted ? 1 : 0,
            PendingCount = status == PipelineStatusPending ? 1 : 0
        };
    }

    private static RawNoticePipelineStepDto BuildFormalImportStep(
        RawNotice raw,
        Notice? notice,
        int packageCount,
        int requirementCount)
    {
        if (notice != null)
        {
            return new RawNoticePipelineStepDto
            {
                Code = "FormalImport",
                Title = "正式入库",
                Status = PipelineStatusCompleted,
                Description = $"正式公告已创建，包件 {packageCount} 个，要求项 {requirementCount} 条。",
                OccurredAt = notice.CreatedAt,
                TotalCount = 1,
                SucceededCount = 1
            };
        }

        var skipped = raw.Status == RawNoticeStatus.Ignored;
        var failed = raw.Status == RawNoticeStatus.Failed;
        return new RawNoticePipelineStepDto
        {
            Code = "FormalImport",
            Title = "正式入库",
            Status = skipped ? PipelineStatusSkipped : failed ? PipelineStatusFailed : PipelineStatusPending,
            Description = skipped
                ? "原始公告已忽略，不进入正式公告库。"
                : failed
                    ? "原始公告失败，未写入正式公告库。"
                    : "等待审核通过后写入正式公告、包件和要求项。",
            TotalCount = 1,
            FailedCount = failed ? 1 : 0,
            PendingCount = skipped || failed ? 0 : 1,
            Error = failed ? raw.LastError : string.Empty
        };
    }

    public async Task<PagedResult<NoticeDto>> SearchNoticesAsync(NoticeSearchQuery query, CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _notices.QueryDataScopeAsync(BidOpsDataResources.Notice, AtlasDataScopeType.AllTenant, ct);
        Dictionary<long, string>? lifecycleStatuses = null;
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x =>
                x.Title.Contains(keyword) ||
                x.ProjectName.Contains(keyword) ||
                x.ProjectCode.Contains(keyword) ||
                x.BuyerName.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.NoticeType))
        {
            var noticeType = query.NoticeType.Trim();
            builder = builder.Where(x => x.NoticeType == noticeType);
        }

        var lifecycleStatusFilter = NormalizeLifecycleReviewStatus(query.LifecycleReviewStatus);
        if (!string.IsNullOrWhiteSpace(lifecycleStatusFilter))
        {
            lifecycleStatuses = await LoadLifecycleReviewStatusesByAwardRawNoticeAsync(ct);
            builder = ApplyNoticeLifecycleReviewStatusFilter(builder, lifecycleStatuses, lifecycleStatusFilter);
        }

        var total = await builder.CountAsync(ct);
        var items = await builder
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .SelectToListAsync(x => new NoticeDto
            {
                Id = x.Id,
                RawNoticeId = x.RawNoticeId,
                Title = x.Title,
                NoticeType = x.NoticeType,
                ProjectName = x.ProjectName,
                ProjectCode = x.ProjectCode,
                BuyerName = x.BuyerName,
                Region = x.Region,
                BudgetAmount = x.BudgetAmount,
                PublishTime = x.PublishTime,
                BidDeadline = x.BidDeadline,
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }, ct);
        await EnrichNoticeLifecycleReviewStatusesAsync(items, lifecycleStatuses, ct);

        return new PagedResult<NoticeDto>(total, items, pageIndex, pageSize);
    }

    public async Task<NoticeDto?> GetNoticeAsync(long id, CancellationToken ct = default)
    {
        var builder = await _notices.QueryDataScopeAsync(BidOpsDataResources.Notice, AtlasDataScopeType.AllTenant, ct);
        var notice = await builder.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (notice == null)
            return null;

        var dto = MapNotice(notice);
        await EnrichNoticeLifecycleReviewStatusesAsync([dto], null, ct);
        return dto;
    }

    private static IQueryBuilder<Notice> ApplyNoticeLifecycleReviewStatusFilter(
        IQueryBuilder<Notice> builder,
        IReadOnlyDictionary<long, string> lifecycleStatuses,
        string lifecycleStatusFilter)
    {
        if (lifecycleStatusFilter == BidOpsLifecycleReviewStatuses.NotAnalyzed)
        {
            var linkedRawNoticeIds = lifecycleStatuses.Keys.ToList();
            return linkedRawNoticeIds.Count == 0
                ? builder
                : builder.Where(x => !linkedRawNoticeIds.Contains(x.RawNoticeId));
        }

        if (lifecycleStatusFilter == BidOpsLifecycleReviewStatuses.NotApproved)
        {
            var approvedRawNoticeIds = lifecycleStatuses
                .Where(x => x.Value == BidOpsLifecycleReviewStatuses.Approved)
                .Select(x => x.Key)
                .ToList();
            return approvedRawNoticeIds.Count == 0
                ? builder
                : builder.Where(x => !approvedRawNoticeIds.Contains(x.RawNoticeId));
        }

        var rawNoticeIds = lifecycleStatuses
            .Where(x => x.Value == lifecycleStatusFilter)
            .Select(x => x.Key)
            .ToList();
        return rawNoticeIds.Count == 0
            ? builder.Where(x => x.Id == 0)
            : builder.Where(x => rawNoticeIds.Contains(x.RawNoticeId));
    }

    private async Task EnrichNoticeLifecycleReviewStatusesAsync(
        IReadOnlyCollection<NoticeDto> notices,
        Dictionary<long, string>? knownStatuses,
        CancellationToken ct)
    {
        if (notices.Count == 0)
            return;

        var statuses = knownStatuses;
        if (statuses == null)
        {
            var rawNoticeIds = notices
                .Select(x => x.RawNoticeId)
                .Distinct()
                .ToList();
            var builder = await _lifecycleLinks.QueryDataScopeAsync(
                BidOpsDataResources.LifecyclePackageLink,
                AtlasDataScopeType.AllTenant,
                ct);
            var links = await builder
                .Where(x => x.AwardRawNoticeId.HasValue && rawNoticeIds.Contains(x.AwardRawNoticeId.Value))
                .ToListAsync(ct);
            statuses = BuildLifecycleReviewStatusesByAwardRawNotice(links);
        }

        foreach (var notice in notices)
        {
            notice.LifecycleReviewStatus = statuses.TryGetValue(notice.RawNoticeId, out var status)
                ? status
                : BidOpsLifecycleReviewStatuses.NotAnalyzed;
        }
    }

    private async Task<Dictionary<long, string>> LoadLifecycleReviewStatusesByAwardRawNoticeAsync(CancellationToken ct)
    {
        var builder = await _lifecycleLinks.QueryDataScopeAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);
        var links = await builder
            .Where(x => x.AwardRawNoticeId.HasValue)
            .ToListAsync(ct);
        return BuildLifecycleReviewStatusesByAwardRawNotice(links);
    }

    private static Dictionary<long, string> BuildLifecycleReviewStatusesByAwardRawNotice(
        IEnumerable<LifecyclePackageLink> links)
    {
        return links
            .Where(x => x.AwardRawNoticeId.HasValue)
            .GroupBy(x => x.AwardRawNoticeId!.Value)
            .ToDictionary(
                x => x.Key,
                x => ResolveLifecycleReviewStatus(x.Select(link => link.LinkStatus).ToArray()));
    }

    private static string ResolveLifecycleReviewStatus(IReadOnlyCollection<string> linkStatuses)
    {
        if (linkStatuses.Count == 0)
            return BidOpsLifecycleReviewStatuses.NotAnalyzed;

        var confirmedCount = linkStatuses.Count(x => string.Equals(x, BidOpsLifecycleLinkStatuses.Confirmed, StringComparison.OrdinalIgnoreCase));
        var rejectedCount = linkStatuses.Count(x => string.Equals(x, BidOpsLifecycleLinkStatuses.Rejected, StringComparison.OrdinalIgnoreCase));
        if (confirmedCount == linkStatuses.Count)
            return BidOpsLifecycleReviewStatuses.Approved;
        if (confirmedCount > 0)
            return BidOpsLifecycleReviewStatuses.PartiallyApproved;
        if (rejectedCount == linkStatuses.Count)
            return BidOpsLifecycleReviewStatuses.Rejected;

        return BidOpsLifecycleReviewStatuses.PendingReview;
    }

    private static string NormalizeLifecycleReviewStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim();
        return normalized.ToUpperInvariant() switch
        {
            "NOTANALYZED" => BidOpsLifecycleReviewStatuses.NotAnalyzed,
            "PENDINGREVIEW" => BidOpsLifecycleReviewStatuses.PendingReview,
            "PARTIALLYAPPROVED" => BidOpsLifecycleReviewStatuses.PartiallyApproved,
            "APPROVED" => BidOpsLifecycleReviewStatuses.Approved,
            "REJECTED" => BidOpsLifecycleReviewStatuses.Rejected,
            "NOTAPPROVED" => BidOpsLifecycleReviewStatuses.NotApproved,
            _ => string.Empty
        };
    }

    public async Task<PagedResult<TenderPackageDto>> SearchPackagesAsync(PackageSearchQuery query, CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _packages.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x => x.PackageName.Contains(keyword) || x.PackageNo.Contains(keyword));
        }

        if (query.NoticeId.HasValue)
            builder = builder.Where(x => x.NoticeId == query.NoticeId.Value);

        var total = await builder.CountAsync(ct);
        var items = await builder
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .SelectToListAsync(x => new TenderPackageDto
            {
                Id = x.Id,
                NoticeId = x.NoticeId,
                LotNo = x.LotNo,
                LotName = x.LotName,
                PackageNo = x.PackageNo,
                PackageName = x.PackageName,
                Category = x.Category,
                Quantity = x.Quantity,
                Unit = x.Unit,
                BudgetAmount = x.BudgetAmount,
                MaxPrice = x.MaxPrice,
                DeliveryPlace = x.DeliveryPlace,
                DeliveryPeriod = x.DeliveryPeriod,
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }, ct);

        return new PagedResult<TenderPackageDto>(total, items, pageIndex, pageSize);
    }

    private static NoticeDto MapNotice(Notice notice)
    {
        return new NoticeDto
        {
            Id = notice.Id,
            RawNoticeId = notice.RawNoticeId,
            Title = notice.Title,
            NoticeType = notice.NoticeType,
            ProjectName = notice.ProjectName,
            ProjectCode = notice.ProjectCode,
            BuyerName = notice.BuyerName,
            Region = notice.Region,
            BudgetAmount = notice.BudgetAmount,
            PublishTime = notice.PublishTime,
            BidDeadline = notice.BidDeadline,
            Status = notice.Status,
            CreatedAt = notice.CreatedAt,
            UpdatedAt = notice.UpdatedAt
        };
    }

    public async Task<TenderPackageDto?> GetPackageAsync(long id, CancellationToken ct = default)
    {
        var package = await GetPackageCoreAsync(id, ct);
        if (package == null)
            return null;

        var notice = await GetPackageNoticeAsync(package.NoticeId, ct);
        var requirements = await ListRequirementEntitiesAsync(package.Id, ct);
        var procurementDetails = await ListProcurementDetailEntitiesAsync(package.Id, ct);
        return MapPackage(package, notice, requirements, procurementDetails);
    }

    public async Task<IReadOnlyList<PackageTimelineItemDto>> GetPackageTimelineAsync(long id, CancellationToken ct = default)
    {
        var package = await GetPackageCoreAsync(id, ct);
        if (package == null)
            return Array.Empty<PackageTimelineItemDto>();

        var notice = await GetPackageNoticeAsync(package.NoticeId, ct);
        var requirements = await ListRequirementEntitiesAsync(package.Id, ct);
        var items = new List<PackageTimelineItemDto>();

        if (notice?.PublishTime != null)
        {
            items.Add(new PackageTimelineItemDto
            {
                EventType = "NoticePublished",
                Title = "公告发布",
                Description = string.IsNullOrWhiteSpace(notice.Title) ? "公开公告已发布。" : notice.Title,
                OccurredAt = notice.PublishTime.Value,
                Status = notice.Status
            });
        }

        if (notice != null)
        {
            items.Add(new PackageTimelineItemDto
            {
                EventType = "NoticeApproved",
                Title = "正式公告入库",
                Description = string.IsNullOrWhiteSpace(notice.ProjectName) ? "审核通过后创建正式公告。" : notice.ProjectName,
                OccurredAt = notice.CreatedAt,
                Status = notice.Status
            });
        }

        items.Add(new PackageTimelineItemDto
        {
            EventType = "PackageCreated",
            Title = "包件创建",
            Description = BuildPackageDisplayName(package),
            OccurredAt = package.CreatedAt,
            Status = package.Status
        });

        if (requirements.Count > 0)
        {
            var rejectRiskCount = requirements.Count(x => x.IsRejectRisk);
            items.Add(new PackageTimelineItemDto
            {
                EventType = "RequirementsCreated",
                Title = "要求项生成",
                Description = rejectRiskCount > 0
                    ? $"生成 {requirements.Count} 条要求项，其中 {rejectRiskCount} 条废标风险。"
                    : $"生成 {requirements.Count} 条要求项。",
                OccurredAt = requirements.Min(x => x.CreatedAt),
                Status = "Completed"
            });
        }

        if (package.UpdatedAt.HasValue && package.UpdatedAt.Value > package.CreatedAt.AddSeconds(1))
        {
            items.Add(new PackageTimelineItemDto
            {
                EventType = "PackageUpdated",
                Title = "包件更新",
                Description = "包件基础信息已更新。",
                OccurredAt = package.UpdatedAt.Value,
                Status = package.Status
            });
        }

        return items
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.EventType)
            .ToList();
    }

    public async Task<IReadOnlyList<RequirementItemDto>> ListRequirementsAsync(long packageId, CancellationToken ct = default)
    {
        var requirements = await ListRequirementEntitiesAsync(packageId, ct);
        return requirements.Select(MapRequirement).ToList();
    }

    private async Task<TenderPackage?> GetPackageCoreAsync(long id, CancellationToken ct)
    {
        var builder = await _packages.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        return await builder.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    private async Task<Notice?> GetPackageNoticeAsync(long noticeId, CancellationToken ct)
    {
        var builder = await _notices.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        return await builder.Where(x => x.Id == noticeId).FirstOrDefaultAsync(ct);
    }

    private async Task<List<RequirementItem>> ListRequirementEntitiesAsync(long packageId, CancellationToken ct)
    {
        var builder = await _requirements.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        return await builder
            .Where(x => x.PackageId == packageId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
    }

    private async Task<List<ProcurementDetail>> ListProcurementDetailEntitiesAsync(long packageId, CancellationToken ct)
    {
        var builder = await _procurementDetails.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        var details = await builder
            .Where(x => x.TenderPackageId == packageId)
            .ToListAsync(ct);
        return details
            .OrderBy(x => x.TableIndex ?? int.MaxValue)
            .ThenBy(x => x.RowIndex ?? int.MaxValue)
            .ThenBy(x => x.Id)
            .ToList();
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

    private static ProcurementDetailDto MapProcurementDetail(ProcurementDetail detail)
    {
        return new ProcurementDetailDto
        {
            Id = detail.Id,
            NoticeId = detail.NoticeId,
            TenderPackageId = detail.TenderPackageId,
            ProcurementDetailStagingId = detail.ProcurementDetailStagingId,
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
            Status = detail.Status,
            CreatedAt = detail.CreatedAt,
            UpdatedAt = detail.UpdatedAt
        };
    }

    private static TenderPackageDto MapPackage(
        TenderPackage package,
        Notice? notice,
        IReadOnlyCollection<RequirementItem> requirements,
        IReadOnlyCollection<ProcurementDetail>? procurementDetails = null)
    {
        return new TenderPackageDto
        {
            Id = package.Id,
            NoticeId = package.NoticeId,
            NoticeTitle = notice?.Title ?? string.Empty,
            NoticeType = notice?.NoticeType ?? string.Empty,
            ProjectName = notice?.ProjectName ?? string.Empty,
            ProjectCode = notice?.ProjectCode ?? string.Empty,
            BuyerName = notice?.BuyerName ?? string.Empty,
            Region = notice?.Region ?? string.Empty,
            PublishTime = notice?.PublishTime,
            BidDeadline = notice?.BidDeadline,
            LotNo = package.LotNo,
            LotName = package.LotName,
            PackageNo = package.PackageNo,
            PackageName = package.PackageName,
            Category = package.Category,
            Quantity = package.Quantity,
            Unit = package.Unit,
            BudgetAmount = package.BudgetAmount,
            MaxPrice = package.MaxPrice,
            DeliveryPlace = package.DeliveryPlace,
            DeliveryPeriod = package.DeliveryPeriod,
            Status = package.Status,
            RequirementCount = requirements.Count,
            RejectRiskCount = requirements.Count(x => x.IsRejectRisk),
            ProcurementDetails = procurementDetails?.Select(MapProcurementDetail).ToList() ?? [],
            CreatedAt = package.CreatedAt,
            UpdatedAt = package.UpdatedAt
        };
    }

    private static RequirementItemDto MapRequirement(RequirementItem requirement)
    {
        return new RequirementItemDto
        {
            Id = requirement.Id,
            PackageId = requirement.PackageId,
            RequirementType = requirement.RequirementType,
            OriginalText = requirement.OriginalText,
            IsMandatory = requirement.IsMandatory,
            IsRejectRisk = requirement.IsRejectRisk,
            RequiredEvidenceType = requirement.RequiredEvidenceType,
            RiskLevel = requirement.RiskLevel
        };
    }

    private static string BuildPackageDisplayName(TenderPackage package)
    {
        if (!string.IsNullOrWhiteSpace(package.PackageName) && !string.IsNullOrWhiteSpace(package.PackageNo))
            return $"{package.PackageNo} {package.PackageName}";

        if (!string.IsNullOrWhiteSpace(package.PackageName))
            return package.PackageName;

        return string.IsNullOrWhiteSpace(package.PackageNo) ? $"包件 {package.Id}" : package.PackageNo;
    }

    private static (int PageIndex, int PageSize) NormalizePaging(BidOpsPagedQuery query)
    {
        var pageIndex = query.PageIndex < 1 ? 1 : query.PageIndex;
        var pageSize = query.PageSize < 1 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);
        return (pageIndex, pageSize);
    }

    private static RawNoticeDto Map(RawNotice raw)
    {
        return new RawNoticeDto
        {
            Id = raw.Id,
            SourceId = raw.SourceId,
            ChannelId = raw.ChannelId,
            Title = raw.Title,
            DetailUrl = raw.DetailUrl,
            NoticeType = raw.NoticeType,
            PublishTime = raw.PublishTime,
            FetchTime = raw.FetchTime,
            ContentHash = raw.ContentHash,
            TextPreview = BidOpsRawNoticeTextFormatter.ToDisplayText(raw.TextPreview),
            Status = raw.Status,
            LastError = raw.LastError,
            CreatedAt = raw.CreatedAt,
            UpdatedAt = raw.UpdatedAt
        };
    }

    private static CrawlRunLogDto MapCrawlRunLog(CrawlRunLog log)
    {
        return new CrawlRunLogDto
        {
            Id = log.Id,
            SourceId = log.SourceId,
            ChannelId = log.ChannelId,
            BackgroundJobId = log.BackgroundJobId,
            Operation = log.Operation,
            Status = log.Status,
            Message = log.Message,
            DurationMs = log.DurationMs,
            CreatedAt = log.CreatedAt,
            UpdatedAt = log.UpdatedAt
        };
    }

    private static ReviewTaskDto Map(ReviewTask task)
    {
        return new ReviewTaskDto
        {
            Id = task.Id,
            BizType = task.BizType,
            BizId = task.BizId,
            RawNoticeId = task.RawNoticeId,
            TaskTitle = task.TaskTitle,
            Priority = task.Priority,
            Status = task.Status,
            Decision = task.Decision,
            Remark = task.Remark,
            QualityScore = task.QualityScore,
            RiskLevel = task.RiskLevel.ToString(),
            QualityIssueCount = task.QualityIssueCount,
            HighRiskIssueCount = task.HighRiskIssueCount,
            ReviewRecommendation = task.ReviewRecommendation.ToString(),
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            ReviewedAt = task.ReviewedAt
        };
    }

    private static ReviewQualityIssueDto MapReviewQualityIssue(ReviewQualityIssue issue)
    {
        return new ReviewQualityIssueDto
        {
            Id = issue.Id,
            ReviewTaskId = issue.ReviewTaskId,
            RawNoticeId = issue.RawNoticeId,
            NoticeStagingId = issue.NoticeStagingId,
            PackageStagingId = issue.PackageStagingId,
            OutcomeSupplierRecordId = issue.OutcomeSupplierRecordId,
            ProcurementDetailStagingId = issue.ProcurementDetailStagingId,
            IssueType = issue.IssueType,
            Severity = issue.Severity.ToString(),
            FieldName = issue.FieldName,
            Message = issue.Message,
            EvidenceJson = issue.EvidenceJson,
            IsResolved = issue.IsResolved,
            ResolvedBy = issue.ResolvedBy,
            ResolvedAt = issue.ResolvedAt,
            CreatedAt = issue.CreatedAt
        };
    }

    private static ReviewCorrectionSampleDto MapReviewCorrectionSample(ReviewCorrectionSample sample)
    {
        return new ReviewCorrectionSampleDto
        {
            Id = sample.Id,
            ReviewTaskId = sample.ReviewTaskId,
            RawNoticeId = sample.RawNoticeId,
            NoticeType = sample.NoticeType,
            SourceKind = sample.SourceKind,
            FieldName = sample.FieldName,
            OriginalValue = sample.OriginalValue,
            CorrectedValue = sample.CorrectedValue,
            OriginalHeader = sample.OriginalHeader,
            OriginalRowJson = sample.OriginalRowJson,
            ReviewerPrompt = sample.ReviewerPrompt,
            Reason = sample.Reason,
            CreatedBy = sample.CreatedBy,
            CreatedAt = sample.CreatedAt
        };
    }

    private static bool TryReadBackgroundJobId(string? evidenceJson, out long jobId)
    {
        jobId = 0;
        if (string.IsNullOrWhiteSpace(evidenceJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(evidenceJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("backgroundJobId", out var property) &&
                   property.TryGetInt64(out jobId) &&
                   jobId > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static NoticeStagingDto Map(NoticeStaging notice)
    {
        return new NoticeStagingDto
        {
            Id = notice.Id,
            RawNoticeId = notice.RawNoticeId,
            NoticeType = notice.NoticeType,
            ProjectName = notice.ProjectName,
            ProjectCode = notice.ProjectCode,
            BuyerName = notice.BuyerName,
            AgencyName = notice.AgencyName,
            Region = notice.Region,
            BudgetAmount = notice.BudgetAmount,
            PublishTime = notice.PublishTime,
            SignupDeadline = notice.SignupDeadline,
            BidDeadline = notice.BidDeadline,
            OpenBidTime = notice.OpenBidTime,
            AiConfidence = notice.AiConfidence,
            ReviewStatus = notice.ReviewStatus
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
            AwardAmount = record.AwardAmount,
            ProcurementAgencyServiceFeeAmount = record.ProcurementAgencyServiceFeeAmount,
            ExtractionOrder = record.ExtractionOrder,
            Currency = record.Currency,
            EvidenceText = record.EvidenceText,
            ExtractionConfidence = record.ExtractionConfidence,
            CreatedAt = record.CreatedAt
        };
    }

    private static string NormalizeOutcomeType(string value)
    {
        return value switch
        {
            BidOpsOutcomeTypes.Awarded => BidOpsOutcomeTypes.Awarded,
            BidOpsOutcomeTypes.Shortlisted => BidOpsOutcomeTypes.Shortlisted,
            _ => BidOpsOutcomeTypes.Candidate
        };
    }

    private static decimal ClampConfidence(decimal value)
    {
        if (value < 0m)
            return 0m;
        return value > 1m ? 1m : value;
    }

    private static string FirstMeaningful(params string?[] values)
    {
        foreach (var value in values)
        {
            var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
            if (!string.IsNullOrWhiteSpace(cleaned) && !BidOpsTextQuality.IsUnknownMarker(cleaned))
                return cleaned;
        }

        return string.Empty;
    }

    private static string ClearIfSameMeaningfulValue(string? value, string? other)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        var normalized = NormalizeLooseText(cleaned);
        var otherNormalized = NormalizeLooseText(other);
        return !string.IsNullOrWhiteSpace(normalized) &&
               string.Equals(normalized, otherNormalized, StringComparison.Ordinal)
            ? string.Empty
            : cleaned;
    }

    private static string NormalizeLooseText(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        return new string(cleaned
                .Where(x => !char.IsWhiteSpace(x) && !"　:：,，;；。.!！".Contains(x))
                .ToArray())
            .ToUpperInvariant();
    }

    private static string Truncate(string? value, int maxLength)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
