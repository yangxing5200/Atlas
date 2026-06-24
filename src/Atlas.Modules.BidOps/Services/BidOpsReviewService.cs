using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsReviewService : IBidOpsReviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<ReviewTask> _reviewTasks;
    private readonly IRepository<NoticeStaging> _noticeStaging;
    private readonly IRepository<PackageStaging> _packageStaging;
    private readonly IRepository<RequirementStaging> _requirementStaging;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<Notice> _notices;
    private readonly IRepository<TenderPackage> _packages;
    private readonly IRepository<RequirementItem> _requirements;
    private readonly IRepository<OutcomeSupplierRecord> _outcomeRecords;
    private readonly IRepository<ReviewCorrectionSample> _correctionSamples;
    private readonly IBidOpsOrganizationMasterDataService _organizationMasterData;
    private readonly IBidOpsOutcomeSupplierExtractionService _outcomeSupplierExtraction;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _jobs;
    private readonly ICurrentIdentity _identity;
    private readonly IIdGenerator _idGenerator;
    private readonly IBidOpsRuntimeControlService _runtimeControl;

    public BidOpsReviewService(
        IRepository<ReviewTask> reviewTasks,
        IRepository<NoticeStaging> noticeStaging,
        IRepository<PackageStaging> packageStaging,
        IRepository<RequirementStaging> requirementStaging,
        IRepository<RawNotice> rawNotices,
        IRepository<Notice> notices,
        IRepository<TenderPackage> packages,
        IRepository<RequirementItem> requirements,
        IRepository<OutcomeSupplierRecord> outcomeRecords,
        IRepository<ReviewCorrectionSample> correctionSamples,
        IBidOpsOrganizationMasterDataService organizationMasterData,
        IBidOpsOutcomeSupplierExtractionService outcomeSupplierExtraction,
        IUnitOfWork unitOfWork,
        IBackgroundJobClient jobs,
        ICurrentIdentity identity,
        IIdGenerator idGenerator,
        IBidOpsRuntimeControlService runtimeControl)
    {
        _reviewTasks = reviewTasks ?? throw new ArgumentNullException(nameof(reviewTasks));
        _noticeStaging = noticeStaging ?? throw new ArgumentNullException(nameof(noticeStaging));
        _packageStaging = packageStaging ?? throw new ArgumentNullException(nameof(packageStaging));
        _requirementStaging = requirementStaging ?? throw new ArgumentNullException(nameof(requirementStaging));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        _outcomeRecords = outcomeRecords ?? throw new ArgumentNullException(nameof(outcomeRecords));
        _correctionSamples = correctionSamples ?? throw new ArgumentNullException(nameof(correctionSamples));
        _organizationMasterData = organizationMasterData ?? throw new ArgumentNullException(nameof(organizationMasterData));
        _outcomeSupplierExtraction = outcomeSupplierExtraction ?? throw new ArgumentNullException(nameof(outcomeSupplierExtraction));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _runtimeControl = runtimeControl ?? throw new ArgumentNullException(nameof(runtimeControl));
    }

    public async Task<NoticeDto> ApproveAsync(
        long reviewTaskId,
        ReviewDecisionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureOutcomeRecordsBeforeApprovalAsync(reviewTaskId, ct);

        return await _unitOfWork.ExecuteInTransactionAsync(
            token => ApproveCoreAsync(reviewTaskId, request, token),
            ct);
    }

    public async Task<BulkReviewTaskActionResultDto> BulkApproveAsync(
        BulkApproveReviewTasksRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ids = NormalizeReviewTaskIds(request.ReviewTaskIds);
        var result = new BulkReviewTaskActionResultDto { RequestedCount = ids.Count };
        if (!TryParseEnum<ReviewQualityRiskLevel>(request.ExpectedRiskLevel, out var expectedRiskLevel))
            expectedRiskLevel = ReviewQualityRiskLevel.Low;

        foreach (var id in ids)
        {
            try
            {
                var task = await GetTaskForUpdateAsync(id, ct);
                if (task.Status is not (ReviewTaskStatus.Pending or ReviewTaskStatus.InReview))
                {
                    AddBulkItem(result, id, false, true, "任务状态不是待审核/审核中，已跳过。");
                    continue;
                }

                if (task.RiskLevel != expectedRiskLevel)
                {
                    AddBulkItem(result, id, false, false, $"当前风险等级为 {task.RiskLevel}，不满足批量确认条件。");
                    continue;
                }

                if (task.HighRiskIssueCount > request.MaxHighRiskIssueCount)
                {
                    AddBulkItem(result, id, false, false, "存在高风险异常，不能批量确认。");
                    continue;
                }

                var noticeStaging = await GetNoticeStagingForUpdateAsync(task.BizId, ct);
                var raw = await GetRawForUpdateAsync(noticeStaging.RawNoticeId, ct);
                var originalStatus = task.Status.ToString();
                await ApproveAsync(id, new ReviewDecisionRequest { Remark = request.Remark }, ct);
                await AddCorrectionSampleAsync(
                    task,
                    noticeStaging,
                    raw,
                    BidOpsReviewCorrectionSourceKinds.BulkApprove,
                    nameof(ReviewTask.Status),
                    originalStatus,
                    ReviewTaskStatus.Approved.ToString(),
                    string.Empty,
                    string.Empty,
                    null,
                    request.Remark ?? "Low risk bulk approval.",
                    ct);
                await _unitOfWork.SaveChangesAsync(ct);
                AddBulkItem(result, id, true, false, "批量确认成功。");
            }
            catch (Exception ex)
            {
                AddBulkItem(result, id, false, false, ex.Message);
            }
        }

        return result;
    }

    public async Task<BulkReviewTaskActionResultDto> BatchReparseAsync(
        BatchReviewTaskReparseRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ids = NormalizeReviewTaskIds(request.ReviewTaskIds);
        var prompt = NormalizeReviewerPrompt(request.Prompt);
        var result = new BulkReviewTaskActionResultDto { RequestedCount = ids.Count };

        foreach (var id in ids)
        {
            try
            {
                var task = await GetTaskForUpdateAsync(id, ct);
                if (task.Status is ReviewTaskStatus.Approved or ReviewTaskStatus.Ignored or ReviewTaskStatus.Merged)
                {
                    AddBulkItem(result, id, false, true, "已完成或已合并任务不能重新解析。");
                    continue;
                }

                var noticeStaging = await GetNoticeStagingForUpdateAsync(task.BizId, ct);
                var raw = await GetRawForUpdateAsync(noticeStaging.RawNoticeId, ct);
                if (raw.Status == RawNoticeStatus.Approved ||
                    await _notices.FirstOrDefaultAsync(x => x.RawNoticeId == raw.Id, ct) != null)
                {
                    AddBulkItem(result, id, false, true, "已入库公告不能重新解析。");
                    continue;
                }

                EnqueueJobDto job;
                if (LooksLikeOutcomeReviewNotice(raw, noticeStaging))
                {
                    job = await EnqueueOutcomeAiReparseAsync(
                        id,
                        new ReviewOutcomeAiReparseRequest { Prompt = prompt },
                        ct);
                }
                else
                {
                    job = await EnqueueRawNoticeReparseAsync(
                        raw.Id,
                        new ReparseRawNoticeRequest
                        {
                            Reason = string.IsNullOrWhiteSpace(request.Reason)
                                ? "Batch AI reviewer prompt."
                                : request.Reason,
                            Prompt = prompt
                        },
                        ct);
                }

                AddBulkItem(result, id, true, false, $"已入队：{job.JobType}", job.JobId, job.JobType);
            }
            catch (Exception ex)
            {
                AddBulkItem(result, id, false, false, ex.Message);
            }
        }

        return result;
    }

    public async Task<EnqueueJobDto> EnqueueReviewQualityBackfillAsync(
        ReviewQualityBackfillRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _runtimeControl.EnsureTasksNotPausedAsync(ct);

        var tenant = RequireTenant();
        var userId = RequireUser();
        var maxItems = Math.Clamp(request.MaxItems <= 0 ? 100 : request.MaxItems, 1, 500);
        var runId = Guid.NewGuid().ToString("N");
        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<ReviewQualityBackfillJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.ReviewQualityBackfill,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = "BidOps review quality backfill",
                TenantId = tenant,
                StoreId = _identity.StoreId,
                DeduplicationKey = $"bidops:review-quality-backfill:{tenant}:{runId}",
                Priority = BidOpsBackgroundJobPriorities.Manual,
                MaxAttempts = 1,
                Payload = new ReviewQualityBackfillJobPayload(
                    tenant,
                    _identity.StoreId,
                    userId,
                    _identity.UserName,
                    maxItems,
                    request.NoticeType,
                    request.RiskLevel,
                    request.DryRun,
                    request.SourceId,
                    request.PauseSourceAware)
            },
            ct);

        return new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists);
    }

    private async Task<NoticeDto> ApproveCoreAsync(
        long reviewTaskId,
        ReviewDecisionRequest request,
        CancellationToken ct)
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
            var existingPackages = await LoadFormalPackagesForUpdateAsync(existingNotice.Id, ct);
            var existingOutcomeRecords = await LoadOutcomeRecordsForUpdateAsync(raw.TenantId, raw.Id, ct);
            await _organizationMasterData.SyncApprovedNoticeOrganizationsAsync(
                raw.TenantId,
                existingNotice,
                raw.DetailUrl,
                existingPackages,
                existingOutcomeRecords,
                ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Map(existingNotice);
        }

        var notice = new Notice
        {
            Id = _idGenerator.NextId(),
            TenantId = raw.TenantId,
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
        var formalPackages = new List<TenderPackage>();

        var stagingPackageQuery = await _packageStaging.QueryTrackingAsync(ct);
        var stagingPackages = await stagingPackageQuery
            .Where(x => x.NoticeStagingId == noticeStaging.Id)
            .ToListAsync(ct);

        foreach (var stagingPackage in stagingPackages)
        {
            var package = new TenderPackage
            {
                Id = _idGenerator.NextId(),
                TenantId = raw.TenantId,
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
            formalPackages.Add(package);
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
                    TenantId = raw.TenantId,
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
        var outcomeRecords = await LoadOutcomeRecordsForUpdateAsync(raw.TenantId, raw.Id, ct);
        await _organizationMasterData.SyncApprovedNoticeOrganizationsAsync(
            raw.TenantId,
            notice,
            raw.DetailUrl,
            formalPackages,
            outcomeRecords,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Map(notice);
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

    public async Task<EnqueueJobDto> EnqueueOutcomeAiReparseAsync(
        long reviewTaskId,
        ReviewOutcomeAiReparseRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _runtimeControl.EnsureTasksNotPausedAsync(ct);

        var tenant = RequireTenant();
        var userId = RequireUser();
        var prompt = NormalizeReviewerPrompt(request.Prompt);

        var task = await GetTaskForUpdateAsync(reviewTaskId, ct);
        if (task.BizType != "NoticeStaging")
            throw new AtlasException($"Unsupported BidOps review task type: {task.BizType}");

        var noticeStaging = await GetNoticeStagingForUpdateAsync(task.BizId, ct);
        var raw = await GetRawForUpdateAsync(noticeStaging.RawNoticeId, ct);
        if (raw.Status == RawNoticeStatus.Approved ||
            await _notices.FirstOrDefaultAsync(x => x.RawNoticeId == raw.Id, ct) != null)
        {
            throw new AtlasException("已入库或已审核通过的公告不能重新调用 DeepSeek 解析，请选择待审核公告或重新导入公开来源。");
        }

        var reparseRunId = Guid.NewGuid().ToString("N");
        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<OutcomeSupplierExtractJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.OutcomeSupplierExtract,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = "BidOps review outcome AI reparse",
                TenantId = tenant,
                StoreId = _identity.StoreId,
                DeduplicationKey = $"bidops:review-outcome-ai-reparse:{tenant}:{raw.Id}:{reparseRunId}",
                Priority = BidOpsBackgroundJobPriorities.Manual,
                MaxAttempts = 3,
                Payload = new OutcomeSupplierExtractJobPayload(
                    tenant,
                    _identity.StoreId,
                    userId,
                    _identity.UserName,
                    raw.Id,
                    prompt,
                    noticeStaging.ProjectCode)
            },
            ct);

        await AddCorrectionSampleAsync(
            task,
            noticeStaging,
            raw,
            BidOpsReviewCorrectionSourceKinds.ReparsePrompt,
            nameof(ReviewOutcomeAiReparseRequest.Prompt),
            string.Empty,
            string.Empty,
            string.Empty,
            BuildBackgroundJobEvidenceJson(result, "OutcomeAiReparse"),
            prompt,
            "Outcome supplier AI reparse prompt.",
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists);
    }

    public async Task<OutcomeSupplierRecordDto> AddOutcomeSupplierRecordAsync(
        long reviewTaskId,
        ReviewOutcomeSupplierRecordEditRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await GetEditableOutcomeContextAsync(reviewTaskId, ct);
        var supplierName = CleanRequired(request.SupplierName, "厂家名称");
        var supplierNameNormalized = Truncate(BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(supplierName), 191);
        if (string.IsNullOrWhiteSpace(supplierNameNormalized))
            throw new AtlasException("厂家名称无效，请重新填写。");

        var outcomeType = NormalizeOutcomeType(request.OutcomeType, context.NoticeStaging.NoticeType);
        var record = new OutcomeSupplierRecord
        {
            Id = _idGenerator.NextId(),
            TenantId = context.Raw.TenantId,
            RawNoticeId = context.Raw.Id,
            SourceUrl = Truncate(context.Raw.DetailUrl, 1500),
            NoticeTitle = Truncate(context.Raw.Title, 500),
            NoticeType = Truncate(context.Raw.NoticeType, 64),
            ProjectName = Truncate(FirstMeaningful(request.ProjectName, context.NoticeStaging.ProjectName), 500),
            ProjectCode = Truncate(FirstMeaningful(request.ProjectCode, context.NoticeStaging.ProjectCode), 128),
            BuyerName = Truncate(FirstMeaningful(request.BuyerName, context.NoticeStaging.BuyerName), 300),
            Region = Truncate(context.NoticeStaging.Region, 128),
            PublishTime = context.NoticeStaging.PublishTime ?? context.Raw.PublishTime,
            LotNo = Truncate(request.LotNo, 128),
            LotName = Truncate(request.LotName, 300),
            PackageNo = Truncate(request.PackageNo, 128),
            PackageName = Truncate(FirstMeaningful(request.PackageName, request.LotName), 500),
            Category = Truncate(request.Category, 128),
            SupplierName = Truncate(supplierName, 300),
            SupplierNameNormalized = supplierNameNormalized,
            OutcomeType = outcomeType,
            Rank = NormalizeRank(request.Rank),
            AwardAmount = NormalizeAmount(request.AwardAmount, "成交/报价金额"),
            ProcurementAgencyServiceFeeAmount = NormalizeAmount(request.ProcurementAgencyServiceFeeAmount, "代理服务费"),
            ExtractionOrder = await GetNextOutcomeExtractionOrderAsync(context.Raw.TenantId, context.Raw.Id, ct),
            Currency = "CNY",
            EvidenceText = Truncate(request.EvidenceText, 2000),
            ExtractionConfidence = 1m
        };
        record.SourceHash = ComputeManualSourceHash(record);

        await _outcomeRecords.AddAsync(record, ct);
        await AddCorrectionSampleAsync(
            context.Task,
            context.NoticeStaging,
            context.Raw,
            BidOpsReviewCorrectionSourceKinds.ManualEdit,
            "OutcomeSupplierRecord",
            string.Empty,
            SerializeOutcomeRecord(record),
            string.Empty,
            string.Empty,
            null,
            "Reviewer added outcome supplier record.",
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapOutcomeRecord(record);
    }

    public async Task<OutcomeSupplierRecordDto> UpdateOutcomeSupplierRecordAsync(
        long reviewTaskId,
        long outcomeRecordId,
        ReviewOutcomeSupplierRecordEditRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await GetEditableOutcomeContextAsync(reviewTaskId, ct);
        var record = await GetOutcomeRecordForUpdateAsync(context.Raw, outcomeRecordId, ct);
        var originalJson = SerializeOutcomeRecord(record);
        var supplierName = CleanRequired(request.SupplierName, "厂家名称");
        var supplierNameNormalized = Truncate(BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(supplierName), 191);
        if (string.IsNullOrWhiteSpace(supplierNameNormalized))
            throw new AtlasException("厂家名称无效，请重新填写。");

        if (!string.Equals(record.SupplierNameNormalized, supplierNameNormalized, StringComparison.OrdinalIgnoreCase))
            record.SupplierId = null;

        var buyerName = Truncate(FirstMeaningful(request.BuyerName, context.NoticeStaging.BuyerName), 300);
        if (!string.Equals(record.BuyerName, buyerName, StringComparison.Ordinal))
            record.BuyerId = null;

        record.ProjectName = Truncate(FirstMeaningful(request.ProjectName, context.NoticeStaging.ProjectName), 500);
        record.ProjectCode = Truncate(FirstMeaningful(request.ProjectCode, context.NoticeStaging.ProjectCode), 128);
        record.BuyerName = buyerName;
        record.Region = Truncate(context.NoticeStaging.Region, 128);
        record.LotNo = Truncate(request.LotNo, 128);
        record.LotName = Truncate(request.LotName, 300);
        record.PackageNo = Truncate(request.PackageNo, 128);
        record.PackageName = Truncate(FirstMeaningful(request.PackageName, request.LotName), 500);
        record.Category = Truncate(request.Category, 128);
        record.SupplierName = Truncate(supplierName, 300);
        record.SupplierNameNormalized = supplierNameNormalized;
        record.OutcomeType = NormalizeOutcomeType(request.OutcomeType, context.NoticeStaging.NoticeType);
        record.Rank = NormalizeRank(request.Rank);
        record.AwardAmount = NormalizeAmount(request.AwardAmount, "成交/报价金额");
        record.ProcurementAgencyServiceFeeAmount = NormalizeAmount(request.ProcurementAgencyServiceFeeAmount, "代理服务费");
        record.Currency = "CNY";
        record.EvidenceText = Truncate(request.EvidenceText, 2000);
        record.ExtractionConfidence = 1m;
        record.SourceHash = ComputeManualSourceHash(record);

        await AddCorrectionSampleAsync(
            context.Task,
            context.NoticeStaging,
            context.Raw,
            BidOpsReviewCorrectionSourceKinds.ManualEdit,
            "OutcomeSupplierRecord",
            originalJson,
            SerializeOutcomeRecord(record),
            string.Empty,
            string.Empty,
            null,
            "Reviewer edited outcome supplier record.",
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapOutcomeRecord(record);
    }

    public async Task DeleteOutcomeSupplierRecordAsync(
        long reviewTaskId,
        long outcomeRecordId,
        CancellationToken ct = default)
    {
        var context = await GetEditableOutcomeContextAsync(reviewTaskId, ct);
        var record = await GetOutcomeRecordForUpdateAsync(context.Raw, outcomeRecordId, ct);
        var originalJson = SerializeOutcomeRecord(record);

        await _outcomeRecords.RemoveAsync(record, ct);
        await AddCorrectionSampleAsync(
            context.Task,
            context.NoticeStaging,
            context.Raw,
            BidOpsReviewCorrectionSourceKinds.ManualEdit,
            "OutcomeSupplierRecord",
            originalJson,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            "Reviewer deleted outcome supplier record.",
            ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<EnqueueJobDto> EnqueueRawNoticeReparseAsync(
        long rawNoticeId,
        ReparseRawNoticeRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _runtimeControl.EnsureTasksNotPausedAsync(ct);

        var tenant = RequireTenant();
        var userId = RequireUser();
        var raw = await GetRawForUpdateAsync(rawNoticeId, ct);
        var reviewerPrompt = NormalizeOptionalReviewerPrompt(request.Prompt);
        if (raw.Status == RawNoticeStatus.Approved ||
            await _notices.FirstOrDefaultAsync(x => x.RawNoticeId == raw.Id, ct) != null)
        {
            throw new AtlasException("已入库或已审核通过的公告不能重新解析，请选择待审核公告或重新导入公开来源。");
        }

        var reason = BuildReparseReason(request.Reason);
        raw.Status = RawNoticeStatus.ParseQueued;
        raw.LastError = reason;

        NoticeStaging? staging = null;
        ReviewTask? task = null;
        var stagingQuery = await _noticeStaging.QueryTrackingAsync(ct);
        staging = await stagingQuery
            .Where(x => x.RawNoticeId == raw.Id)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (staging != null)
        {
            staging.ReviewStatus = ReviewStatus.ReparseRequired;
            staging.ReviewerId = null;
            staging.ReviewedAt = null;

            var taskQuery = await _reviewTasks.QueryTrackingAsync(ct);
            task = await taskQuery
                .Where(x => x.BizType == "NoticeStaging" && x.BizId == staging.Id)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (task != null)
            {
                task.Status = ReviewTaskStatus.ReparseRequired;
                task.Decision = "ReparseRequired";
                task.Remark = reason;
                task.ReviewerId = null;
                task.ReviewedAt = null;
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var reparseRunId = Guid.NewGuid().ToString("N");
        var result = await _jobs.EnqueueAsync(
            new EnqueueBackgroundJobRequest<AttachmentProcessJobPayload>
            {
                JobType = BidOpsBackgroundJobTypes.AttachmentProcess,
                Queue = BidOpsBackgroundJobQueues.BidOps,
                JobName = "BidOps raw notice reparse",
                TenantId = tenant,
                StoreId = _identity.StoreId,
                DeduplicationKey = $"bidops:manual-reparse:{tenant}:{raw.Id}:{reparseRunId}",
                Priority = BidOpsBackgroundJobPriorities.Manual,
                MaxAttempts = 3,
                Payload = new AttachmentProcessJobPayload(
                    tenant,
                    _identity.StoreId,
                    userId,
                    _identity.UserName,
                    raw.Id,
                    reparseRunId,
                    reviewerPrompt,
                    BidOpsJobProjectCode.FirstMeaningful(staging?.ProjectCode, BidOpsJobProjectCode.FromRawNotice(raw)))
            },
            ct);

        if (staging != null && task != null)
        {
            await AddCorrectionSampleAsync(
                task,
                staging,
                raw,
                BidOpsReviewCorrectionSourceKinds.ReparsePrompt,
                nameof(ReparseRawNoticeRequest.Prompt),
                string.Empty,
                string.Empty,
                string.Empty,
                BuildBackgroundJobEvidenceJson(result, "RawNoticeReparse"),
                reviewerPrompt,
                reason,
                ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists);
    }

    private static List<long> NormalizeReviewTaskIds(IEnumerable<long>? reviewTaskIds)
    {
        return (reviewTaskIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .Take(100)
            .ToList();
    }

    private static void AddBulkItem(
        BulkReviewTaskActionResultDto result,
        long reviewTaskId,
        bool succeeded,
        bool skipped,
        string message,
        long? jobId = null,
        string? jobType = null)
    {
        result.Items.Add(new BulkReviewTaskActionItemDto
        {
            ReviewTaskId = reviewTaskId,
            Succeeded = succeeded,
            Skipped = skipped,
            Message = message,
            JobId = jobId,
            JobType = jobType
        });

        if (succeeded)
            result.SucceededCount++;
        else if (skipped)
            result.SkippedCount++;
        else
            result.FailedCount++;
    }

    private async Task AddCorrectionSampleAsync(
        ReviewTask task,
        NoticeStaging noticeStaging,
        RawNotice raw,
        string sourceKind,
        string fieldName,
        string originalValue,
        string correctedValue,
        string originalHeader,
        string originalRowJson,
        string? reviewerPrompt,
        string reason,
        CancellationToken ct)
    {
        await _correctionSamples.AddAsync(new ReviewCorrectionSample
        {
            Id = _idGenerator.NextId(),
            TenantId = raw.TenantId,
            ReviewTaskId = task.Id,
            RawNoticeId = raw.Id,
            NoticeType = Truncate(FirstMeaningful(noticeStaging.NoticeType, raw.NoticeType), 64),
            SourceKind = Truncate(sourceKind, 64),
            FieldName = Truncate(fieldName, 128),
            OriginalValue = originalValue ?? string.Empty,
            CorrectedValue = correctedValue ?? string.Empty,
            OriginalHeader = Truncate(originalHeader, 300),
            OriginalRowJson = originalRowJson ?? string.Empty,
            ReviewerPrompt = reviewerPrompt ?? string.Empty,
            Reason = Truncate(reason, 1000),
            CreatedBy = _identity.UserId,
            CreatedAt = DateTime.UtcNow
        }, ct);
    }

    private static string SerializeOutcomeRecord(OutcomeSupplierRecord record)
    {
        return JsonSerializer.Serialize(new
        {
            record.Id,
            record.RawNoticeId,
            record.ProjectName,
            record.ProjectCode,
            record.BuyerName,
            record.LotNo,
            record.LotName,
            record.PackageNo,
            record.PackageName,
            record.Category,
            record.SupplierName,
            record.OutcomeType,
            record.Rank,
            record.AwardAmount,
            record.ProcurementAgencyServiceFeeAmount,
            record.EvidenceText
        }, JsonOptions);
    }

    private static string BuildBackgroundJobEvidenceJson(
        BackgroundJobEnqueueResult result,
        string action)
    {
        return JsonSerializer.Serialize(new
        {
            backgroundJobId = result.JobId,
            result.JobType,
            result.Queue,
            Status = result.Status.ToString(),
            result.AlreadyExists,
            action
        }, JsonOptions);
    }

    private static bool LooksLikeOutcomeReviewNotice(RawNotice raw, NoticeStaging noticeStaging)
    {
        var text = string.Join(' ', raw.NoticeType, raw.Title, noticeStaging.NoticeType, noticeStaging.ProjectName);
        return text.Contains("Award", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Result", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Candidate", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Shortlist", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("中标", StringComparison.Ordinal) ||
               text.Contains("成交", StringComparison.Ordinal) ||
               text.Contains("结果", StringComparison.Ordinal) ||
               text.Contains("候选", StringComparison.Ordinal) ||
               text.Contains("入围", StringComparison.Ordinal);
    }

    private static bool TryParseEnum<TEnum>(string? value, out TEnum result)
        where TEnum : struct
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value) &&
               Enum.TryParse(value.Trim(), true, out result);
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

    private long RequireTenant()
    {
        return _identity.TenantId
            ?? throw new AtlasException("Tenant context is required for BidOps operations.");
    }

    private long RequireUser()
    {
        return _identity.UserId
            ?? throw new AtlasException("Authenticated user context is required for BidOps operations.");
    }

    private async Task<List<TenderPackage>> LoadFormalPackagesForUpdateAsync(long noticeId, CancellationToken ct)
    {
        var packageQuery = await _packages.QueryTrackingAsync(ct);
        return await packageQuery.Where(x => x.NoticeId == noticeId).ToListAsync(ct);
    }

    private async Task<List<OutcomeSupplierRecord>> LoadOutcomeRecordsForUpdateAsync(
        long tenantId,
        long rawNoticeId,
        CancellationToken ct)
    {
        var outcomeQuery = await _outcomeRecords.QueryTrackingAsync(tenantId, ct);
        var records = await outcomeQuery.Where(x => x.RawNoticeId == rawNoticeId).ToListAsync(ct);
        return records
            .OrderBy(x => x.ExtractionOrder)
            .ThenBy(x => x.Id)
            .ToList();
    }

    private async Task<int> GetNextOutcomeExtractionOrderAsync(long tenantId, long rawNoticeId, CancellationToken ct)
    {
        var outcomeQuery = await _outcomeRecords.QueryAsync(tenantId, ct);
        var records = await outcomeQuery.Where(x => x.RawNoticeId == rawNoticeId).ToListAsync(ct);
        return records.Count == 0 ? 0 : records.Max(x => x.ExtractionOrder) + 1;
    }

    private async Task<EditableOutcomeContext> GetEditableOutcomeContextAsync(long reviewTaskId, CancellationToken ct)
    {
        var task = await GetTaskForUpdateAsync(reviewTaskId, ct);
        if (task.BizType != "NoticeStaging")
            throw new AtlasException($"Unsupported BidOps review task type: {task.BizType}");
        if (task.Status is ReviewTaskStatus.Approved or ReviewTaskStatus.Ignored)
            throw new AtlasException("已完成的审核任务不能编辑解析明细。");

        var noticeStaging = await GetNoticeStagingForUpdateAsync(task.BizId, ct);
        var raw = await GetRawForUpdateAsync(noticeStaging.RawNoticeId, ct);
        if (raw.Status == RawNoticeStatus.Approved ||
            await _notices.FirstOrDefaultAsync(x => x.RawNoticeId == raw.Id, ct) != null)
        {
            throw new AtlasException("Approved BidOps raw notices cannot be edited in MVP.");
        }

        return new EditableOutcomeContext(task, noticeStaging, raw);
    }

    private async Task<OutcomeSupplierRecord> GetOutcomeRecordForUpdateAsync(
        RawNotice raw,
        long outcomeRecordId,
        CancellationToken ct)
    {
        var query = await _outcomeRecords.QueryTrackingAsync(raw.TenantId, ct);
        var record = await query
            .Where(x => x.Id == outcomeRecordId && x.RawNoticeId == raw.Id)
            .FirstOrDefaultAsync(ct);
        if (record == null)
            throw new AtlasException($"BidOps outcome supplier record does not exist for this review task: {outcomeRecordId}");

        return record;
    }

    private async Task EnsureOutcomeRecordsBeforeApprovalAsync(long reviewTaskId, CancellationToken ct)
    {
        var task = await _reviewTasks.GetByIdAsync(reviewTaskId, ct);
        if (task == null || task.BizType != "NoticeStaging")
            return;

        var noticeStaging = await _noticeStaging.GetByIdAsync(task.BizId, ct);
        if (noticeStaging == null)
            return;

        var raw = await _rawNotices.GetByIdAsync(noticeStaging.RawNoticeId, ct);
        if (raw == null)
            return;

        var outcomeQuery = await _outcomeRecords.QueryAsync(raw.TenantId, ct);
        if (await outcomeQuery.Where(x => x.RawNoticeId == raw.Id).AnyAsync(ct))
            return;

        await _outcomeSupplierExtraction.ExtractRawNoticeAsync(raw.Id, ct);
    }

    private static string BuildReparseReason(string? reason)
    {
        var value = string.IsNullOrWhiteSpace(reason)
            ? "Reparse requested by reviewer."
            : $"Reparse requested by reviewer: {reason.Trim()}";
        return value.Length <= 2000 ? value : value[..2000];
    }

    private static string NormalizeReviewerPrompt(string? prompt)
    {
        var value = prompt?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            throw new AtlasException("请输入给 AI 的调整提示词。");

        return value.Length <= 4000 ? value : value[..4000];
    }

    private static string? NormalizeOptionalReviewerPrompt(string? prompt)
    {
        var value = prompt?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Length <= 4000 ? value : value[..4000];
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
            Status = notice.Status,
            CreatedAt = notice.CreatedAt,
            UpdatedAt = notice.UpdatedAt
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

    private static string NormalizeOutcomeType(string? value, string noticeType)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed switch
        {
            BidOpsOutcomeTypes.Awarded => BidOpsOutcomeTypes.Awarded,
            BidOpsOutcomeTypes.Shortlisted => BidOpsOutcomeTypes.Shortlisted,
            BidOpsOutcomeTypes.Candidate => BidOpsOutcomeTypes.Candidate,
            _ when string.Equals(noticeType, "AwardAnnouncement", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(noticeType, "ResultAnnouncement", StringComparison.OrdinalIgnoreCase) => BidOpsOutcomeTypes.Awarded,
            _ => BidOpsOutcomeTypes.Candidate
        };
    }

    private static int? NormalizeRank(int? value)
    {
        return value.HasValue && value.Value > 0 ? value.Value : null;
    }

    private static decimal? NormalizeAmount(decimal? value, string label)
    {
        if (!value.HasValue)
            return null;
        if (value.Value < 0m)
            throw new AtlasException($"{label}不能为负数。");
        return value.Value;
    }

    private static string CleanRequired(string? value, string label)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned) || BidOpsTextQuality.IsUnknownMarker(cleaned))
            throw new AtlasException($"请填写{label}。");
        return cleaned;
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

    private static string Truncate(string? value, int maxLength)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static string ComputeManualSourceHash(OutcomeSupplierRecord record)
    {
        var joined = string.Join(
            '\u001f',
            "manual-review-edit",
            record.RawNoticeId.ToString(),
            record.SupplierNameNormalized,
            record.ProjectCode,
            record.LotNo,
            record.LotName,
            record.PackageNo,
            record.OutcomeType,
            record.Rank?.ToString() ?? string.Empty);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record EditableOutcomeContext(
        ReviewTask Task,
        NoticeStaging NoticeStaging,
        RawNotice Raw);
}
