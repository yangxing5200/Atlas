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

        var text = await ReadRawTextAsync(raw, ct);
        var extract = await _ai.ExtractAsync(raw.Title, text, ct);
        var rawAiOutput = await SaveAiOutputAsync(extract, ct);

        var noticeStaging = new NoticeStaging
        {
            Id = _idGenerator.NextId(),
            RawNoticeId = raw.Id,
            NoticeType = extract.NoticeType,
            ProjectName = extract.ProjectName,
            ProjectCode = extract.ProjectCode,
            BuyerName = extract.BuyerName,
            AgencyName = extract.AgencyName,
            Region = extract.Region,
            BudgetAmount = extract.BudgetAmount,
            PublishTime = extract.PublishTime ?? raw.PublishTime,
            SignupDeadline = extract.SignupDeadline,
            BidDeadline = extract.BidDeadline,
            OpenBidTime = extract.OpenBidTime,
            AiConfidence = extract.Confidence,
            ReviewStatus = ReviewStatus.Pending,
            RawAiOutputStorageKey = rawAiOutput.StorageKey
        };

        await _noticeStaging.AddAsync(noticeStaging, ct);

        foreach (var package in extract.Packages)
        {
            var packageStaging = new PackageStaging
            {
                Id = _idGenerator.NextId(),
                NoticeStagingId = noticeStaging.Id,
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
                AiConfidence = package.Confidence,
                ReviewStatus = ReviewStatus.Pending
            };

            await _packageStaging.AddAsync(packageStaging, ct);

            foreach (var requirement in package.Requirements)
            {
                await _requirementStaging.AddAsync(new RequirementStaging
                {
                    Id = _idGenerator.NextId(),
                    PackageStagingId = packageStaging.Id,
                    RequirementType = requirement.RequirementType,
                    OriginalText = requirement.OriginalText,
                    SourcePage = requirement.SourcePage,
                    IsMandatory = requirement.IsMandatory,
                    IsRejectRisk = requirement.IsRejectRisk,
                    RequiredEvidenceType = requirement.RequiredEvidenceType,
                    RiskLevel = requirement.RiskLevel,
                    AiExplanation = requirement.AiExplanation,
                    AiConfidence = requirement.Confidence,
                    ReviewStatus = ReviewStatus.Pending
                }, ct);
            }
        }

        var task = new ReviewTask
        {
            Id = _idGenerator.NextId(),
            BizType = "NoticeStaging",
            BizId = noticeStaging.Id,
            RawNoticeId = raw.Id,
            TaskTitle = $"审核标讯：{noticeStaging.ProjectName}",
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
        var text = await ReadRawTextAsync(raw, ct);
        var extract = await _ai.ExtractAsync(raw.Title, text, ct);
        var rawAiOutput = await SaveAiOutputAsync(extract, ct);

        noticeStaging.NoticeType = extract.NoticeType;
        noticeStaging.ProjectName = extract.ProjectName;
        noticeStaging.ProjectCode = extract.ProjectCode;
        noticeStaging.BuyerName = extract.BuyerName;
        noticeStaging.AgencyName = extract.AgencyName;
        noticeStaging.Region = extract.Region;
        noticeStaging.BudgetAmount = extract.BudgetAmount;
        noticeStaging.PublishTime = extract.PublishTime ?? raw.PublishTime;
        noticeStaging.SignupDeadline = extract.SignupDeadline;
        noticeStaging.BidDeadline = extract.BidDeadline;
        noticeStaging.OpenBidTime = extract.OpenBidTime;
        noticeStaging.AiConfidence = extract.Confidence;
        noticeStaging.ReviewStatus = ReviewStatus.Pending;
        noticeStaging.ReviewerId = null;
        noticeStaging.ReviewedAt = null;
        noticeStaging.RawAiOutputStorageKey = rawAiOutput.StorageKey;

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
        reviewTask.TaskTitle = $"审核标讯：{noticeStaging.ProjectName}";
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
                AiConfidence = package.Confidence,
                ReviewStatus = ReviewStatus.Pending
            };

            await _packageStaging.AddAsync(packageStaging, ct);

            foreach (var requirement in package.Requirements)
            {
                await _requirementStaging.AddAsync(new RequirementStaging
                {
                    Id = _idGenerator.NextId(),
                    PackageStagingId = packageStaging.Id,
                    RequirementType = requirement.RequirementType,
                    OriginalText = requirement.OriginalText,
                    SourcePage = requirement.SourcePage,
                    IsMandatory = requirement.IsMandatory,
                    IsRejectRisk = requirement.IsRejectRisk,
                    RequiredEvidenceType = requirement.RequiredEvidenceType,
                    RiskLevel = requirement.RiskLevel,
                    AiExplanation = requirement.AiExplanation,
                    AiConfidence = requirement.Confidence,
                    ReviewStatus = ReviewStatus.Pending
                }, ct);
            }
        }
    }

    private async Task<string> ReadRawTextAsync(RawNotice raw, CancellationToken ct)
    {
        var builder = new System.Text.StringBuilder();
        if (string.IsNullOrWhiteSpace(raw.TextContentStorageKey))
        {
            builder.AppendLine(raw.TextPreview);
        }
        else
        {
            await using var stream = await _fileStore.OpenReadAsync(raw.TextContentStorageKey, ct);
            using var reader = new StreamReader(stream);
            var text = await reader.ReadToEndAsync(ct);
            builder.AppendLine(string.IsNullOrWhiteSpace(text) ? raw.TextPreview : text);
        }

        var attachmentQuery = await _rawAttachments.QueryAsync(ct);
        var attachments = await attachmentQuery
            .Where(x => x.RawNoticeId == raw.Id &&
                        x.TextExtractStatus == TextExtractStatus.Succeeded &&
                        x.TextContentStorageKey != string.Empty)
            .ToListAsync(ct);
        foreach (var attachment in attachments)
        {
            await using var attachmentStream = await _fileStore.OpenReadAsync(attachment.TextContentStorageKey, ct);
            using var attachmentReader = new StreamReader(attachmentStream);
            var attachmentText = await attachmentReader.ReadToEndAsync(ct);
            if (string.IsNullOrWhiteSpace(attachmentText))
                continue;

            builder.AppendLine();
            builder.AppendLine($"Attachment: {attachment.FileName}");
            builder.AppendLine(attachmentText);
        }

        return builder.ToString();
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
}
