using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Atlas.Core.Authorization;
using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsAmountCandidateService : IBidOpsAmountCandidateService
{
    private const int StoredTextReadLimit = 220_000;
    private const decimal MaxStoredAmountCandidateValue = 999_999_999_999.999999m;
    private static readonly Regex OutcomeServiceFeeTailAmountRegex = new(
        @"(?:^|[\s|])(?<amount>\d{1,9}(?:\.\d{1,6})?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IRepository<AmountCandidate> _amountCandidates;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<RawAttachment> _rawAttachments;
    private readonly IRepository<OutcomeSupplierRecord> _outcomeRecords;
    private readonly IRepository<ProcurementDetailStaging> _procurementDetails;
    private readonly IRepository<LifecyclePackageLink> _lifecycleLinks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentIdentity _current;
    private readonly IIdGenerator _idGenerator;
    private readonly IBidOpsFileStore _fileStore;
    private readonly ILogger<BidOpsAmountCandidateService> _logger;

    public BidOpsAmountCandidateService(
        IRepository<AmountCandidate> amountCandidates,
        IRepository<RawNotice> rawNotices,
        IRepository<RawAttachment> rawAttachments,
        IRepository<OutcomeSupplierRecord> outcomeRecords,
        IRepository<ProcurementDetailStaging> procurementDetails,
        IRepository<LifecyclePackageLink> lifecycleLinks,
        IUnitOfWork unitOfWork,
        ICurrentIdentity current,
        IIdGenerator idGenerator,
        IBidOpsFileStore fileStore,
        ILogger<BidOpsAmountCandidateService> logger)
    {
        _amountCandidates = amountCandidates ?? throw new ArgumentNullException(nameof(amountCandidates));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _rawAttachments = rawAttachments ?? throw new ArgumentNullException(nameof(rawAttachments));
        _outcomeRecords = outcomeRecords ?? throw new ArgumentNullException(nameof(outcomeRecords));
        _procurementDetails = procurementDetails ?? throw new ArgumentNullException(nameof(procurementDetails));
        _lifecycleLinks = lifecycleLinks ?? throw new ArgumentNullException(nameof(lifecycleLinks));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _current = current ?? throw new ArgumentNullException(nameof(current));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<AmountCandidateDto>> EnsureRawNoticeAmountCandidatesAsync(
        long rawNoticeId,
        CancellationToken ct = default)
    {
        var raw = await _rawNotices.GetByIdAsync(rawNoticeId, ct)
                  ?? throw new AtlasException($"BidOps raw notice does not exist: {rawNoticeId}");
        var drafts = await BuildRawNoticeCandidateDraftsAsync(raw, null, null, ct);
        await RemoveStaleOutcomeRecordCandidatesAsync([raw.Id], ct);
        await UpsertDraftsAsync(drafts, ct);
        var candidates = await LoadCandidatesForRawNoticesAsync([raw.Id], ct);
        return OrderCandidates(candidates).Select(MapAmountCandidate).ToList();
    }

    public async Task<IReadOnlyDictionary<long, IReadOnlyList<AmountCandidateDto>>> EnsureLifecycleAmountCandidatesAsync(
        IReadOnlyCollection<LifecyclePackageLinkDto> links,
        CancellationToken ct = default)
    {
        if (links.Count == 0)
            return new Dictionary<long, IReadOnlyList<AmountCandidateDto>>();

        var rawIds = links
            .SelectMany(RelatedRawNoticeIds)
            .Distinct()
            .ToArray();
        var rawById = await LoadRawNoticesAsync(rawIds, ct);
        var recordsByRaw = await LoadOutcomeRecordsByRawNoticeAsync(rawIds, ct);
        var detailsByRaw = await LoadProcurementDetailsByRawNoticeAsync(rawIds, ct);
        var attachmentsByRaw = await LoadAttachmentsByRawNoticeAsync(rawIds, ct);

        var drafts = new List<AmountCandidate>();
        foreach (var link in links)
        {
            foreach (var rawNoticeId in RelatedRawNoticeIds(link).Distinct())
            {
                if (!rawById.TryGetValue(rawNoticeId, out var raw))
                    continue;

                drafts.AddRange(await BuildRawNoticeCandidateDraftsAsync(
                    raw,
                    link.Id,
                    link.AwardRawNoticeId,
                    recordsByRaw.GetValueOrDefault(rawNoticeId) ?? [],
                    detailsByRaw.GetValueOrDefault(rawNoticeId) ?? [],
                    attachmentsByRaw.GetValueOrDefault(rawNoticeId) ?? [],
                    ct));
            }
        }

        await RemoveStaleOutcomeRecordCandidatesAsync(rawIds, ct);
        await UpsertDraftsAsync(drafts, ct);

        var candidates = await LoadCandidatesForRawNoticesAsync(rawIds, ct);
        var result = new Dictionary<long, IReadOnlyList<AmountCandidateDto>>();
        foreach (var link in links)
        {
            var related = RelatedRawNoticeIds(link).ToHashSet();
            var linkCandidates = candidates
                .Where(x => x.LifecyclePackageLinkId == link.Id || related.Contains(x.RawNoticeId))
                .ToList();
            result[link.Id] = OrderCandidates(linkCandidates).Select(MapAmountCandidate).ToList();
        }

        return result;
    }

    public async Task<IReadOnlyList<AmountCandidateDto>> EnsureLifecycleAmountCandidatesAsync(
        long linkId,
        CancellationToken ct = default)
    {
        var link = await LoadLifecycleLinkForReadAsync(linkId, ct);
        var dto = MapLifecycleLinkForCandidateLoading(link);
        var result = await EnsureLifecycleAmountCandidatesAsync([dto], ct);
        return result.TryGetValue(linkId, out var candidates) ? candidates : [];
    }

    public async Task<LifecycleAmountCandidateDebugDto> DiagnoseLifecycleAmountCandidatesAsync(
        long linkId,
        CancellationToken ct = default)
    {
        var link = await LoadLifecycleLinkForReadAsync(linkId, ct);
        var candidates = await EnsureLifecycleAmountCandidatesAsync(linkId, ct);
        var relatedRawIds = RelatedRawNoticeIds(MapLifecycleLinkForCandidateLoading(link)).ToHashSet();
        var publicReviewCount = candidates.Count(x => relatedRawIds.Contains(x.RawNoticeId));
        var closureCount = candidates.Count;
        var reasons = new List<string>();
        if (closureCount < publicReviewCount)
            reasons.Add("闭环页候选数少于公共审核可见候选数，需要检查 RawNotice 关联。");
        if (closureCount == publicReviewCount)
            reasons.Add("公共审核页和闭环页使用同一个 amount_candidate 候选池，当前没有数量差异。");

        return new LifecycleAmountCandidateDebugDto
        {
            LinkId = link.Id,
            AwardRawNoticeId = link.AwardRawNoticeId,
            CandidateRawNoticeId = link.CandidateRawNoticeId,
            ProcurementRawNoticeId = link.ProcurementRawNoticeId,
            PublicReviewVisibleCandidateCount = publicReviewCount,
            ClosureVisibleCandidateCount = closureCount,
            StatusCounts = candidates
                .GroupBy(x => x.Status, StringComparer.OrdinalIgnoreCase)
                .Select(x => new LifecycleAmountCandidateStatusCountDto { Status = x.Key, Count = x.Count() })
                .OrderBy(x => x.Status, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            DifferenceReasons = reasons,
            FilterReasons =
            [
                "闭环页按中标公告、候选公示、前置公告 RawNoticeId 汇总候选。",
                "Rejected 与 Unresolved 候选不会被隐藏。",
                "没有最终金额的候选仍作为 Unresolved 候选展示。"
            ],
            Candidates = candidates.ToList()
        };
    }

    public async Task<AmountCandidateOperationResultDto> SelectCandidateAsync(
        long linkId,
        long candidateId,
        AmountCandidateSelectRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var link = await LoadLifecycleLinkForUpdateAsync(linkId, ct);
        var candidate = await LoadCandidateForUpdateAsync(candidateId, ct);
        EnsureCandidateBelongsToLink(candidate, link);
        if (BidOpsOutcomeRecordPolicy.IsNonAwardLifecycleLink(link) ||
            BidOpsOutcomeRecordPolicy.IsNonAwardSupplierName(candidate.SupplierName))
        {
            throw new AtlasException("流标/废标/失败行仅用于展示，不能选择为中标/成交金额。");
        }

        if (!candidate.AmountValue.HasValue || candidate.AmountUnit is "rate" or "discount")
            throw new AtlasException("Only normalized money candidates can be selected as the final award amount.");
        if (!BidOpsAmountCandidateExtractor.IsPotentialFinalAmountType(candidate.AmountType))
            throw new AtlasException("Only winning/deal/quote amount candidates can be selected as the final award amount.");

        var now = DateTime.UtcNow;
        var query = await _amountCandidates.QueryDataScopeTrackingAsync(
            BidOpsDataResources.AmountCandidate,
            AtlasDataScopeType.AllTenant,
            ct);
        var selected = await query
            .Where(x => x.LifecyclePackageLinkId == link.Id && x.Status == BidOpsAmountCandidateStatuses.Selected)
            .ToListAsync(ct);
        foreach (var existing in selected.Where(x => x.Id != candidate.Id))
        {
            existing.Status = BidOpsAmountCandidateExtractor.IsPotentialFinalAmountType(existing.AmountType)
                ? BidOpsAmountCandidateStatuses.Recommended
                : BidOpsAmountCandidateStatuses.Candidate;
            existing.SelectedBy = null;
            existing.SelectedAt = null;
            existing.UpdatedAt = now;
        }

        candidate.LifecyclePackageLinkId = link.Id;
        candidate.Status = BidOpsAmountCandidateStatuses.Selected;
        candidate.RejectReason = string.Empty;
        candidate.ManualRemark = Truncate(request.Remark, 1000);
        candidate.SelectedBy = _current.UserId;
        candidate.SelectedAt = now;
        candidate.RejectedBy = null;
        candidate.RejectedAt = null;
        candidate.UpdatedAt = now;

        link.FinalAwardAmount = Math.Round(candidate.AmountValue.Value, 2);
        link.FinalAwardAmountSource = Truncate($"AmountCandidate:{candidate.AmountType}", 128);
        link.RequiresManualReview = true;
        link.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(ct);
        return await BuildOperationResultAsync(link, candidate, ct);
    }

    public async Task<AmountCandidateOperationResultDto> MarkCandidateTypeAsync(
        long linkId,
        long candidateId,
        AmountCandidateMarkTypeRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var amountType = NormalizeAmountType(request.AmountType);
        var link = await LoadLifecycleLinkForUpdateAsync(linkId, ct);
        var candidate = await LoadCandidateForUpdateAsync(candidateId, ct);
        EnsureCandidateBelongsToLink(candidate, link);

        var status = BidOpsAmountCandidateExtractor.ResolveStatus(amountType, candidate.AmountValue);
        candidate.LifecyclePackageLinkId ??= link.Id;
        candidate.AmountType = amountType;
        candidate.IsPotentialFinalAmount = BidOpsAmountCandidateExtractor.IsPotentialFinalAmountType(amountType);
        if (candidate.Status != BidOpsAmountCandidateStatuses.Selected)
        {
            candidate.Status = status.Status;
            candidate.RejectReason = status.RejectReason;
        }

        candidate.Confidence = Math.Max(candidate.Confidence, status.Confidence);
        candidate.ManualRemark = Truncate(request.Remark, 1000);
        candidate.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        return await BuildOperationResultAsync(link, candidate, ct);
    }

    public async Task<AmountCandidateOperationResultDto> RejectCandidateAsync(
        long linkId,
        long candidateId,
        AmountCandidateRejectRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new AtlasException("A reject reason is required for amount candidates.");

        var link = await LoadLifecycleLinkForUpdateAsync(linkId, ct);
        var candidate = await LoadCandidateForUpdateAsync(candidateId, ct);
        EnsureCandidateBelongsToLink(candidate, link);
        var now = DateTime.UtcNow;
        candidate.LifecyclePackageLinkId ??= link.Id;
        candidate.Status = BidOpsAmountCandidateStatuses.Rejected;
        candidate.RejectReason = Truncate(request.Reason, 500);
        candidate.RejectedBy = _current.UserId;
        candidate.RejectedAt = now;
        candidate.UpdatedAt = now;
        if (candidate.SelectedAt.HasValue)
        {
            candidate.SelectedBy = null;
            candidate.SelectedAt = null;
            if (link.FinalAwardAmount == candidate.AmountValue)
            {
                link.FinalAwardAmount = null;
                link.FinalAwardAmountSource = "Missing";
                link.UpdatedAt = now;
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return await BuildOperationResultAsync(link, candidate, ct);
    }

    public async Task<AmountCandidateOperationResultDto> RestoreCandidateAsync(
        long linkId,
        long candidateId,
        AmountCandidateRestoreRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var link = await LoadLifecycleLinkForUpdateAsync(linkId, ct);
        var candidate = await LoadCandidateForUpdateAsync(candidateId, ct);
        EnsureCandidateBelongsToLink(candidate, link);
        var status = BidOpsAmountCandidateExtractor.ResolveStatus(candidate.AmountType, candidate.AmountValue);
        candidate.LifecyclePackageLinkId ??= link.Id;
        candidate.Status = status.Status;
        candidate.RejectReason = status.RejectReason;
        candidate.RejectedBy = null;
        candidate.RejectedAt = null;
        candidate.ManualRemark = Truncate(request.Remark, 1000);
        candidate.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        return await BuildOperationResultAsync(link, candidate, ct);
    }

    public async Task<LifecycleFinalAwardAmountClearResultDto> ClearFinalAwardAmountsAsync(
        LifecycleFinalAwardAmountClearRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var linkIds = (request.LinkIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (linkIds.Length == 0)
            throw new AtlasException("At least one lifecycle link id is required.");

        var result = new LifecycleFinalAwardAmountClearResultDto
        {
            RequestedCount = linkIds.Length
        };

        var linkQuery = await _lifecycleLinks.QueryDataScopeTrackingAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);
        var links = await linkQuery
            .Where(x => linkIds.Contains(x.Id))
            .ToListAsync(ct);
        var linksById = links.ToDictionary(x => x.Id);

        var candidateQuery = await _amountCandidates.QueryDataScopeTrackingAsync(
            BidOpsDataResources.AmountCandidate,
            AtlasDataScopeType.AllTenant,
            ct);
        var selectedCandidates = await candidateQuery
            .Where(x => x.LifecyclePackageLinkId.HasValue &&
                        linkIds.Contains(x.LifecyclePackageLinkId.Value) &&
                        x.Status == BidOpsAmountCandidateStatuses.Selected)
            .ToListAsync(ct);
        var selectedByLinkId = selectedCandidates
            .GroupBy(x => x.LifecyclePackageLinkId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());

        var now = DateTime.UtcNow;
        foreach (var linkId in linkIds)
        {
            if (!linksById.TryGetValue(linkId, out var link))
            {
                result.FailedCount += 1;
                result.Items.Add(new LifecycleFinalAwardAmountClearItemDto
                {
                    LinkId = linkId,
                    Succeeded = false,
                    Skipped = false,
                    Message = "Lifecycle link does not exist or is outside the current data scope."
                });
                continue;
            }

            var selected = selectedByLinkId.GetValueOrDefault(linkId) ?? [];
            if (!link.FinalAwardAmount.HasValue &&
                selected.Count == 0 &&
                string.Equals(link.FinalAwardAmountSource, "Missing", StringComparison.OrdinalIgnoreCase))
            {
                result.SkippedCount += 1;
                result.Items.Add(new LifecycleFinalAwardAmountClearItemDto
                {
                    LinkId = linkId,
                    Succeeded = true,
                    Skipped = true,
                    Message = "中标金额已经为空。",
                    FinalAwardAmount = link.FinalAwardAmount,
                    FinalAwardAmountSource = link.FinalAwardAmountSource,
                    LinkUpdatedAt = link.UpdatedAt
                });
                continue;
            }

            foreach (var candidate in selected)
            {
                RestoreSelectedCandidateStatus(candidate, now, request.Reason);
            }

            // 批量清空只撤销最终中标金额，不改变闭环匹配状态，便于重新从真实中标金额候选里人工选择。
            link.FinalAwardAmount = null;
            link.FinalAwardAmountSource = "Missing";
            link.RequiresManualReview = true;
            link.UpdatedAt = now;

            result.SucceededCount += 1;
            result.Items.Add(new LifecycleFinalAwardAmountClearItemDto
            {
                LinkId = link.Id,
                Succeeded = true,
                Skipped = false,
                Message = selected.Count > 0
                    ? $"已清空中标金额，并撤销 {selected.Count} 个已采用金额候选。"
                    : "已清空中标金额。",
                FinalAwardAmount = link.FinalAwardAmount,
                FinalAwardAmountSource = link.FinalAwardAmountSource,
                LinkUpdatedAt = link.UpdatedAt
            });
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    private async Task<AmountCandidateOperationResultDto> BuildOperationResultAsync(
        LifecyclePackageLink link,
        AmountCandidate candidate,
        CancellationToken ct)
    {
        var candidates = await EnsureLifecycleAmountCandidatesAsync(link.Id, ct);
        return new AmountCandidateOperationResultDto
        {
            Candidate = MapAmountCandidate(candidate),
            Candidates = candidates.ToList(),
            FinalAwardAmount = link.FinalAwardAmount,
            FinalAwardAmountSource = link.FinalAwardAmountSource,
            LinkUpdatedAt = link.UpdatedAt
        };
    }

    private static void RestoreSelectedCandidateStatus(
        AmountCandidate candidate,
        DateTime now,
        string? remark)
    {
        var status = BidOpsAmountCandidateExtractor.ResolveStatus(candidate.AmountType, candidate.AmountValue);
        candidate.Status = status.Status;
        candidate.RejectReason = status.RejectReason;
        candidate.ManualRemark = Truncate(remark, 1000);
        candidate.SelectedBy = null;
        candidate.SelectedAt = null;
        candidate.UpdatedAt = now;
    }

    private async Task<List<AmountCandidate>> BuildRawNoticeCandidateDraftsAsync(
        RawNotice raw,
        long? lifecyclePackageLinkId,
        long? resultRawNoticeId,
        CancellationToken ct)
    {
        var records = await LoadOutcomeRecordsAsync(raw.Id, ct);
        var details = await LoadProcurementDetailsAsync(raw.Id, ct);
        var attachments = await LoadAttachmentsAsync(raw.Id, ct);
        return await BuildRawNoticeCandidateDraftsAsync(
            raw,
            lifecyclePackageLinkId,
            resultRawNoticeId,
            records,
            details,
            attachments,
            ct);
    }

    private async Task<List<AmountCandidate>> BuildRawNoticeCandidateDraftsAsync(
        RawNotice raw,
        long? lifecyclePackageLinkId,
        long? resultRawNoticeId,
        IReadOnlyCollection<OutcomeSupplierRecord> records,
        IReadOnlyCollection<ProcurementDetailStaging> details,
        IReadOnlyCollection<RawAttachment> attachments,
        CancellationToken ct)
    {
        var rawText = await ReadRawNoticeTextAsync(raw, ct);
        var drafts = new List<AmountCandidate>();
        foreach (var record in records)
            AddOutcomeRecordDrafts(raw, lifecyclePackageLinkId, resultRawNoticeId, record, rawText, drafts);

        foreach (var detail in details)
            AddProcurementDetailDrafts(raw, lifecyclePackageLinkId, resultRawNoticeId, detail, drafts);

        AddTextDrafts(
            raw,
            lifecyclePackageLinkId,
            resultRawNoticeId,
            null,
            BidOpsAmountCandidateSourceKinds.RawNoticeText,
            raw.Title,
            "raw-notice",
            rawText,
            drafts);

        foreach (var attachment in attachments.Where(x => !string.IsNullOrWhiteSpace(x.TextContentStorageKey)))
        {
            var text = await ReadAttachmentTextAsync(attachment, ct);
            AddTextDrafts(
                raw,
                lifecyclePackageLinkId,
                resultRawNoticeId,
                attachment,
                BidOpsAmountCandidateSourceKinds.RawAttachmentText,
                attachment.FileName,
                $"attachment:{attachment.Id.ToString(CultureInfo.InvariantCulture)}",
                text,
                drafts);
        }

        return drafts;
    }

    private void AddOutcomeRecordDrafts(
        RawNotice raw,
        long? lifecyclePackageLinkId,
        long? resultRawNoticeId,
        OutcomeSupplierRecord record,
        string rawText,
        ICollection<AmountCandidate> drafts)
    {
        if (BidOpsOutcomeRecordPolicy.IsNonAwardOutcome(record))
            return;

        if (record.AwardAmount.HasValue)
        {
            var type = ResolveOutcomeAmountType(record);
            drafts.Add(CreateDraft(
                raw,
                lifecyclePackageLinkId,
                resultRawNoticeId,
                BidOpsAmountCandidateSourceKinds.OutcomeSupplierRecord,
                raw.Title,
                string.Empty,
                $"outcome_supplier_record:{record.Id.ToString(CultureInfo.InvariantCulture)}:AwardAmount",
                record.ProjectCode,
                record.ProjectName,
                record.LotNo,
                record.LotName,
                record.PackageNo,
                record.PackageName,
                record.SupplierName,
                type,
                record.AwardAmount.Value.ToString(CultureInfo.InvariantCulture),
                record.AwardAmount.Value,
                "元",
                record.EvidenceText,
                record.EvidenceText,
                record.OutcomeType == BidOpsOutcomeTypes.Candidate ? 0.86m : 0.92m,
                outcomeSupplierRecordId: record.Id,
                tenderPackageId: record.TenderPackageId));
        }
        else if (!string.IsNullOrWhiteSpace(record.EvidenceText))
        {
            foreach (var extracted in BidOpsAmountCandidateExtractor.ExtractTextCandidates(
                         record.EvidenceText,
                         $"outcome_supplier_record:{record.Id.ToString(CultureInfo.InvariantCulture)}"))
            {
                drafts.Add(CreateDraftFromExtracted(
                    raw,
                    lifecyclePackageLinkId,
                    resultRawNoticeId,
                    BidOpsAmountCandidateSourceKinds.OutcomeSupplierRecord,
                    raw.Title,
                    string.Empty,
                    record.ProjectCode,
                    record.ProjectName,
                    record.LotNo,
                    record.LotName,
                    record.PackageNo,
                    record.PackageName,
                    record.SupplierName,
                    extracted,
                    outcomeSupplierRecordId: record.Id,
                    tenderPackageId: record.TenderPackageId));
            }
        }

        if (TryExtractOutcomeServiceFeeCandidate(record, rawText, out var serviceFee))
        {
            drafts.Add(CreateDraft(
                raw,
                lifecyclePackageLinkId,
                resultRawNoticeId,
                BidOpsAmountCandidateSourceKinds.OutcomeSupplierRecord,
                raw.Title,
                string.Empty,
                $"outcome_supplier_record:{record.Id.ToString(CultureInfo.InvariantCulture)}:ServiceFeeTenThousandYuanEvidence",
                record.ProjectCode,
                record.ProjectName,
                record.LotNo,
                record.LotName,
                record.PackageNo,
                record.PackageName,
                record.SupplierName,
                BidOpsAmountCandidateTypes.AgencyFee,
                serviceFee.AmountRaw,
                serviceFee.AmountValue,
                "万元",
                serviceFee.EvidenceText,
                serviceFee.EvidenceText,
                0.82m,
                outcomeSupplierRecordId: record.Id,
                tenderPackageId: record.TenderPackageId));
        }

        if (record.ProcurementAgencyServiceFeeAmount.HasValue)
        {
            drafts.Add(CreateDraft(
                raw,
                lifecyclePackageLinkId,
                resultRawNoticeId,
                BidOpsAmountCandidateSourceKinds.OutcomeSupplierRecord,
                raw.Title,
                string.Empty,
                $"outcome_supplier_record:{record.Id.ToString(CultureInfo.InvariantCulture)}:ProcurementAgencyServiceFeeAmount",
                record.ProjectCode,
                record.ProjectName,
                record.LotNo,
                record.LotName,
                record.PackageNo,
                record.PackageName,
                record.SupplierName,
                BidOpsAmountCandidateTypes.AgencyFee,
                record.ProcurementAgencyServiceFeeAmount.Value.ToString(CultureInfo.InvariantCulture),
                record.ProcurementAgencyServiceFeeAmount.Value,
                "元",
                record.EvidenceText,
                record.EvidenceText,
                0.82m,
                outcomeSupplierRecordId: record.Id,
                tenderPackageId: record.TenderPackageId));
        }

        if (!record.AwardAmount.HasValue && string.IsNullOrWhiteSpace(record.EvidenceText))
        {
            drafts.Add(CreateDraft(
                raw,
                lifecyclePackageLinkId,
                resultRawNoticeId,
                BidOpsAmountCandidateSourceKinds.OutcomeSupplierRecord,
                raw.Title,
                string.Empty,
                $"outcome_supplier_record:{record.Id.ToString(CultureInfo.InvariantCulture)}:MissingAmount",
                record.ProjectCode,
                record.ProjectName,
                record.LotNo,
                record.LotName,
                record.PackageNo,
                record.PackageName,
                record.SupplierName,
                BidOpsAmountCandidateTypes.Unknown,
                string.Empty,
                null,
                string.Empty,
                "中标/候选明细未解析出金额。",
                string.Empty,
                0.35m,
                outcomeSupplierRecordId: record.Id,
                tenderPackageId: record.TenderPackageId));
        }
    }

    private void AddProcurementDetailDrafts(
        RawNotice raw,
        long? lifecyclePackageLinkId,
        long? resultRawNoticeId,
        ProcurementDetailStaging detail,
        ICollection<AmountCandidate> drafts)
    {
        AddProcurementAmountDraft(raw, lifecyclePackageLinkId, resultRawNoticeId, detail, detail.PackageEstimatedAmount, "PackageEstimatedAmount", BidOpsAmountCandidateTypes.BudgetAmount, drafts);
        AddProcurementAmountDraft(raw, lifecyclePackageLinkId, resultRawNoticeId, detail, detail.BudgetAmount, "BudgetAmount", BidOpsAmountCandidateTypes.BudgetAmount, drafts);
        AddProcurementAmountDraft(raw, lifecyclePackageLinkId, resultRawNoticeId, detail, detail.MaxPrice, "MaxPrice", BidOpsAmountCandidateTypes.CeilingPrice, drafts);
        AddProcurementAmountDraft(raw, lifecyclePackageLinkId, resultRawNoticeId, detail, detail.ProcurementAmount, "ProcurementAmount", BidOpsAmountCandidateTypes.BudgetAmount, drafts);
        AddProcurementAmountDraft(raw, lifecyclePackageLinkId, resultRawNoticeId, detail, detail.ItemEstimatedAmount, "ItemEstimatedAmount", BidOpsAmountCandidateTypes.BudgetAmount, drafts);
        AddProcurementAmountDraft(raw, lifecyclePackageLinkId, resultRawNoticeId, detail, detail.ResponseGuaranteeAmount, "ResponseGuaranteeAmount", BidOpsAmountCandidateTypes.Deposit, drafts);
        AddProcurementRateDraft(raw, lifecyclePackageLinkId, resultRawNoticeId, detail, detail.MaxPriceRatePercent, "MaxPriceRatePercent", BidOpsAmountCandidateTypes.Rate, drafts);
        AddProcurementRateDraft(raw, lifecyclePackageLinkId, resultRawNoticeId, detail, detail.TaxRatePercent, "TaxRatePercent", BidOpsAmountCandidateTypes.Rate, drafts);
    }

    private void AddProcurementAmountDraft(
        RawNotice raw,
        long? lifecyclePackageLinkId,
        long? resultRawNoticeId,
        ProcurementDetailStaging detail,
        decimal? value,
        string fieldName,
        string amountType,
        ICollection<AmountCandidate> drafts)
    {
        if (!value.HasValue || value.Value <= 0m)
            return;

        var evidence = BuildProcurementEvidence(detail, fieldName);
        drafts.Add(CreateDraft(
            raw,
            lifecyclePackageLinkId,
            resultRawNoticeId,
            BidOpsAmountCandidateSourceKinds.ProcurementDetailStaging,
            raw.Title,
            detail.SourceSheetName,
            $"procurement_detail_staging:{detail.Id.ToString(CultureInfo.InvariantCulture)}:{fieldName}",
            detail.ProjectCode,
            detail.ProjectName,
            detail.LotNo,
            detail.LotName,
            detail.PackageNo,
            detail.PackageName,
            string.Empty,
            amountType,
            value.Value.ToString(CultureInfo.InvariantCulture),
            value.Value,
            "元",
            evidence,
            evidence,
            0.8m,
            procurementDetailStagingId: detail.Id));
    }

    private void AddProcurementRateDraft(
        RawNotice raw,
        long? lifecyclePackageLinkId,
        long? resultRawNoticeId,
        ProcurementDetailStaging detail,
        decimal? value,
        string fieldName,
        string amountType,
        ICollection<AmountCandidate> drafts)
    {
        if (!value.HasValue || value.Value <= 0m)
            return;

        var normalized = Math.Round(value.Value / 100m, 6);
        var evidence = BuildProcurementEvidence(detail, fieldName);
        drafts.Add(CreateDraft(
            raw,
            lifecyclePackageLinkId,
            resultRawNoticeId,
            BidOpsAmountCandidateSourceKinds.ProcurementDetailStaging,
            raw.Title,
            detail.SourceSheetName,
            $"procurement_detail_staging:{detail.Id.ToString(CultureInfo.InvariantCulture)}:{fieldName}",
            detail.ProjectCode,
            detail.ProjectName,
            detail.LotNo,
            detail.LotName,
            detail.PackageNo,
            detail.PackageName,
            string.Empty,
            amountType,
            value.Value.ToString(CultureInfo.InvariantCulture) + "%",
            normalized,
            "rate",
            evidence,
            evidence,
            0.7m,
            procurementDetailStagingId: detail.Id));
    }

    private void AddTextDrafts(
        RawNotice raw,
        long? lifecyclePackageLinkId,
        long? resultRawNoticeId,
        RawAttachment? attachment,
        string sourceKind,
        string sourceTitle,
        string sourceLocationPrefix,
        string? text,
        ICollection<AmountCandidate> drafts)
    {
        foreach (var extracted in BidOpsAmountCandidateExtractor.ExtractTextCandidates(text, sourceLocationPrefix))
        {
            if (IsLowContextTextExtractNoise(sourceKind, extracted))
                continue;

            drafts.Add(CreateDraftFromExtracted(
                raw,
                lifecyclePackageLinkId,
                resultRawNoticeId,
                sourceKind,
                sourceTitle,
                attachment?.FileName ?? string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                extracted,
                rawAttachmentId: attachment?.Id));
        }
    }

    private AmountCandidate CreateDraftFromExtracted(
        RawNotice raw,
        long? lifecyclePackageLinkId,
        long? resultRawNoticeId,
        string sourceKind,
        string sourceTitle,
        string sourceFileName,
        string projectCode,
        string projectName,
        string lotNo,
        string lotName,
        string packageNo,
        string packageName,
        string supplierName,
        BidOpsExtractedAmountCandidate extracted,
        long? rawAttachmentId = null,
        long? outcomeSupplierRecordId = null,
        long? procurementDetailStagingId = null,
        long? tenderPackageId = null)
    {
        return CreateDraft(
            raw,
            lifecyclePackageLinkId,
            resultRawNoticeId,
            sourceKind,
            sourceTitle,
            sourceFileName,
            extracted.SourceLocation,
            projectCode,
            projectName,
            lotNo,
            lotName,
            packageNo,
            packageName,
            supplierName,
            extracted.AmountType,
            extracted.AmountRaw,
            extracted.AmountValue,
            extracted.AmountUnit,
            extracted.ContextText,
            extracted.ContextText,
            extracted.Confidence,
            rawAttachmentId,
            outcomeSupplierRecordId,
            procurementDetailStagingId,
            tenderPackageId);
    }

    private AmountCandidate CreateDraft(
        RawNotice raw,
        long? lifecyclePackageLinkId,
        long? resultRawNoticeId,
        string sourceKind,
        string sourceTitle,
        string sourceFileName,
        string sourceLocation,
        string projectCode,
        string projectName,
        string lotNo,
        string lotName,
        string packageNo,
        string packageName,
        string supplierName,
        string amountType,
        string amountRaw,
        decimal? amountValue,
        string amountUnit,
        string evidenceText,
        string contextText,
        decimal confidence,
        long? rawAttachmentId = null,
        long? outcomeSupplierRecordId = null,
        long? procurementDetailStagingId = null,
        long? tenderPackageId = null)
    {
        var normalizedAmountType = NormalizeAmountType(amountType);
        var storedAmountValue = NormalizeStoredAmountValue(amountValue, out var amountOutOfRange);
        (string Status, string RejectReason, decimal Confidence) resolved = amountOutOfRange
            ? (BidOpsAmountCandidateStatuses.Unresolved, "金额数值超出系统可存储范围，保留原文待人工核验。", 0.35m)
            : BidOpsAmountCandidateExtractor.ResolveStatus(normalizedAmountType, storedAmountValue);
        var status = storedAmountValue.HasValue
            ? resolved.Status
            : BidOpsAmountCandidateStatuses.Unresolved;
        var rejectReason = storedAmountValue.HasValue
            ? resolved.RejectReason
            : resolved.RejectReason;

        var candidate = new AmountCandidate
        {
            Id = _idGenerator.NextId(),
            TenantId = raw.TenantId,
            LifecyclePackageLinkId = lifecyclePackageLinkId,
            RawNoticeId = raw.Id,
            ResultRawNoticeId = resultRawNoticeId,
            RawAttachmentId = rawAttachmentId,
            OutcomeSupplierRecordId = outcomeSupplierRecordId,
            ProcurementDetailStagingId = procurementDetailStagingId,
            TenderPackageId = tenderPackageId,
            SourceKind = Truncate(sourceKind, 64),
            SourceNoticeType = Truncate(raw.NoticeType, 64),
            SourceTitle = Truncate(sourceTitle, 500),
            SourceFileName = Truncate(sourceFileName, 300),
            SourceLocation = Truncate(sourceLocation, 256),
            ProjectCode = Truncate(projectCode, 128),
            ProjectName = Truncate(projectName, 500),
            LotNo = Truncate(lotNo, 128),
            LotName = Truncate(lotName, 300),
            PackageNo = Truncate(packageNo, 128),
            PackageName = Truncate(packageName, 500),
            SupplierName = Truncate(supplierName, 300),
            AmountType = normalizedAmountType,
            AmountRaw = Truncate(amountRaw, 128),
            AmountValue = storedAmountValue,
            AmountUnit = Truncate(amountUnit, 32),
            Currency = amountUnit is "rate" or "discount" ? string.Empty : "CNY",
            IsPotentialFinalAmount = storedAmountValue.HasValue &&
                                     BidOpsAmountCandidateExtractor.IsPotentialFinalAmountType(normalizedAmountType),
            Confidence = Math.Clamp(Math.Max(confidence, resolved.Confidence), 0m, 1m),
            Status = status,
            RejectReason = Truncate(rejectReason, 500),
            EvidenceText = Truncate(evidenceText, 2000),
            ContextText = Truncate(contextText, 1000),
            CreatedAt = DateTime.UtcNow
        };
        candidate.SourceHash = ComputeSourceHash(
            candidate.RawNoticeId.ToString(CultureInfo.InvariantCulture),
            candidate.RawAttachmentId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            candidate.OutcomeSupplierRecordId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            candidate.ProcurementDetailStagingId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            candidate.SourceKind,
            candidate.SourceLocation,
            candidate.AmountType,
            candidate.AmountRaw,
            candidate.AmountValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            candidate.ProjectCode,
            candidate.LotNo,
            candidate.PackageNo,
            candidate.SupplierName);
        return candidate;
    }

    private static decimal? NormalizeStoredAmountValue(decimal? amountValue, out bool outOfRange)
    {
        outOfRange = false;
        if (!amountValue.HasValue)
            return null;

        var rounded = Math.Round(amountValue.Value, 6);
        if (rounded is > MaxStoredAmountCandidateValue or < -MaxStoredAmountCandidateValue)
        {
            // amount_candidate.AmountValue 是 decimal(18,6)，异常长数字保留原文和证据即可，不能让列表查询写库失败。
            outOfRange = true;
            return null;
        }

        return rounded;
    }

    private async Task UpsertDraftsAsync(IReadOnlyCollection<AmountCandidate> drafts, CancellationToken ct)
    {
        var uniqueDrafts = drafts
            .Where(x => !string.IsNullOrWhiteSpace(x.SourceHash))
            .GroupBy(x => new AmountCandidateSourceHashKey(x.TenantId, x.SourceHash))
            .Select(x => x.First())
            .ToList();
        if (uniqueDrafts.Count == 0)
            return;

        var hashes = uniqueDrafts
            .Select(x => x.SourceHash)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var query = await _amountCandidates.QueryDataScopeAsync(
            BidOpsDataResources.AmountCandidate,
            AtlasDataScopeType.AllTenant,
            ct);
        var existingKeys = (await query
                .Where(x => hashes.Contains(x.SourceHash))
                .Select(x => new { x.TenantId, x.SourceHash })
                .ToListAsync(ct))
            .Select(x => new AmountCandidateSourceHashKey(x.TenantId, x.SourceHash))
            .ToHashSet();
        var missing = uniqueDrafts
            .Where(x => !existingKeys.Contains(new AmountCandidateSourceHashKey(x.TenantId, x.SourceHash)))
            .ToList();
        if (missing.Count == 0)
            return;

        await _amountCandidates.AddRangeAsync(missing, ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsAmountCandidateSourceHashDuplicate(ex))
        {
            _logger.LogInformation(
                ex,
                "Ignored concurrent BidOps amount candidate source-hash insert. MissingDraftCount={MissingDraftCount}.",
                missing.Count);
        }
    }

    private async Task RemoveStaleOutcomeRecordCandidatesAsync(
        IReadOnlyCollection<long> rawNoticeIds,
        CancellationToken ct)
    {
        if (rawNoticeIds.Count == 0)
            return;

        var recordQuery = await _outcomeRecords.QueryDataScopeAsync(
            BidOpsDataResources.OutcomeSupplierRecord,
            AtlasDataScopeType.AllTenant,
            ct);
        var currentRecords = await recordQuery
            .Where(x => rawNoticeIds.Contains(x.RawNoticeId))
            .ToListAsync(ct);
        var currentRecordIds = currentRecords.Select(x => x.Id).ToHashSet();

        var candidateQuery = await _amountCandidates.QueryDataScopeTrackingAsync(
            BidOpsDataResources.AmountCandidate,
            AtlasDataScopeType.AllTenant,
            ct);
        var outcomeCandidates = await candidateQuery
            .Where(x =>
                rawNoticeIds.Contains(x.RawNoticeId) &&
                x.SourceKind == BidOpsAmountCandidateSourceKinds.OutcomeSupplierRecord &&
                x.OutcomeSupplierRecordId.HasValue)
            .ToListAsync(ct);
        var staleCandidates = outcomeCandidates
            .Where(x => IsStaleOutcomeRecordCandidate(x, currentRecordIds))
            .ToList();
        if (staleCandidates.Count == 0)
            return;

        // 成交明细重解析会整体替换 OutcomeSupplierRecord；旧明细派生的金额候选必须清理，否则审核页会混入过期分标/包信息。
        await _amountCandidates.RemoveRangeAsync(staleCandidates, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static bool IsStaleOutcomeRecordCandidate(
        AmountCandidate candidate,
        IReadOnlySet<long> currentOutcomeRecordIds)
    {
        return candidate.SourceKind == BidOpsAmountCandidateSourceKinds.OutcomeSupplierRecord &&
               candidate.OutcomeSupplierRecordId.HasValue &&
               !currentOutcomeRecordIds.Contains(candidate.OutcomeSupplierRecordId.Value);
    }

    private async Task<IReadOnlyDictionary<long, RawNotice>> LoadRawNoticesAsync(
        IReadOnlyCollection<long> rawNoticeIds,
        CancellationToken ct)
    {
        if (rawNoticeIds.Count == 0)
            return new Dictionary<long, RawNotice>();

        var query = await _rawNotices.QueryDataScopeAsync(
            BidOpsDataResources.RawNotice,
            AtlasDataScopeType.AllTenant,
            ct);
        var raws = await query.Where(x => rawNoticeIds.Contains(x.Id)).ToListAsync(ct);
        return raws.ToDictionary(x => x.Id);
    }

    private async Task<IReadOnlyDictionary<long, IReadOnlyList<OutcomeSupplierRecord>>> LoadOutcomeRecordsByRawNoticeAsync(
        IReadOnlyCollection<long> rawNoticeIds,
        CancellationToken ct)
    {
        if (rawNoticeIds.Count == 0)
            return new Dictionary<long, IReadOnlyList<OutcomeSupplierRecord>>();

        var query = await _outcomeRecords.QueryDataScopeAsync(
            BidOpsDataResources.OutcomeSupplierRecord,
            AtlasDataScopeType.AllTenant,
            ct);
        var records = await query.Where(x => rawNoticeIds.Contains(x.RawNoticeId)).ToListAsync(ct);
        return records
            .GroupBy(x => x.RawNoticeId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<OutcomeSupplierRecord>)x.OrderBy(r => r.ExtractionOrder).ThenBy(r => r.Id).ToList());
    }

    private async Task<IReadOnlyDictionary<long, IReadOnlyList<ProcurementDetailStaging>>> LoadProcurementDetailsByRawNoticeAsync(
        IReadOnlyCollection<long> rawNoticeIds,
        CancellationToken ct)
    {
        if (rawNoticeIds.Count == 0)
            return new Dictionary<long, IReadOnlyList<ProcurementDetailStaging>>();

        var query = await _procurementDetails.QueryDataScopeAsync(
            BidOpsDataResources.ReviewTask,
            AtlasDataScopeType.AllTenant,
            ct);
        var details = await query.Where(x => rawNoticeIds.Contains(x.RawNoticeId)).ToListAsync(ct);
        return details
            .GroupBy(x => x.RawNoticeId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<ProcurementDetailStaging>)x.OrderBy(d => d.TableIndex ?? int.MaxValue).ThenBy(d => d.RowIndex ?? int.MaxValue).ThenBy(d => d.Id).ToList());
    }

    private async Task<IReadOnlyDictionary<long, IReadOnlyList<RawAttachment>>> LoadAttachmentsByRawNoticeAsync(
        IReadOnlyCollection<long> rawNoticeIds,
        CancellationToken ct)
    {
        if (rawNoticeIds.Count == 0)
            return new Dictionary<long, IReadOnlyList<RawAttachment>>();

        var query = await _rawAttachments.QueryDataScopeAsync(
            BidOpsDataResources.RawNotice,
            AtlasDataScopeType.AllTenant,
            ct);
        var attachments = await query.Where(x => rawNoticeIds.Contains(x.RawNoticeId)).ToListAsync(ct);
        return attachments
            .GroupBy(x => x.RawNoticeId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<RawAttachment>)x.OrderBy(a => a.Id).ToList());
    }

    private async Task<List<OutcomeSupplierRecord>> LoadOutcomeRecordsAsync(long rawNoticeId, CancellationToken ct)
    {
        var query = await _outcomeRecords.QueryDataScopeAsync(
            BidOpsDataResources.OutcomeSupplierRecord,
            AtlasDataScopeType.AllTenant,
            ct);
        return await query.Where(x => x.RawNoticeId == rawNoticeId).ToListAsync(ct);
    }

    private async Task<List<ProcurementDetailStaging>> LoadProcurementDetailsAsync(long rawNoticeId, CancellationToken ct)
    {
        var query = await _procurementDetails.QueryDataScopeAsync(
            BidOpsDataResources.ReviewTask,
            AtlasDataScopeType.AllTenant,
            ct);
        return await query.Where(x => x.RawNoticeId == rawNoticeId).ToListAsync(ct);
    }

    private async Task<List<RawAttachment>> LoadAttachmentsAsync(long rawNoticeId, CancellationToken ct)
    {
        var query = await _rawAttachments.QueryDataScopeAsync(
            BidOpsDataResources.RawNotice,
            AtlasDataScopeType.AllTenant,
            ct);
        return await query.Where(x => x.RawNoticeId == rawNoticeId).ToListAsync(ct);
    }

    private async Task<List<AmountCandidate>> LoadCandidatesForRawNoticesAsync(
        IReadOnlyCollection<long> rawNoticeIds,
        CancellationToken ct)
    {
        if (rawNoticeIds.Count == 0)
            return [];

        var query = await _amountCandidates.QueryDataScopeAsync(
            BidOpsDataResources.AmountCandidate,
            AtlasDataScopeType.AllTenant,
            ct);
        return await query.Where(x => rawNoticeIds.Contains(x.RawNoticeId)).ToListAsync(ct);
    }

    private async Task<LifecyclePackageLink> LoadLifecycleLinkForReadAsync(long linkId, CancellationToken ct)
    {
        var query = await _lifecycleLinks.QueryDataScopeAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);
        return await query.Where(x => x.Id == linkId).FirstOrDefaultAsync(ct)
               ?? throw new AtlasException($"BidOps lifecycle package link does not exist: {linkId}");
    }

    private async Task<LifecyclePackageLink> LoadLifecycleLinkForUpdateAsync(long linkId, CancellationToken ct)
    {
        var query = await _lifecycleLinks.QueryDataScopeTrackingAsync(
            BidOpsDataResources.LifecyclePackageLink,
            AtlasDataScopeType.AllTenant,
            ct);
        return await query.Where(x => x.Id == linkId).FirstOrDefaultAsync(ct)
               ?? throw new AtlasException($"BidOps lifecycle package link does not exist: {linkId}");
    }

    private async Task<AmountCandidate> LoadCandidateForUpdateAsync(long candidateId, CancellationToken ct)
    {
        var query = await _amountCandidates.QueryDataScopeTrackingAsync(
            BidOpsDataResources.AmountCandidate,
            AtlasDataScopeType.AllTenant,
            ct);
        return await query.Where(x => x.Id == candidateId).FirstOrDefaultAsync(ct)
               ?? throw new AtlasException($"BidOps amount candidate does not exist: {candidateId}");
    }

    private async Task<string> ReadRawNoticeTextAsync(RawNotice raw, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw.TextContentStorageKey))
            return raw.TextPreview;

        return await ReadStoredTextAsync(raw.TextContentStorageKey, raw.TextPreview, raw.Id, null, ct);
    }

    private async Task<string> ReadAttachmentTextAsync(RawAttachment attachment, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(attachment.TextContentStorageKey))
            return string.Empty;

        return await ReadStoredTextAsync(attachment.TextContentStorageKey, string.Empty, attachment.RawNoticeId, attachment.Id, ct);
    }

    private async Task<string> ReadStoredTextAsync(
        string storageKey,
        string fallback,
        long rawNoticeId,
        long? rawAttachmentId,
        CancellationToken ct)
    {
        try
        {
            await using var stream = await _fileStore.OpenReadAsync(storageKey, ct);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync(ct);
            return text.Length > StoredTextReadLimit ? text[..StoredTextReadLimit] : text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to read BidOps amount candidate source text for RawNoticeId {RawNoticeId}, RawAttachmentId {RawAttachmentId}, StorageKey {StorageKey}.",
                rawNoticeId,
                rawAttachmentId,
                storageKey);
            return fallback;
        }
    }

    private static IReadOnlyList<long> RelatedRawNoticeIds(LifecyclePackageLinkDto link)
    {
        return new[] { link.AwardRawNoticeId, link.CandidateRawNoticeId, link.ProcurementRawNoticeId }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();
    }

    private static LifecyclePackageLinkDto MapLifecycleLinkForCandidateLoading(LifecyclePackageLink link)
    {
        return new LifecyclePackageLinkDto
        {
            Id = link.Id,
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
            FinalAwardAmountSource = link.FinalAwardAmountSource
        };
    }

    private static bool IsLowContextTextExtractNoise(
        string sourceKind,
        BidOpsExtractedAmountCandidate extracted)
    {
        return sourceKind is BidOpsAmountCandidateSourceKinds.RawNoticeText or BidOpsAmountCandidateSourceKinds.RawAttachmentText &&
               string.Equals(extracted.AmountType, BidOpsAmountCandidateTypes.Unknown, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureCandidateBelongsToLink(AmountCandidate candidate, LifecyclePackageLink link)
    {
        if (candidate.LifecyclePackageLinkId == link.Id ||
            candidate.RawNoticeId == link.AwardRawNoticeId ||
            candidate.RawNoticeId == link.CandidateRawNoticeId ||
            candidate.RawNoticeId == link.ProcurementRawNoticeId)
        {
            return;
        }

        throw new AtlasException("Amount candidate does not belong to this lifecycle link.");
    }

    private static IReadOnlyList<AmountCandidate> OrderCandidates(IEnumerable<AmountCandidate> candidates)
    {
        return candidates
            .Where(candidate => !BidOpsOutcomeRecordPolicy.IsNonAwardSupplierName(candidate.SupplierName))
            .Where(candidate => !IsLowContextTextNoiseCandidate(candidate))
            .OrderBy(x => StatusSortKey(x.Status))
            .ThenByDescending(x => x.IsPotentialFinalAmount)
            .ThenByDescending(x => x.Confidence)
            .ThenBy(x => x.SourceKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.SourceLocation, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)
            .ToList();
    }

    private static bool IsLowContextTextNoiseCandidate(AmountCandidate candidate)
    {
        var isTextScan = candidate.SourceKind is BidOpsAmountCandidateSourceKinds.RawNoticeText or BidOpsAmountCandidateSourceKinds.RawAttachmentText;
        if (!isTextScan ||
            !string.Equals(candidate.AmountType, BidOpsAmountCandidateTypes.Unknown, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(candidate.LotNo) &&
               string.IsNullOrWhiteSpace(candidate.LotName) &&
               string.IsNullOrWhiteSpace(candidate.PackageNo) &&
               string.IsNullOrWhiteSpace(candidate.PackageName) &&
               string.IsNullOrWhiteSpace(candidate.SupplierName);
    }

    private static int StatusSortKey(string status)
    {
        return status switch
        {
            BidOpsAmountCandidateStatuses.Selected => 0,
            BidOpsAmountCandidateStatuses.Recommended => 1,
            BidOpsAmountCandidateStatuses.Candidate => 2,
            BidOpsAmountCandidateStatuses.Unresolved => 3,
            BidOpsAmountCandidateStatuses.Rejected => 4,
            _ => 5
        };
    }

    private static string ResolveOutcomeAmountType(OutcomeSupplierRecord record)
    {
        if (record.OutcomeType == BidOpsOutcomeTypes.Candidate)
            return BidOpsAmountCandidateTypes.FinalQuote;

        var signal = string.Join(' ', record.NoticeTitle, record.NoticeType, record.EvidenceText);
        if (signal.Contains("成交", StringComparison.Ordinal))
            return BidOpsAmountCandidateTypes.DealAmount;

        return BidOpsAmountCandidateTypes.WinningAmount;
    }

    private static bool TryExtractOutcomeServiceFeeCandidate(
        OutcomeSupplierRecord record,
        string rawText,
        out OutcomeServiceFeeCandidate candidate)
    {
        candidate = default;
        if (record.AwardAmount.HasValue ||
            record.ProcurementAgencyServiceFeeAmount.HasValue ||
            string.IsNullOrWhiteSpace(record.EvidenceText) ||
            !RawNoticeDeclaresOutcomeServiceFeeTenThousandYuan(rawText))
        {
            return false;
        }

        var evidence = BidOpsTextQuality.CleanExtractedValue(record.EvidenceText);
        if (string.IsNullOrWhiteSpace(evidence) || !evidence.Contains('|', StringComparison.Ordinal))
            return false;

        var match = OutcomeServiceFeeTailAmountRegex.Match(evidence);
        if (!match.Success ||
            !decimal.TryParse(
                match.Groups["amount"].Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount) ||
            amount <= 0m)
        {
            return false;
        }

        candidate = new OutcomeServiceFeeCandidate(
            match.Groups["amount"].Value,
            Math.Round(amount * 10_000m, 2),
            $"{evidence}\n表头：中标服务费（万元）");
        return true;
    }

    private static bool RawNoticeDeclaresOutcomeServiceFeeTenThousandYuan(string? rawText)
    {
        var normalized = BidOpsTextQuality.CleanExtractedValue(rawText);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var compact = normalized
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal);
        return ContainsAny(compact, "中标服务费（万元）", "中标服务费(万元)", "成交服务费（万元）", "成交服务费(万元)") ||
               (ContainsAny(compact, "中标服务费", "成交服务费") &&
                ContainsAny(compact, "金额单位：万元", "金额单位:万元", "单位：万元", "单位:万元", "（万元）", "(万元)", "万元"));
    }

    private static string BuildProcurementEvidence(ProcurementDetailStaging detail, string fieldName)
    {
        return string.Join(
            " / ",
            new[]
            {
                detail.SourceSheetName,
                detail.TableIndex.HasValue ? $"table:{detail.TableIndex.Value.ToString(CultureInfo.InvariantCulture)}" : string.Empty,
                detail.RowIndex.HasValue ? $"row:{detail.RowIndex.Value.ToString(CultureInfo.InvariantCulture)}" : string.Empty,
                detail.ProjectCode,
                detail.LotNo,
                detail.LotName,
                detail.PackageNo,
                detail.PackageName,
                fieldName
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string NormalizeAmountType(string value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized switch
        {
            BidOpsAmountCandidateTypes.WinningAmount or
            BidOpsAmountCandidateTypes.DealAmount or
            BidOpsAmountCandidateTypes.WinningPrice or
            BidOpsAmountCandidateTypes.DealPrice or
            BidOpsAmountCandidateTypes.QuoteAmount or
            BidOpsAmountCandidateTypes.BidQuote or
            BidOpsAmountCandidateTypes.ResponseQuote or
            BidOpsAmountCandidateTypes.FinalQuote or
            BidOpsAmountCandidateTypes.TotalQuote or
            BidOpsAmountCandidateTypes.BudgetAmount or
            BidOpsAmountCandidateTypes.CeilingPrice or
            BidOpsAmountCandidateTypes.AgencyFee or
            BidOpsAmountCandidateTypes.Deposit or
            BidOpsAmountCandidateTypes.UnitPrice or
            BidOpsAmountCandidateTypes.Rate or
            BidOpsAmountCandidateTypes.DiscountRate or
            BidOpsAmountCandidateTypes.ReductionRate or
            BidOpsAmountCandidateTypes.Unknown => normalized,
            _ => BidOpsAmountCandidateTypes.Unknown
        };
    }

    private static AmountCandidateDto MapAmountCandidate(AmountCandidate candidate)
    {
        var evidence = BuildAmountCandidateEvidence(candidate);
        return new AmountCandidateDto
        {
            Id = candidate.Id,
            LifecyclePackageLinkId = candidate.LifecyclePackageLinkId,
            RawNoticeId = candidate.RawNoticeId,
            ResultRawNoticeId = candidate.ResultRawNoticeId,
            RawAttachmentId = candidate.RawAttachmentId,
            OutcomeSupplierRecordId = candidate.OutcomeSupplierRecordId,
            ProcurementDetailStagingId = candidate.ProcurementDetailStagingId,
            TenderPackageId = candidate.TenderPackageId,
            SourceKind = candidate.SourceKind,
            SourceNoticeType = candidate.SourceNoticeType,
            SourceTitle = candidate.SourceTitle,
            SourceFileName = candidate.SourceFileName,
            SourceLocation = candidate.SourceLocation,
            ProjectCode = candidate.ProjectCode,
            ProjectName = candidate.ProjectName,
            LotNo = candidate.LotNo,
            LotName = candidate.LotName,
            PackageNo = candidate.PackageNo,
            PackageName = candidate.PackageName,
            SupplierName = candidate.SupplierName,
            AmountType = candidate.AmountType,
            AmountRaw = candidate.AmountRaw,
            AmountValue = candidate.AmountValue,
            AmountUnit = candidate.AmountUnit,
            Currency = candidate.Currency,
            IsPotentialFinalAmount = candidate.IsPotentialFinalAmount,
            Confidence = candidate.Confidence,
            Status = candidate.Status,
            RejectReason = candidate.RejectReason,
            EvidenceText = candidate.EvidenceText,
            ContextText = candidate.ContextText,
            EvidenceSource = evidence.Source,
            EvidenceRowText = evidence.RowText,
            EvidenceHeaderText = evidence.HeaderText,
            EvidenceUnitText = evidence.UnitText,
            EvidenceUnitScale = evidence.UnitScale,
            EvidenceHasTenThousandYuanUnit = evidence.HasTenThousandYuanUnit,
            ManualRemark = candidate.ManualRemark,
            SelectedBy = candidate.SelectedBy,
            SelectedAt = candidate.SelectedAt,
            RejectedBy = candidate.RejectedBy,
            RejectedAt = candidate.RejectedAt,
            CreatedAt = candidate.CreatedAt,
            UpdatedAt = candidate.UpdatedAt
        };
    }

    private static AmountCandidateEvidence BuildAmountCandidateEvidence(AmountCandidate candidate)
    {
        var evidenceText = candidate.EvidenceText ?? string.Empty;
        var contextText = candidate.ContextText ?? string.Empty;
        var headerText = ExtractEvidenceLabel(evidenceText, "表头：");
        var rowText = ExtractEvidenceRowText(evidenceText, headerText);
        var source = BuildEvidenceSource(candidate);
        var unitText = ResolveEvidenceUnitText(candidate, headerText, contextText, evidenceText);
        var hasTenThousandYuanUnit = ContainsAny(headerText, "万元", "万") ||
                                     (!string.IsNullOrWhiteSpace(headerText) &&
                                      ContainsAny(unitText, "万元", "万")) ||
                                     ContainsAny(candidate.AmountUnit, "万元");

        decimal? unitScale = unitText switch
        {
            var value when ContainsAny(value, "亿元", "亿") => 100_000_000m,
            var value when ContainsAny(value, "万元", "万") => 10_000m,
            var value when ContainsAny(value, "元") => 1m,
            _ => null
        };

        return new AmountCandidateEvidence(
            source,
            rowText,
            headerText,
            unitText,
            unitScale,
            hasTenThousandYuanUnit);
    }

    private static string BuildEvidenceSource(AmountCandidate candidate)
    {
        var kind = candidate.SourceKind switch
        {
            BidOpsAmountCandidateSourceKinds.OutcomeSupplierRecord => "中标/成交明细解析记录",
            BidOpsAmountCandidateSourceKinds.ProcurementDetailStaging => "前置公告暂存包件",
            BidOpsAmountCandidateSourceKinds.RawNoticeText => "公告正文",
            BidOpsAmountCandidateSourceKinds.RawAttachmentText => "附件解析文本",
            _ => candidate.SourceKind
        };
        return string.Join(
            " / ",
            new[]
            {
                kind,
                candidate.RawAttachmentId.HasValue ? $"附件 {candidate.RawAttachmentId.Value.ToString(CultureInfo.InvariantCulture)}" : string.Empty,
                !string.IsNullOrWhiteSpace(candidate.SourceFileName) ? candidate.SourceFileName : string.Empty,
                candidate.RawNoticeId > 0 ? $"RawNotice {candidate.RawNoticeId.ToString(CultureInfo.InvariantCulture)}" : string.Empty
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string ResolveEvidenceUnitText(
        AmountCandidate candidate,
        string headerText,
        string contextText,
        string evidenceText)
    {
        var signal = string.Join(
            " ",
            new[] { headerText, candidate.AmountUnit, contextText, evidenceText }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (ContainsAny(signal, "亿元", "亿"))
            return "亿元";
        if (ContainsAny(signal, "万元", "万"))
            return "万元";
        if (ContainsAny(signal, "元"))
            return "元";
        if (candidate.AmountUnit is "rate" or "discount")
            return candidate.AmountUnit;
        return candidate.AmountUnit;
    }

    private static string ExtractEvidenceLabel(string evidenceText, string label)
    {
        if (string.IsNullOrWhiteSpace(evidenceText))
            return string.Empty;

        var index = evidenceText.IndexOf(label, StringComparison.Ordinal);
        if (index < 0)
            return string.Empty;

        var valueStart = index + label.Length;
        var valueEnd = evidenceText.IndexOf('\n', valueStart);
        if (valueEnd < 0)
            valueEnd = evidenceText.Length;

        return Truncate(evidenceText[valueStart..valueEnd], 500);
    }

    private static string ExtractEvidenceRowText(string evidenceText, string headerText)
    {
        if (string.IsNullOrWhiteSpace(evidenceText))
            return string.Empty;
        if (string.IsNullOrWhiteSpace(headerText))
            return Truncate(evidenceText, 1000);

        var headerIndex = evidenceText.IndexOf("表头：", StringComparison.Ordinal);
        if (headerIndex <= 0)
            return Truncate(evidenceText, 1000);

        return Truncate(evidenceText[..headerIndex], 1000);
    }

    private static string Truncate(string? value, int maxLength)
    {
        value = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        return candidates.Any(x => value.Contains(x, StringComparison.Ordinal));
    }

    private static string ComputeSourceHash(params string[] values)
    {
        var raw = string.Join('\u001f', values.Select(x => x ?? string.Empty));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool IsAmountCandidateSourceHashDuplicate(DbUpdateException ex)
    {
        var message = ex.ToString();
        return message.Contains("IX_bidops_amount_candidate_Tenant_SourceHash", StringComparison.OrdinalIgnoreCase) ||
               (message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("SourceHash", StringComparison.OrdinalIgnoreCase));
    }

    private readonly record struct AmountCandidateSourceHashKey(long TenantId, string SourceHash);

    private readonly record struct OutcomeServiceFeeCandidate(
        string AmountRaw,
        decimal AmountValue,
        string EvidenceText);

    private readonly record struct AmountCandidateEvidence(
        string Source,
        string RowText,
        string HeaderText,
        string UnitText,
        decimal? UnitScale,
        bool HasTenThousandYuanUnit);
}
