using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsReviewService : IBidOpsReviewService
{
    private readonly IRepository<ReviewTask> _reviewTasks;
    private readonly IRepository<NoticeStaging> _noticeStaging;
    private readonly IRepository<PackageStaging> _packageStaging;
    private readonly IRepository<RequirementStaging> _requirementStaging;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<Notice> _notices;
    private readonly IRepository<TenderPackage> _packages;
    private readonly IRepository<RequirementItem> _requirements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentIdentity _identity;
    private readonly IIdGenerator _idGenerator;

    public BidOpsReviewService(
        IRepository<ReviewTask> reviewTasks,
        IRepository<NoticeStaging> noticeStaging,
        IRepository<PackageStaging> packageStaging,
        IRepository<RequirementStaging> requirementStaging,
        IRepository<RawNotice> rawNotices,
        IRepository<Notice> notices,
        IRepository<TenderPackage> packages,
        IRepository<RequirementItem> requirements,
        IUnitOfWork unitOfWork,
        ICurrentIdentity identity,
        IIdGenerator idGenerator)
    {
        _reviewTasks = reviewTasks ?? throw new ArgumentNullException(nameof(reviewTasks));
        _noticeStaging = noticeStaging ?? throw new ArgumentNullException(nameof(noticeStaging));
        _packageStaging = packageStaging ?? throw new ArgumentNullException(nameof(packageStaging));
        _requirementStaging = requirementStaging ?? throw new ArgumentNullException(nameof(requirementStaging));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<NoticeDto> ApproveAsync(
        long reviewTaskId,
        ReviewDecisionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var task = await GetTaskForUpdateAsync(reviewTaskId, ct);
            if (task.BizType != "NoticeStaging")
                throw new AtlasException($"Unsupported BidOps review task type: {task.BizType}");

            var noticeStaging = await GetNoticeStagingForUpdateAsync(task.BizId, ct);
            var raw = await GetRawForUpdateAsync(noticeStaging.RawNoticeId, ct);
            var existingNotice = await _notices.FirstOrDefaultAsync(x => x.RawNoticeId == raw.Id, ct);
            if (existingNotice != null)
            {
                MarkApproved(task, noticeStaging, request.Remark);
                raw.Status = RawNoticeStatus.Approved;
                await _unitOfWork.SaveChangesAsync(ct);
                await _unitOfWork.CommitAsync(ct);
                return Map(existingNotice);
            }

            var notice = new Notice
            {
                Id = _idGenerator.NextId(),
                RawNoticeId = raw.Id,
                NoticeStagingId = noticeStaging.Id,
                Title = raw.Title,
                NoticeType = noticeStaging.NoticeType,
                ProjectName = noticeStaging.ProjectName,
                ProjectCode = noticeStaging.ProjectCode,
                BuyerName = noticeStaging.BuyerName,
                AgencyName = noticeStaging.AgencyName,
                Region = noticeStaging.Region,
                BudgetAmount = noticeStaging.BudgetAmount,
                PublishTime = noticeStaging.PublishTime,
                SignupDeadline = noticeStaging.SignupDeadline,
                BidDeadline = noticeStaging.BidDeadline,
                OpenBidTime = noticeStaging.OpenBidTime,
                Status = "Active"
            };
            await _notices.AddAsync(notice, ct);

            var stagingPackageQuery = await _packageStaging.QueryTrackingAsync(ct);
            var stagingPackages = await stagingPackageQuery
                .Where(x => x.NoticeStagingId == noticeStaging.Id)
                .ToListAsync(ct);

            foreach (var stagingPackage in stagingPackages)
            {
                var package = new TenderPackage
                {
                    Id = _idGenerator.NextId(),
                    NoticeId = notice.Id,
                    PackageStagingId = stagingPackage.Id,
                    LotNo = stagingPackage.LotNo,
                    LotName = stagingPackage.LotName,
                    PackageNo = stagingPackage.PackageNo,
                    PackageName = stagingPackage.PackageName,
                    Category = stagingPackage.Category,
                    Quantity = stagingPackage.Quantity,
                    Unit = stagingPackage.Unit,
                    BudgetAmount = stagingPackage.BudgetAmount,
                    MaxPrice = stagingPackage.MaxPrice,
                    DeliveryPlace = stagingPackage.DeliveryPlace,
                    DeliveryPeriod = stagingPackage.DeliveryPeriod,
                    Status = "New"
                };
                await _packages.AddAsync(package, ct);
                stagingPackage.ReviewStatus = ReviewStatus.Approved;

                var stagingRequirementQuery = await _requirementStaging.QueryTrackingAsync(ct);
                var stagingRequirements = await stagingRequirementQuery
                    .Where(x => x.PackageStagingId == stagingPackage.Id)
                    .ToListAsync(ct);

                foreach (var stagingRequirement in stagingRequirements)
                {
                    await _requirements.AddAsync(new RequirementItem
                    {
                        Id = _idGenerator.NextId(),
                        PackageId = package.Id,
                        RequirementStagingId = stagingRequirement.Id,
                        RequirementType = stagingRequirement.RequirementType,
                        OriginalText = stagingRequirement.OriginalText,
                        SourceFileId = stagingRequirement.SourceFileId,
                        SourcePage = stagingRequirement.SourcePage,
                        IsMandatory = stagingRequirement.IsMandatory,
                        IsRejectRisk = stagingRequirement.IsRejectRisk,
                        RequiredEvidenceType = stagingRequirement.RequiredEvidenceType,
                        RiskLevel = stagingRequirement.RiskLevel,
                        AiExplanation = stagingRequirement.AiExplanation
                    }, ct);
                    stagingRequirement.ReviewStatus = ReviewStatus.Approved;
                }
            }

            MarkApproved(task, noticeStaging, request.Remark);
            raw.Status = RawNoticeStatus.Approved;
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Map(notice);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task IgnoreAsync(
        long reviewTaskId,
        ReviewDecisionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var task = await GetTaskForUpdateAsync(reviewTaskId, ct);
        if (task.BizType != "NoticeStaging")
            throw new AtlasException($"Unsupported BidOps review task type: {task.BizType}");

        var noticeStaging = await GetNoticeStagingForUpdateAsync(task.BizId, ct);
        var raw = await GetRawForUpdateAsync(noticeStaging.RawNoticeId, ct);

        var now = DateTime.UtcNow;
        task.Status = ReviewTaskStatus.Ignored;
        task.Decision = "Ignored";
        task.Remark = request.Remark?.Trim() ?? string.Empty;
        task.ReviewerId = _identity.UserId;
        task.ReviewedAt = now;
        noticeStaging.ReviewStatus = ReviewStatus.Ignored;
        noticeStaging.ReviewerId = _identity.UserId;
        noticeStaging.ReviewedAt = now;
        raw.Status = RawNoticeStatus.Ignored;

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<ReviewTask> GetTaskForUpdateAsync(long reviewTaskId, CancellationToken ct)
    {
        var taskQuery = await _reviewTasks.QueryTrackingAsync(ct);
        var task = await taskQuery.Where(x => x.Id == reviewTaskId).FirstOrDefaultAsync(ct);
        if (task == null)
            throw new AtlasException($"BidOps review task does not exist: {reviewTaskId}");

        return task;
    }

    private async Task<NoticeStaging> GetNoticeStagingForUpdateAsync(long id, CancellationToken ct)
    {
        var stagingQuery = await _noticeStaging.QueryTrackingAsync(ct);
        var staging = await stagingQuery.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (staging == null)
            throw new AtlasException($"BidOps notice staging does not exist: {id}");

        return staging;
    }

    private async Task<RawNotice> GetRawForUpdateAsync(long id, CancellationToken ct)
    {
        var rawQuery = await _rawNotices.QueryTrackingAsync(ct);
        var raw = await rawQuery.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (raw == null)
            throw new AtlasException($"BidOps raw notice does not exist: {id}");

        return raw;
    }

    private void MarkApproved(
        ReviewTask task,
        NoticeStaging noticeStaging,
        string? remark)
    {
        var now = DateTime.UtcNow;
        task.Status = ReviewTaskStatus.Approved;
        task.Decision = "Approved";
        task.Remark = remark?.Trim() ?? string.Empty;
        task.ReviewerId = _identity.UserId;
        task.ReviewedAt = now;
        noticeStaging.ReviewStatus = ReviewStatus.Approved;
        noticeStaging.ReviewerId = _identity.UserId;
        noticeStaging.ReviewedAt = now;
    }

    private static NoticeDto Map(Notice notice)
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
            Status = notice.Status
        };
    }
}
