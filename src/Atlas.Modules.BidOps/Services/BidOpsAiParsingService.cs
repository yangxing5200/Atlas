using System.Text.Json;
using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Staging;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsAiParsingService : IBidOpsAiParsingService
{
    private const int NoticeTypeMaxLength = 64;
    private const int ProjectNameMaxLength = 500;
    private const int ProjectCodeMaxLength = 128;
    private const int OrganizationNameMaxLength = 300;
    private const int RegionMaxLength = 128;
    private const int StorageKeyMaxLength = 1000;
    private const int LotNoMaxLength = 128;
    private const int LotNameMaxLength = 300;
    private const int PackageNoMaxLength = 128;
    private const int PackageNameMaxLength = 500;
    private const int CategoryMaxLength = 128;
    private const int UnitMaxLength = 64;
    private const int DeliveryPlaceMaxLength = 300;
    private const int DeliveryPeriodMaxLength = 200;
    private const int RequirementTypeMaxLength = 128;
    private const int RequirementOriginalTextMaxLength = 256;
    private const int RequiredEvidenceTypeMaxLength = 128;
    private const int RiskLevelMaxLength = 64;
    private const int AiExplanationMaxLength = 1000;
    private const int ReviewTaskTitleMaxLength = 500;

    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<RawAttachment> _rawAttachments;
    private readonly IRepository<NoticeStaging> _noticeStaging;
    private readonly IRepository<PackageStaging> _packageStaging;
    private readonly IRepository<RequirementStaging> _requirementStaging;
    private readonly IRepository<ReviewTask> _reviewTasks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBidOpsAiExtractionService _ai;
    private readonly IBidOpsFileStore _fileStore;
    private readonly IIdGenerator _idGenerator;

    public BidOpsAiParsingService(
        IRepository<RawNotice> rawNotices,
        IRepository<RawAttachment> rawAttachments,
        IRepository<NoticeStaging> noticeStaging,
        IRepository<PackageStaging> packageStaging,
        IRepository<RequirementStaging> requirementStaging,
        IRepository<ReviewTask> reviewTasks,
        IUnitOfWork unitOfWork,
        IBidOpsAiExtractionService ai,
        IBidOpsFileStore fileStore,
        IIdGenerator idGenerator)
    {
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _rawAttachments = rawAttachments ?? throw new ArgumentNullException(nameof(rawAttachments));
        _noticeStaging = noticeStaging ?? throw new ArgumentNullException(nameof(noticeStaging));
        _packageStaging = packageStaging ?? throw new ArgumentNullException(nameof(packageStaging));
        _requirementStaging = requirementStaging ?? throw new ArgumentNullException(nameof(requirementStaging));
        _reviewTasks = reviewTasks ?? throw new ArgumentNullException(nameof(reviewTasks));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _ai = ai ?? throw new ArgumentNullException(nameof(ai));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<long> ParseRawNoticeAsync(long rawNoticeId, CancellationToken ct = default)
    {
        var rawQuery = await _rawNotices.QueryTrackingAsync(ct);
        var raw = await rawQuery.Where(x => x.Id == rawNoticeId).FirstOrDefaultAsync(ct);
        if (raw == null)
            throw new AtlasException($"BidOps raw notice does not exist: {rawNoticeId}");

        var stagingQuery = await _noticeStaging.QueryTrackingAsync(ct);
        var existingStaging = await stagingQuery.Where(x => x.RawNoticeId == rawNoticeId).FirstOrDefaultAsync(ct);
        if (existingStaging != null)
        {
            var taskQuery = await _reviewTasks.QueryTrackingAsync(ct);
            var existingTask = await taskQuery
                .Where(x => x.BizType == "NoticeStaging" && x.BizId == existingStaging.Id)
                .FirstOrDefaultAsync(ct);
            if (raw.Status != RawNoticeStatus.ParseQueued)
                return existingTask?.Id ?? existingStaging.Id;

            return await ReparseExistingStagingAsync(raw, existingStaging, existingTask, ct);
        }

        var aiRequest = await BuildAiRequestAsync(raw, ct);
        var extract = await _ai.ExtractAsync(aiRequest, ct);
        var rawAiOutput = await SaveAiOutputAsync(extract, ct);

        var noticeStaging = new NoticeStaging
        {
            Id = _idGenerator.NextId(),
            RawNoticeId = raw.Id,
            NoticeType = Truncate(extract.NoticeType, NoticeTypeMaxLength),
            ProjectName = Truncate(extract.ProjectName, ProjectNameMaxLength),
            ProjectCode = Truncate(extract.ProjectCode, ProjectCodeMaxLength),
            BuyerName = Truncate(extract.BuyerName, OrganizationNameMaxLength),
            AgencyName = Truncate(extract.AgencyName, OrganizationNameMaxLength),
            Region = Truncate(extract.Region, RegionMaxLength),
            BudgetAmount = extract.BudgetAmount,
            PublishTime = extract.PublishTime ?? raw.PublishTime,
            SignupDeadline = extract.SignupDeadline,
            BidDeadline = extract.BidDeadline,
            OpenBidTime = extract.OpenBidTime,
            AiConfidence = extract.Confidence,
            ReviewStatus = ReviewStatus.Pending,
            RawAiOutputStorageKey = Truncate(rawAiOutput.StorageKey, StorageKeyMaxLength)
        };

        await _noticeStaging.AddAsync(noticeStaging, ct);

        foreach (var package in extract.Packages)
        {
            var packageStaging = new PackageStaging
            {
                Id = _idGenerator.NextId(),
                NoticeStagingId = noticeStaging.Id,
                LotNo = Truncate(package.LotNo, LotNoMaxLength),
                LotName = Truncate(package.LotName, LotNameMaxLength),
                PackageNo = Truncate(package.PackageNo, PackageNoMaxLength),
                PackageName = Truncate(package.PackageName, PackageNameMaxLength),
                Category = Truncate(package.Category, CategoryMaxLength),
                Quantity = package.Quantity,
                Unit = Truncate(package.Unit, UnitMaxLength),
                BudgetAmount = package.BudgetAmount,
                MaxPrice = package.MaxPrice,
                DeliveryPlace = Truncate(package.DeliveryPlace, DeliveryPlaceMaxLength),
                DeliveryPeriod = Truncate(package.DeliveryPeriod, DeliveryPeriodMaxLength),
                AiConfidence = package.Confidence,
                ReviewStatus = ReviewStatus.Pending
            };

            await _packageStaging.AddAsync(packageStaging, ct);

            foreach (var requirement in package.Requirements)
            {
                await _requirementStaging.AddAsync(CreateRequirementStaging(packageStaging.Id, requirement), ct);
            }
        }

        var task = new ReviewTask
        {
            Id = _idGenerator.NextId(),
            BizType = "NoticeStaging",
            BizId = noticeStaging.Id,
            RawNoticeId = raw.Id,
            TaskTitle = BuildReviewTaskTitle(noticeStaging.ProjectName),
            Priority = 0,
            Status = ReviewTaskStatus.Pending
        };
        await _reviewTasks.AddAsync(task, ct);

        raw.Status = RawNoticeStatus.ReviewPending;
        raw.LastError = string.Empty;
        await _unitOfWork.SaveChangesAsync(ct);

        return task.Id;
    }

    private async Task<long> ReparseExistingStagingAsync(
        RawNotice raw,
        NoticeStaging noticeStaging,
        ReviewTask? reviewTask,
        CancellationToken ct)
    {
        var aiRequest = await BuildAiRequestAsync(raw, ct);
        var extract = await _ai.ExtractAsync(aiRequest, ct);
        var rawAiOutput = await SaveAiOutputAsync(extract, ct);

        noticeStaging.NoticeType = Truncate(extract.NoticeType, NoticeTypeMaxLength);
        noticeStaging.ProjectName = Truncate(extract.ProjectName, ProjectNameMaxLength);
        noticeStaging.ProjectCode = Truncate(extract.ProjectCode, ProjectCodeMaxLength);
        noticeStaging.BuyerName = Truncate(extract.BuyerName, OrganizationNameMaxLength);
        noticeStaging.AgencyName = Truncate(extract.AgencyName, OrganizationNameMaxLength);
        noticeStaging.Region = Truncate(extract.Region, RegionMaxLength);
        noticeStaging.BudgetAmount = extract.BudgetAmount;
        noticeStaging.PublishTime = extract.PublishTime ?? raw.PublishTime;
        noticeStaging.SignupDeadline = extract.SignupDeadline;
        noticeStaging.BidDeadline = extract.BidDeadline;
        noticeStaging.OpenBidTime = extract.OpenBidTime;
        noticeStaging.AiConfidence = extract.Confidence;
        noticeStaging.ReviewStatus = ReviewStatus.Pending;
        noticeStaging.ReviewerId = null;
        noticeStaging.ReviewedAt = null;
        noticeStaging.RawAiOutputStorageKey = Truncate(rawAiOutput.StorageKey, StorageKeyMaxLength);

        var packageQuery = await _packageStaging.QueryTrackingAsync(ct);
        var packages = await packageQuery.Where(x => x.NoticeStagingId == noticeStaging.Id).ToListAsync(ct);
        foreach (var package in packages)
        {
            var requirementQuery = await _requirementStaging.QueryTrackingAsync(ct);
            var requirements = await requirementQuery.Where(x => x.PackageStagingId == package.Id).ToListAsync(ct);
            if (requirements.Count > 0)
                await _requirementStaging.RemoveRangeAsync(requirements, ct);
        }

        if (packages.Count > 0)
            await _packageStaging.RemoveRangeAsync(packages, ct);

        await AddExtractedPackagesAsync(noticeStaging.Id, extract, ct);

        reviewTask ??= new ReviewTask
        {
            Id = _idGenerator.NextId(),
            BizType = "NoticeStaging",
            BizId = noticeStaging.Id,
            RawNoticeId = raw.Id
        };

        if (reviewTask.Id == 0)
            reviewTask.Id = _idGenerator.NextId();

        reviewTask.RawNoticeId = raw.Id;
        reviewTask.TaskTitle = BuildReviewTaskTitle(noticeStaging.ProjectName);
        reviewTask.Priority = 0;
        reviewTask.Status = ReviewTaskStatus.Pending;
        reviewTask.Decision = string.Empty;
        reviewTask.Remark = string.Empty;
        reviewTask.ReviewerId = null;
        reviewTask.ReviewedAt = null;

        if (await _reviewTasks.FirstOrDefaultAsync(x => x.Id == reviewTask.Id, ct) == null)
            await _reviewTasks.AddAsync(reviewTask, ct);

        raw.Status = RawNoticeStatus.ReviewPending;
        raw.LastError = string.Empty;
        await _unitOfWork.SaveChangesAsync(ct);
        return reviewTask.Id;
    }

    private async Task AddExtractedPackagesAsync(
        long noticeStagingId,
        BidOpsNoticeExtract extract,
        CancellationToken ct)
    {
        foreach (var package in extract.Packages)
        {
            var packageStaging = new PackageStaging
            {
                Id = _idGenerator.NextId(),
                NoticeStagingId = noticeStagingId,
                LotNo = Truncate(package.LotNo, LotNoMaxLength),
                LotName = Truncate(package.LotName, LotNameMaxLength),
                PackageNo = Truncate(package.PackageNo, PackageNoMaxLength),
                PackageName = Truncate(package.PackageName, PackageNameMaxLength),
                Category = Truncate(package.Category, CategoryMaxLength),
                Quantity = package.Quantity,
                Unit = Truncate(package.Unit, UnitMaxLength),
                BudgetAmount = package.BudgetAmount,
                MaxPrice = package.MaxPrice,
                DeliveryPlace = Truncate(package.DeliveryPlace, DeliveryPlaceMaxLength),
                DeliveryPeriod = Truncate(package.DeliveryPeriod, DeliveryPeriodMaxLength),
                AiConfidence = package.Confidence,
                ReviewStatus = ReviewStatus.Pending
            };

            await _packageStaging.AddAsync(packageStaging, ct);

            foreach (var requirement in package.Requirements)
            {
                await _requirementStaging.AddAsync(CreateRequirementStaging(packageStaging.Id, requirement), ct);
            }
        }
    }

    private async Task<BidOpsNoticeAiExtractionRequest> BuildAiRequestAsync(RawNotice raw, CancellationToken ct)
    {
        var text = string.IsNullOrWhiteSpace(raw.TextContentStorageKey)
            ? raw.TextPreview
            : await TryReadStoredTextAsync(raw.TextContentStorageKey, raw.TextPreview, ct);
        var html = string.IsNullOrWhiteSpace(raw.HtmlSnapshotStorageKey)
            ? string.Empty
            : await TryReadStoredTextAsync(raw.HtmlSnapshotStorageKey, string.Empty, ct);

        var attachmentQuery = await _rawAttachments.QueryAsync(raw.TenantId, ct);
        var attachments = await attachmentQuery
            .Where(x => x.RawNoticeId == raw.Id &&
                        x.TextExtractStatus == TextExtractStatus.Succeeded &&
                        x.TextContentStorageKey != string.Empty)
            .ToListAsync(ct);

        var attachmentInputs = new List<BidOpsAiAttachmentInput>();
        foreach (var attachment in attachments)
        {
            var attachmentText = await TryReadStoredTextAsync(attachment.TextContentStorageKey, string.Empty, ct);
            if (string.IsNullOrWhiteSpace(attachmentText))
                continue;

            attachmentInputs.Add(new BidOpsAiAttachmentInput(
                attachment.FileName,
                attachment.FileType,
                attachment.FileUrl,
                attachment.FileSize,
                attachmentText));
        }

        return new BidOpsNoticeAiExtractionRequest(
            raw.Title,
            raw.NoticeType,
            raw.DetailUrl,
            raw.PublishTime,
            text,
            html,
            attachmentInputs);
    }

    private async Task<string> TryReadStoredTextAsync(
        string storageKey,
        string fallback,
        CancellationToken ct)
    {
        try
        {
            await using var stream = await _fileStore.OpenReadAsync(storageKey, ct);
            using var reader = new StreamReader(stream);
            var text = await reader.ReadToEndAsync(ct);
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException or DirectoryNotFoundException)
        {
            return fallback;
        }
    }

    private async Task<StoredFileInfo> SaveAiOutputAsync(BidOpsNoticeExtract extract, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(extract, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        });
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return await _fileStore.SaveAsync(stream, "structured-ai-output.json", "application/json", ct);
    }

    private RequirementStaging CreateRequirementStaging(
        long packageStagingId,
        BidOpsRequirementExtract requirement)
    {
        return new RequirementStaging
        {
            Id = _idGenerator.NextId(),
            PackageStagingId = packageStagingId,
            RequirementType = Truncate(requirement.RequirementType, RequirementTypeMaxLength),
            OriginalText = Truncate(requirement.OriginalText, RequirementOriginalTextMaxLength),
            SourcePage = requirement.SourcePage,
            IsMandatory = requirement.IsMandatory,
            IsRejectRisk = requirement.IsRejectRisk,
            RequiredEvidenceType = Truncate(requirement.RequiredEvidenceType, RequiredEvidenceTypeMaxLength),
            RiskLevel = Truncate(requirement.RiskLevel, RiskLevelMaxLength),
            AiExplanation = Truncate(requirement.AiExplanation, AiExplanationMaxLength),
            AiConfidence = requirement.Confidence,
            ReviewStatus = ReviewStatus.Pending
        };
    }

    private static string BuildReviewTaskTitle(string projectName)
    {
        return Truncate($"审核标讯：{projectName}", ReviewTaskTitleMaxLength);
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
