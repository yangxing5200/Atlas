using System.Text;
using Atlas.Core.Authorization;
using Atlas.Data.Abstractions;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Queries;

public sealed class BidOpsQueryService : IBidOpsQueryService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;
    private const int RawTextContentMaxCharacters = 120_000;
    private const int AttachmentTextContentMaxCharacters = 120_000;
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
    private readonly IRepository<Notice> _notices;
    private readonly IRepository<TenderPackage> _packages;
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
        IRepository<Notice> notices,
        IRepository<TenderPackage> packages,
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
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
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
                LastError = x.LastError
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
            builder = builder.Where(x => x.TaskTitle.Contains(keyword));
        }

        if (query.Status.HasValue)
            builder = builder.Where(x => x.Status == query.Status.Value);

        var total = await builder.CountAsync(ct);
        var tasks = await builder
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        var items = tasks.Select(Map).ToList();
        await EnrichReviewTaskSummariesAsync(items, ct);

        return new PagedResult<ReviewTaskDto>(total, items, pageIndex, pageSize);
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
        }

        var rawDto = raw == null ? null : Map(raw);
        if (rawDto != null && raw != null)
            rawDto.TextContent = await ReadRawTextContentAsync(raw, ct);
        IReadOnlyList<RawAttachmentDto> attachments = raw == null
            ? Array.Empty<RawAttachmentDto>()
            : await ListRawAttachmentsCoreAsync(raw.Id, ct);

        var taskDto = Map(task);
        if (notice != null)
            ApplyReviewTaskSummary(taskDto, notice, packageDtos);

        return new ReviewTaskDetailDto
        {
            Task = taskDto,
            RawNotice = rawDto,
            Notice = notice == null ? null : Map(notice),
            Packages = packageDtos,
            Attachments = attachments.ToList()
        };
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

    public async Task<PagedResult<NoticeDto>> SearchNoticesAsync(BidOpsPagedQuery query, CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _notices.QueryDataScopeAsync(BidOpsDataResources.Notice, AtlasDataScopeType.AllTenant, ct);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x => x.Title.Contains(keyword) || x.ProjectName.Contains(keyword) || x.ProjectCode.Contains(keyword));
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
                Status = x.Status
            }, ct);

        return new PagedResult<NoticeDto>(total, items, pageIndex, pageSize);
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

    public async Task<TenderPackageDto?> GetPackageAsync(long id, CancellationToken ct = default)
    {
        var package = await GetPackageCoreAsync(id, ct);
        if (package == null)
            return null;

        var notice = await GetPackageNoticeAsync(package.NoticeId, ct);
        var requirements = await ListRequirementEntitiesAsync(package.Id, ct);
        return MapPackage(package, notice, requirements);
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

    private static TenderPackageDto MapPackage(
        TenderPackage package,
        Notice? notice,
        IReadOnlyCollection<RequirementItem> requirements)
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
            LastError = raw.LastError
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
            CreatedAt = task.CreatedAt,
            ReviewedAt = task.ReviewedAt
        };
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
}
