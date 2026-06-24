using System.Text.Json;
using Atlas.Core.IdGenerators;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsReviewQualityService : IBidOpsReviewQualityService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<ReviewQualityIssue> _issues;
    private readonly IRepository<CrawlSource> _sources;
    private readonly IRepository<ReviewTask> _reviewTasks;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<NoticeStaging> _noticeStaging;
    private readonly IRepository<PackageStaging> _packageStaging;
    private readonly IRepository<RequirementStaging> _requirementStaging;
    private readonly IRepository<OutcomeSupplierRecord> _outcomeRecords;
    private readonly IRepository<ProcurementDetailStaging> _procurementDetailStaging;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdGenerator _idGenerator;

    public BidOpsReviewQualityService(
        IRepository<ReviewQualityIssue> issues,
        IRepository<CrawlSource> sources,
        IRepository<ReviewTask> reviewTasks,
        IRepository<RawNotice> rawNotices,
        IRepository<NoticeStaging> noticeStaging,
        IRepository<PackageStaging> packageStaging,
        IRepository<RequirementStaging> requirementStaging,
        IRepository<OutcomeSupplierRecord> outcomeRecords,
        IRepository<ProcurementDetailStaging> procurementDetailStaging,
        IUnitOfWork unitOfWork,
        IIdGenerator idGenerator)
    {
        _issues = issues ?? throw new ArgumentNullException(nameof(issues));
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _reviewTasks = reviewTasks ?? throw new ArgumentNullException(nameof(reviewTasks));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _noticeStaging = noticeStaging ?? throw new ArgumentNullException(nameof(noticeStaging));
        _packageStaging = packageStaging ?? throw new ArgumentNullException(nameof(packageStaging));
        _requirementStaging = requirementStaging ?? throw new ArgumentNullException(nameof(requirementStaging));
        _outcomeRecords = outcomeRecords ?? throw new ArgumentNullException(nameof(outcomeRecords));
        _procurementDetailStaging = procurementDetailStaging ?? throw new ArgumentNullException(nameof(procurementDetailStaging));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task ApplyNoticeQualityAsync(
        ReviewTask task,
        NoticeStaging notice,
        IReadOnlyCollection<PackageStaging> packages,
        IReadOnlyCollection<RequirementStaging> requirements,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(notice);

        var procurementDetailQuery = await _procurementDetailStaging.QueryTrackingAsync(ct);
        var procurementDetails = await procurementDetailQuery
            .Where(x => x.NoticeStagingId == notice.Id)
            .ToListAsync(ct);
        var evaluation = BidOpsReviewQualityEvaluator.EvaluateNotice(
            notice,
            packages,
            requirements,
            procurementDetails);

        await ApplyEvaluationAsync(
            task,
            notice.RawNoticeId,
            notice.Id,
            evaluation,
            ct);
    }

    public async Task ApplyOutcomeQualityAsync(
        ReviewTask task,
        RawNotice raw,
        NoticeStaging? notice,
        IReadOnlyCollection<OutcomeSupplierRecord> outcomeRecords,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(raw);

        IReadOnlyCollection<PackageStaging> packages = [];
        if (notice != null)
        {
            var packageQuery = await _packageStaging.QueryTrackingAsync(ct);
            packages = await packageQuery
                .Where(x => x.NoticeStagingId == notice.Id)
                .ToListAsync(ct);
        }

        var evaluation = BidOpsReviewQualityEvaluator.EvaluateOutcomeNotice(
            raw,
            notice,
            outcomeRecords,
            packages);

        await ApplyEvaluationAsync(
            task,
            raw.Id,
            notice?.Id ?? task.BizId,
            evaluation,
            ct);
    }

    public async Task<ReviewQualityBackfillResultDto> BackfillReviewQualityAsync(
        ReviewQualityBackfillRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var maxItems = Math.Clamp(request.MaxItems <= 0 ? 100 : request.MaxItems, 1, 500);
        var result = new ReviewQualityBackfillResultDto
        {
            RequestedMaxItems = maxItems,
            DryRun = request.DryRun
        };

        var taskQuery = await _reviewTasks.QueryTrackingAsync(ct);
        var tasks = await taskQuery
            .Where(x =>
                x.BizType == "NoticeStaging" &&
                (x.Status == ReviewTaskStatus.Pending ||
                 x.Status == ReviewTaskStatus.InReview ||
                 x.Status == ReviewTaskStatus.ReparseRequired))
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(maxItems * 3)
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            if (result.CandidateCount >= maxItems)
                break;

            result.ScannedCount++;
            var notice = await _noticeStaging.GetByIdAsync(task.BizId, ct);
            if (notice == null)
            {
                result.SkippedCount++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(request.NoticeType) &&
                !string.Equals(notice.NoticeType, request.NoticeType.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                result.SkippedCount++;
                continue;
            }

            if (TryParseEnum<ReviewQualityRiskLevel>(request.RiskLevel, out var filterRisk) &&
                task.RiskLevel != filterRisk)
            {
                result.SkippedCount++;
                continue;
            }

            var raw = await _rawNotices.GetByIdAsync(notice.RawNoticeId, ct);
            if (raw == null)
            {
                result.SkippedCount++;
                continue;
            }

            if (request.SourceId.HasValue && raw.SourceId != request.SourceId.Value)
            {
                result.SkippedCount++;
                continue;
            }

            if (request.PauseSourceAware && await IsPausedSourceAsync(raw.SourceId, ct))
            {
                result.SkippedCount++;
                continue;
            }

            var evaluation = await EvaluateTaskAsync(raw, notice, ct);
            result.CandidateCount++;
            result.Samples.Add(BuildBackfillSample(task, raw, notice, evaluation, !request.DryRun, string.Empty));
            if (request.DryRun)
                continue;

            await ApplyEvaluationAsync(task, raw.Id, notice.Id, evaluation, ct);
            result.UpdatedCount++;
        }

        if (!request.DryRun && result.UpdatedCount > 0)
            await _unitOfWork.SaveChangesAsync(ct);

        return result;
    }

    private async Task<ReviewQualityEvaluation> EvaluateTaskAsync(
        RawNotice raw,
        NoticeStaging notice,
        CancellationToken ct)
    {
        var packageQuery = await _packageStaging.QueryTrackingAsync(ct);
        var packages = await packageQuery
            .Where(x => x.NoticeStagingId == notice.Id)
            .ToListAsync(ct);

        var outcomeQuery = await _outcomeRecords.QueryTrackingAsync(raw.TenantId, ct);
        var outcomeRecords = await outcomeQuery
            .Where(x => x.RawNoticeId == raw.Id)
            .ToListAsync(ct);

        if (LooksLikeOutcomeNotice(raw, notice, outcomeRecords))
            return BidOpsReviewQualityEvaluator.EvaluateOutcomeNotice(raw, notice, outcomeRecords, packages);

        var packageIds = packages.Select(x => x.Id).ToArray();
        var requirementQuery = await _requirementStaging.QueryTrackingAsync(ct);
        var requirements = packageIds.Length == 0
            ? []
            : await requirementQuery.Where(x => packageIds.Contains(x.PackageStagingId)).ToListAsync(ct);
        var procurementDetailQuery = await _procurementDetailStaging.QueryTrackingAsync(ct);
        var procurementDetails = await procurementDetailQuery
            .Where(x => x.NoticeStagingId == notice.Id)
            .ToListAsync(ct);
        return BidOpsReviewQualityEvaluator.EvaluateNotice(
            notice,
            packages,
            requirements,
            procurementDetails);
    }

    private async Task ApplyEvaluationAsync(
        ReviewTask task,
        long rawNoticeId,
        long noticeStagingId,
        ReviewQualityEvaluation evaluation,
        CancellationToken ct)
    {
        var tenantId = task.TenantId;
        task.QualityScore = evaluation.QualityScore;
        task.RiskLevel = evaluation.RiskLevel;
        task.QualityIssueCount = evaluation.QualityIssueCount;
        task.HighRiskIssueCount = evaluation.HighRiskIssueCount;
        task.ReviewRecommendation = evaluation.ReviewRecommendation;

        var issueQuery = await _issues.QueryTrackingAsync(tenantId, ct);
        var existingIssues = await issueQuery
            .Where(x => x.ReviewTaskId == task.Id)
            .ToListAsync(ct);
        if (existingIssues.Count > 0)
            await _issues.RemoveRangeAsync(existingIssues, tenantId, ct);

        foreach (var issue in evaluation.Issues)
        {
            await _issues.AddAsync(new ReviewQualityIssue
            {
                Id = _idGenerator.NextId(),
                ReviewTaskId = task.Id,
                RawNoticeId = rawNoticeId,
                NoticeStagingId = noticeStagingId,
                PackageStagingId = issue.PackageStagingId,
                OutcomeSupplierRecordId = issue.OutcomeSupplierRecordId,
                ProcurementDetailStagingId = issue.ProcurementDetailStagingId,
                IssueType = issue.IssueType,
                Severity = issue.Severity,
                FieldName = issue.FieldName,
                Message = issue.Message,
                EvidenceJson = SerializeEvidence(issue.Evidence)
            }, tenantId, ct);
        }
    }

    private async Task<bool> IsPausedSourceAsync(long sourceId, CancellationToken ct)
    {
        var source = await _sources.GetByIdAsync(sourceId, ct);
        return source != null && !source.Enabled;
    }

    private static ReviewQualityBackfillSampleDto BuildBackfillSample(
        ReviewTask task,
        RawNotice raw,
        NoticeStaging notice,
        ReviewQualityEvaluation evaluation,
        bool updated,
        string message)
    {
        return new ReviewQualityBackfillSampleDto
        {
            ReviewTaskId = task.Id,
            RawNoticeId = raw.Id,
            NoticeType = notice.NoticeType,
            BeforeQualityScore = task.QualityScore,
            BeforeRiskLevel = task.RiskLevel.ToString(),
            AfterQualityScore = evaluation.QualityScore,
            AfterRiskLevel = evaluation.RiskLevel.ToString(),
            IssueCount = evaluation.QualityIssueCount,
            Updated = updated,
            Message = message
        };
    }

    private static bool LooksLikeOutcomeNotice(
        RawNotice raw,
        NoticeStaging notice,
        IReadOnlyCollection<OutcomeSupplierRecord> outcomeRecords)
    {
        if (outcomeRecords.Count > 0)
            return true;

        var text = string.Join(' ', raw.NoticeType, raw.Title, notice.NoticeType, notice.ProjectName);
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

    private static string SerializeEvidence(IReadOnlyDictionary<string, object?>? evidence)
    {
        return evidence == null || evidence.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(evidence, JsonOptions);
    }
}
