using Atlas.Core.Authorization;
using Atlas.Data.Abstractions;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Queries;

public sealed class BidOpsQueryService : IBidOpsQueryService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    private readonly IRepository<CrawlSource> _sources;
    private readonly IRepository<CrawlChannel> _channels;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<ReviewTask> _reviewTasks;
    private readonly IRepository<NoticeStaging> _noticeStaging;
    private readonly IRepository<PackageStaging> _packageStaging;
    private readonly IRepository<RequirementStaging> _requirementStaging;
    private readonly IRepository<Notice> _notices;
    private readonly IRepository<TenderPackage> _packages;
    private readonly IRepository<RequirementItem> _requirements;

    public BidOpsQueryService(
        IRepository<CrawlSource> sources,
        IRepository<CrawlChannel> channels,
        IRepository<RawNotice> rawNotices,
        IRepository<ReviewTask> reviewTasks,
        IRepository<NoticeStaging> noticeStaging,
        IRepository<PackageStaging> packageStaging,
        IRepository<RequirementStaging> requirementStaging,
        IRepository<Notice> notices,
        IRepository<TenderPackage> packages,
        IRepository<RequirementItem> requirements)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _reviewTasks = reviewTasks ?? throw new ArgumentNullException(nameof(reviewTasks));
        _noticeStaging = noticeStaging ?? throw new ArgumentNullException(nameof(noticeStaging));
        _packageStaging = packageStaging ?? throw new ArgumentNullException(nameof(packageStaging));
        _requirementStaging = requirementStaging ?? throw new ArgumentNullException(nameof(requirementStaging));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
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
        var items = await builder
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .SelectToListAsync(x => new ReviewTaskDto
            {
                Id = x.Id,
                BizType = x.BizType,
                BizId = x.BizId,
                RawNoticeId = x.RawNoticeId,
                TaskTitle = x.TaskTitle,
                Priority = x.Priority,
                Status = x.Status,
                Decision = x.Decision,
                Remark = x.Remark,
                CreatedAt = x.CreatedAt,
                ReviewedAt = x.ReviewedAt
            }, ct);

        return new PagedResult<ReviewTaskDto>(total, items, pageIndex, pageSize);
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

        return new ReviewTaskDetailDto
        {
            Task = Map(task),
            RawNotice = raw == null ? null : Map(raw),
            Notice = notice == null ? null : Map(notice),
            Packages = packageDtos
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
                PackageNo = x.PackageNo,
                PackageName = x.PackageName,
                Category = x.Category,
                BudgetAmount = x.BudgetAmount,
                MaxPrice = x.MaxPrice,
                DeliveryPlace = x.DeliveryPlace,
                DeliveryPeriod = x.DeliveryPeriod,
                Status = x.Status
            }, ct);

        return new PagedResult<TenderPackageDto>(total, items, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<RequirementItemDto>> ListRequirementsAsync(long packageId, CancellationToken ct = default)
    {
        var builder = await _requirements.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        return await builder
            .Where(x => x.PackageId == packageId)
            .OrderBy(x => x.Id)
            .SelectToListAsync(x => new RequirementItemDto
            {
                Id = x.Id,
                PackageId = x.PackageId,
                RequirementType = x.RequirementType,
                OriginalText = x.OriginalText,
                IsMandatory = x.IsMandatory,
                IsRejectRisk = x.IsRejectRisk,
                RequiredEvidenceType = x.RequiredEvidenceType,
                RiskLevel = x.RiskLevel
            }, ct);
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
            TextPreview = raw.TextPreview,
            Status = raw.Status,
            LastError = raw.LastError
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
