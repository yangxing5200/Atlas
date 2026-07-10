using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsOutcomeSupplierExtractionService : IBidOpsOutcomeSupplierExtractionService
{
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<RawAttachment> _rawAttachments;
    private readonly IRepository<Notice> _notices;
    private readonly IRepository<TenderPackage> _packages;
    private readonly IRepository<NoticeStaging> _noticeStaging;
    private readonly IRepository<PackageStaging> _packageStaging;
    private readonly IRepository<ReviewTask> _reviewTasks;
    private readonly IRepository<OutcomeSupplierRecord> _records;
    private readonly IBidOpsFileStore _fileStore;
    private readonly IBidOpsOutcomeSupplierAiExtractionService _aiExtraction;
    private readonly IBidOpsOrganizationMasterDataService _organizationMasterData;
    private readonly IBidOpsReviewQualityService _reviewQuality;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdGenerator _idGenerator;
    private readonly ILogger<BidOpsOutcomeSupplierExtractionService> _logger;

    public BidOpsOutcomeSupplierExtractionService(
        IRepository<RawNotice> rawNotices,
        IRepository<RawAttachment> rawAttachments,
        IRepository<Notice> notices,
        IRepository<TenderPackage> packages,
        IRepository<NoticeStaging> noticeStaging,
        IRepository<PackageStaging> packageStaging,
        IRepository<ReviewTask> reviewTasks,
        IRepository<OutcomeSupplierRecord> records,
        IBidOpsFileStore fileStore,
        IBidOpsOutcomeSupplierAiExtractionService aiExtraction,
        IBidOpsOrganizationMasterDataService organizationMasterData,
        IBidOpsReviewQualityService reviewQuality,
        IUnitOfWork unitOfWork,
        IIdGenerator idGenerator,
        ILogger<BidOpsOutcomeSupplierExtractionService> logger)
    {
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _rawAttachments = rawAttachments ?? throw new ArgumentNullException(nameof(rawAttachments));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _noticeStaging = noticeStaging ?? throw new ArgumentNullException(nameof(noticeStaging));
        _packageStaging = packageStaging ?? throw new ArgumentNullException(nameof(packageStaging));
        _reviewTasks = reviewTasks ?? throw new ArgumentNullException(nameof(reviewTasks));
        _records = records ?? throw new ArgumentNullException(nameof(records));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _aiExtraction = aiExtraction ?? throw new ArgumentNullException(nameof(aiExtraction));
        _organizationMasterData = organizationMasterData ?? throw new ArgumentNullException(nameof(organizationMasterData));
        _reviewQuality = reviewQuality ?? throw new ArgumentNullException(nameof(reviewQuality));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OutcomeSupplierExtractionResultDto> ExtractRawNoticeAsync(
        long rawNoticeId,
        CancellationToken ct = default)
    {
        return await ExtractRawNoticeAsync(rawNoticeId, null, ct);
    }

    public async Task<OutcomeSupplierExtractionResultDto> ExtractRawNoticeAsync(
        long rawNoticeId,
        string? reviewerPrompt,
        CancellationToken ct = default)
    {
        var rawQuery = await _rawNotices.QueryAsync(ct);
        var raw = await rawQuery.Where(x => x.Id == rawNoticeId).FirstOrDefaultAsync(ct);
        if (raw == null)
            throw new AtlasException($"BidOps raw notice does not exist: {rawNoticeId}");

        var source = await BuildAiSourceAsync(raw, ct);
        var sourceText = BuildCombinedSourceText(source);
        var isOutcomeNotice = BidOpsOutcomeSupplierTextParser.LooksLikeOutcomeNotice(raw.Title, raw.NoticeType, sourceText);
        var hasReviewerPrompt = !string.IsNullOrWhiteSpace(reviewerPrompt);
        var shouldAttemptExtraction = ShouldAttemptOutcomeExtraction(isOutcomeNotice, reviewerPrompt);
        if (!isOutcomeNotice && hasReviewerPrompt)
        {
            _logger.LogInformation(
                "BidOps outcome supplier extraction will call AI for raw notice {RawNoticeId} because reviewer provided a correction prompt, although automatic outcome detection did not match.",
                raw.Id);
        }

        var selection = shouldAttemptExtraction
            ? await BuildOutcomeExtractsAsync(raw, source, sourceText, reviewerPrompt, ct)
            : OutcomeExtractSelection.Empty;
        var extracts = selection.Extracts;
        var sourceCounts = CountOutcomeExtractSources(extracts);

        var existingQuery = await _records.QueryTrackingAsync(raw.TenantId, ct);
        var existing = await existingQuery.Where(x => x.RawNoticeId == raw.Id).ToListAsync(ct);
        if (existing.Count > 0)
        {
            await _records.RemoveRangeAsync(existing, raw.TenantId, ct);
            await _unitOfWork.SaveChangesAsync(raw.TenantId, ct);
        }

        if (extracts.Count == 0)
        {
            if (shouldAttemptExtraction)
            {
                await ApplyOutcomeQualityIfReviewTaskExistsAsync(raw, [], ct);
                await _unitOfWork.SaveChangesAsync(raw.TenantId, ct);
            }

            return new OutcomeSupplierExtractionResultDto
            {
                RawNoticeId = raw.Id,
                IsOutcomeNotice = shouldAttemptExtraction,
                ExtractedCount = 0,
                SavedCount = 0,
                CandidateCount = selection.CandidateCount,
                MergeGroupCount = selection.MergeGroupCount,
                MergedCandidateCount = selection.MergedCandidateCount,
                SourceCounts = sourceCounts,
                LotNoValidationCounts = CountOutcomeExtractLotNoValidations(extracts),
                StrengthCounts = CountOutcomeExtractStrengths(extracts),
                Message = shouldAttemptExtraction
                    ? (hasReviewerPrompt ? "AI Provider 未返回可保存的中标/候选厂家线索，请查看后台任务的 AI 返回诊断。" : "未识别到中标/候选厂家线索。")
                    : "非结果/候选公示，已跳过厂家线索抽取。"
            };
        }

        var context = await LoadNoticeContextAsync(raw, ct);
        var now = DateTime.UtcNow;
        var records = new List<OutcomeSupplierRecord>();
        var sanitizedExtracts = new List<BidOpsOutcomeSupplierExtract>();
        var extractionOrder = 0;

        foreach (var rawExtract in extracts)
        {
            var extract = SanitizeOutcomeExtractForPersistence(rawExtract, sourceText);
            sanitizedExtracts.Add(extract);
            var supplierName = Truncate(BidOpsTextQuality.CleanExtractedValue(extract.SupplierName), 300);
            var supplierNameNormalized = Truncate(BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(supplierName), 191);
            if (string.IsNullOrWhiteSpace(supplierName) || string.IsNullOrWhiteSpace(supplierNameNormalized))
                continue;

            var package = context.FindPackage(extract);
            var packageNoForHash = FirstMeaningful(extract.PackageNo, package.PackageNo);
            var lotNoForHash = FirstMeaningful(extract.LotNo, package.LotNo);
            var lotNameForHash = FirstMeaningful(extract.LotName, package.LotName);
            var outcomeType = NormalizeOutcomeType(extract.OutcomeType);
            outcomeType = BidOpsOutcomeRecordPolicy.NormalizeOutcomeTypeForPersistence(
                outcomeType,
                supplierName,
                extract.EvidenceText,
                outcomeType);
            var isNonAwardOutcome = outcomeType == BidOpsOutcomeTypes.Failed;
            var sourceHash = ComputeSourceHash(
                raw.Id.ToString(),
                supplierNameNormalized,
                packageNoForHash,
                lotNoForHash,
                lotNameForHash,
                outcomeType,
                extract.Rank?.ToString() ?? string.Empty,
                extract.EvidenceText);

            if (records.Any(x => x.SourceHash == sourceHash))
                continue;

            records.Add(new OutcomeSupplierRecord
            {
                Id = _idGenerator.NextId(),
                TenantId = raw.TenantId,
                RawNoticeId = raw.Id,
                NoticeId = context.NoticeId,
                TenderPackageId = package.TenderPackageId,
                SourceUrl = Truncate(raw.DetailUrl, 1500),
                NoticeTitle = Truncate(raw.Title, 500),
                NoticeType = Truncate(raw.NoticeType, 64),
                ProjectName = Truncate(extract.ProjectName, 500),
                ProjectCode = Truncate(NormalizeProjectCodeForPersistence(FirstMeaningful(extract.ProjectCode, context.ProjectCode)), 128),
                BuyerName = Truncate(FirstMeaningful(extract.BuyerName, context.BuyerName), 300),
                Region = Truncate(context.Region, 128),
                PublishTime = context.PublishTime ?? raw.PublishTime,
                LotNo = Truncate(extract.LotNo, 128),
                LotName = Truncate(FirstMeaningful(extract.LotName, package.LotName), 300),
                PackageNo = Truncate(FirstMeaningful(extract.PackageNo, package.PackageNo), 128),
                PackageName = Truncate(FirstPackageNameDistinctFromProject(
                    extract.ProjectName,
                    extract.PackageName,
                    package.PackageName), 500),
                Category = Truncate(FirstMeaningful(extract.Category, package.Category), 128),
                SupplierName = supplierName,
                SupplierNameNormalized = supplierNameNormalized,
                OutcomeType = outcomeType,
                Rank = extract.Rank,
                AwardAmount = isNonAwardOutcome ? null : extract.AwardAmount,
                ProcurementAgencyServiceFeeAmount = isNonAwardOutcome ? null : extract.ProcurementAgencyServiceFeeAmount,
                ExtractionOrder = extractionOrder++,
                Currency = "CNY",
                EvidenceText = Truncate(extract.EvidenceText, 2000),
                ExtractionConfidence = Math.Clamp(extract.Confidence, 0m, 1m),
                SourceHash = sourceHash,
                CreatedAt = now
            });
        }

        if (records.Count > 0)
        {
            var syncRecords = records
                .Where(record => !BidOpsOutcomeRecordPolicy.IsNonAwardOutcome(record))
                .ToList();
            var syncResult = syncRecords.Count == 0
                ? new BidOpsOrganizationMasterDataSyncResult()
                : await _organizationMasterData.SyncOutcomeOrganizationsAsync(raw.TenantId, syncRecords, ct);
            await _records.AddRangeAsync(records, raw.TenantId, ct);
            await ApplyOutcomeQualityIfReviewTaskExistsAsync(raw, records, ct);
            await _unitOfWork.SaveChangesAsync(raw.TenantId, ct);

            _logger.LogInformation(
                "BidOps outcome supplier extraction saved {SavedCount} records for raw notice {RawNoticeId}; buyer created={BuyerCreated}; supplier created={SupplierCreated}.",
                records.Count,
                raw.Id,
                syncResult.BuyerCreatedCount,
                syncResult.SupplierCreatedCount);

            return new OutcomeSupplierExtractionResultDto
            {
                RawNoticeId = raw.Id,
                IsOutcomeNotice = true,
                ExtractedCount = extracts.Count,
                SavedCount = records.Count,
                CandidateCount = selection.CandidateCount,
                MergeGroupCount = selection.MergeGroupCount,
                MergedCandidateCount = selection.MergedCandidateCount,
                BuyerCreatedCount = syncResult.BuyerCreatedCount,
                BuyerUpdatedCount = syncResult.BuyerUpdatedCount,
                SupplierCreatedCount = syncResult.SupplierCreatedCount,
                SupplierUpdatedCount = syncResult.SupplierUpdatedCount,
                SourceCounts = sourceCounts,
                LotNoValidationCounts = CountOutcomeExtractLotNoValidations(sanitizedExtracts),
                StrengthCounts = CountOutcomeExtractStrengths(sanitizedExtracts),
                Message = $"已保存 {records.Count} 条公开结果厂家线索，并同步采购方/厂家主数据。"
            };
        }

        _logger.LogInformation(
            "BidOps outcome supplier extraction saved {SavedCount} records for raw notice {RawNoticeId}.",
            records.Count,
            raw.Id);
        await ApplyOutcomeQualityIfReviewTaskExistsAsync(raw, records, ct);
        await _unitOfWork.SaveChangesAsync(raw.TenantId, ct);

        return new OutcomeSupplierExtractionResultDto
        {
            RawNoticeId = raw.Id,
            IsOutcomeNotice = true,
            ExtractedCount = extracts.Count,
            SavedCount = records.Count,
            CandidateCount = selection.CandidateCount,
            MergeGroupCount = selection.MergeGroupCount,
            MergedCandidateCount = selection.MergedCandidateCount,
            SourceCounts = sourceCounts,
            LotNoValidationCounts = CountOutcomeExtractLotNoValidations(sanitizedExtracts),
            StrengthCounts = CountOutcomeExtractStrengths(sanitizedExtracts),
            Message = records.Count == 0 ? "识别到文本片段，但未形成有效厂家线索。" : $"已保存 {records.Count} 条公开结果厂家线索。"
        };
    }

    public async Task<OutcomeSupplierRebuildDryRunResultDto> DryRunRawNoticeAsync(
        long rawNoticeId,
        string? reviewerPrompt,
        CancellationToken ct = default)
    {
        var rawQuery = await _rawNotices.QueryAsync(ct);
        var raw = await rawQuery.Where(x => x.Id == rawNoticeId).FirstOrDefaultAsync(ct);
        if (raw == null)
            throw new AtlasException($"BidOps raw notice does not exist: {rawNoticeId}");

        var existingQuery = await _records.QueryAsync(raw.TenantId, ct);
        var existingCountLong = await existingQuery.Where(x => x.RawNoticeId == raw.Id).CountAsync(ct);
        var existingCount = existingCountLong > int.MaxValue ? int.MaxValue : (int)existingCountLong;

        var source = await BuildAiSourceAsync(raw, ct);
        var sourceText = BuildCombinedSourceText(source);
        var isOutcomeNotice = BidOpsOutcomeSupplierTextParser.LooksLikeOutcomeNotice(raw.Title, raw.NoticeType, sourceText);
        var shouldAttemptExtraction = ShouldAttemptOutcomeExtraction(isOutcomeNotice, reviewerPrompt);
        if (!shouldAttemptExtraction)
        {
            return new OutcomeSupplierRebuildDryRunResultDto
            {
                RawNoticeId = raw.Id,
                IsOutcomeNotice = false,
                ExistingCount = existingCount,
                PreviewExtractedCount = 0,
                PreviewSavedCount = 0,
                DeltaCount = -existingCount,
                StrengthCounts = CountOutcomeExtractStrengths([]),
                Message = "非结果/候选公示，dry-run 未执行厂家线索重建。"
            };
        }

        var selection = await BuildOutcomeExtractsAsync(raw, source, sourceText, reviewerPrompt, ct);
        var extracts = selection.Extracts;
        var previewExtracts = extracts
            .Select(x => SanitizeOutcomeExtractForPersistence(x, sourceText))
            .Where(IsPersistableOutcomeExtract)
            .ToList();

        return new OutcomeSupplierRebuildDryRunResultDto
        {
            RawNoticeId = raw.Id,
            IsOutcomeNotice = true,
            ExistingCount = existingCount,
            PreviewExtractedCount = extracts.Count,
            PreviewSavedCount = previewExtracts.Count,
            CandidateCount = selection.CandidateCount,
            MergeGroupCount = selection.MergeGroupCount,
            MergedCandidateCount = selection.MergedCandidateCount,
            DeltaCount = previewExtracts.Count - existingCount,
            SourceCounts = CountOutcomeExtractSources(previewExtracts),
            LotNoValidationCounts = CountOutcomeExtractLotNoValidations(previewExtracts),
            StrengthCounts = CountOutcomeExtractStrengths(previewExtracts),
            Message = $"Dry-run 完成：当前 {existingCount} 条，预览重建 {previewExtracts.Count} 条，未修改业务表。"
        };
    }

    private static Dictionary<string, int> CountOutcomeExtractSources(IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts)
    {
        return extracts
            .GroupBy(x => string.IsNullOrWhiteSpace(x.SourceType)
                ? BidOpsOutcomeSupplierExtractSourceTypes.Unknown
                : x.SourceType)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
    }

    private static Dictionary<string, int> CountOutcomeExtractLotNoValidations(IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts)
    {
        return extracts
            .GroupBy(x => string.IsNullOrWhiteSpace(x.LotNoValidationStatus)
                ? BidOpsLotNoValidationStatuses.NotValidated
                : x.LotNoValidationStatus)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
    }

    private static Dictionary<string, int> CountOutcomeExtractStrengths(IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts)
    {
        return extracts
            .GroupBy(x => string.IsNullOrWhiteSpace(x.StrengthClass)
                ? BidOpsOutcomeSupplierStrengthClasses.NotScored
                : x.StrengthClass)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
    }

    private async Task ApplyOutcomeQualityIfReviewTaskExistsAsync(
        RawNotice raw,
        IReadOnlyCollection<OutcomeSupplierRecord> records,
        CancellationToken ct)
    {
        var stagingQuery = await _noticeStaging.QueryTrackingAsync(raw.TenantId, ct);
        var staging = await stagingQuery
            .Where(x => x.RawNoticeId == raw.Id)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (staging == null)
            return;

        var taskQuery = await _reviewTasks.QueryTrackingAsync(raw.TenantId, ct);
        var task = await taskQuery
            .Where(x => x.BizType == "NoticeStaging" && x.BizId == staging.Id)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (task == null)
            return;

        await _reviewQuality.ApplyOutcomeQualityAsync(task, raw, staging, records, ct);
    }

    private async Task<OutcomeExtractSelection> BuildOutcomeExtractsAsync(
        RawNotice raw,
        BidOpsNoticeAiExtractionRequest source,
        string sourceText,
        string? reviewerPrompt,
        CancellationToken ct)
    {
        var pdfTableExtracts = BidOpsPdfTableOutcomeParser.Extract(raw.Title, raw.NoticeType, sourceText);
        var deterministic = BidOpsOutcomeSupplierExtractBuilder.Extract(
            raw.Title,
            raw.NoticeType,
            raw.DetailUrl,
            raw.PublishTime,
            RemovePdfTableMarkdownSections(sourceText),
            raw.Id)
            .Concat(pdfTableExtracts)
            .ToList();
        var outcomeSource = RemovePdfTableMarkdownSections(source);
        var outcomeSourceText = BuildCombinedSourceText(outcomeSource);
        var forceAwardedOutcome = IsFinalAwardOutcomeNotice(raw.Title, raw.NoticeType, outcomeSourceText);

        var aiExtracts = await _aiExtraction.ExtractAsync(
            new BidOpsOutcomeSupplierAiExtractionRequest(
                raw.Title,
                raw.NoticeType,
                raw.DetailUrl,
                raw.PublishTime,
                outcomeSource.Text,
                deterministic,
                reviewerPrompt,
                outcomeSource.Html,
                outcomeSource.Attachments),
            ct);

        return SelectOutcomeExtractsForPersistence(
            SanitizeOutcomeExtractsForPersistence(deterministic, outcomeSourceText, forceAwardedOutcome),
            SanitizeOutcomeExtractsForPersistence(aiExtracts, outcomeSourceText, forceAwardedOutcome),
            reviewerPrompt);
    }

    private static bool ShouldAttemptOutcomeExtraction(bool looksLikeOutcomeNotice, string? reviewerPrompt)
    {
        return looksLikeOutcomeNotice || !string.IsNullOrWhiteSpace(reviewerPrompt);
    }

    private static bool IsFinalAwardOutcomeNotice(
        string title,
        string noticeType,
        string sourceText)
    {
        if (ContainsAny(noticeType ?? string.Empty, "AwardAnnouncement", "ResultAnnouncement"))
            return true;

        var titleSignal = title ?? string.Empty;
        if (ContainsAny(titleSignal, "中标候选人", "成交候选人", "候选人公示", "推荐的中标候选", "推荐的成交候选"))
            return false;

        if (ContainsAny(titleSignal, "中标结果", "成交结果", "中标公告", "成交公告", "中标人名单"))
            return true;

        var bodySignal = sourceText[..Math.Min(sourceText.Length, 2000)];
        return ContainsAny(bodySignal, "中标结果", "成交结果", "中标公告", "成交公告", "中标人名单", "现将中标人名单公告");
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> ChooseOutcomeExtractsForPersistence(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> deterministic,
        IReadOnlyList<BidOpsOutcomeSupplierExtract> aiExtracts,
        string? reviewerPrompt)
    {
        return SelectOutcomeExtractsForPersistence(deterministic, aiExtracts, reviewerPrompt).Extracts;
    }

    private static OutcomeExtractSelection SelectOutcomeExtractsForPersistence(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> deterministic,
        IReadOnlyList<BidOpsOutcomeSupplierExtract> aiExtracts,
        string? reviewerPrompt)
    {
        var selected = !string.IsNullOrWhiteSpace(reviewerPrompt)
            ? MergeReviewerPromptOutcomeExtracts(deterministic, aiExtracts)
            : MergeOutcomeExtracts(deterministic, aiExtracts);

        var extracts = AssignExtractionOrder(PruneLessSpecificPackageRows(
            EnrichFragmentedOutcomeSupplierNames(EnrichFragmentedOutcomeLotContext(selected.Extracts))));
        var prunedMergedCount = Math.Max(0, selected.CandidateCount - extracts.Count);
        return selected with
        {
            Extracts = extracts,
            MergeGroupCount = extracts.Count,
            MergedCandidateCount = Math.Max(selected.MergedCandidateCount, prunedMergedCount)
        };
    }

    private static OutcomeExtractSelection MergeReviewerPromptOutcomeExtracts(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> deterministic,
        IReadOnlyList<BidOpsOutcomeSupplierExtract> aiExtracts)
    {
        return MergeOutcomeExtractsByPairwiseScore(deterministic, aiExtracts, useReviewerPromptThresholds: true);
    }

    private static OutcomeExtractSelection MergeOutcomeExtracts(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> deterministic,
        IReadOnlyList<BidOpsOutcomeSupplierExtract> aiExtracts)
    {
        if (aiExtracts.Count == 0)
            return MergeOutcomeExtractsByPairwiseScore(deterministic, [], useReviewerPromptThresholds: false);

        return MergeOutcomeExtractsByPairwiseScore(deterministic, aiExtracts, useReviewerPromptThresholds: false);
    }

    private static OutcomeExtractSelection MergeOutcomeExtractsByPairwiseScore(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> deterministic,
        IReadOnlyList<BidOpsOutcomeSupplierExtract> aiExtracts,
        bool useReviewerPromptThresholds)
    {
        var candidates = new List<(BidOpsOutcomeSupplierExtract Extract, int Index)>();
        foreach (var extract in DedupeOutcomeExtracts(aiExtracts))
            candidates.Add((ScoreOutcomeExtractForPersistence(extract), candidates.Count));
        foreach (var extract in DedupeOutcomeExtracts(deterministic))
            candidates.Add((ScoreOutcomeExtractForPersistence(extract), candidates.Count));

        if (candidates.Count <= 1)
            return new OutcomeExtractSelection(candidates.Select(x => x.Extract).ToList(), candidates.Count, candidates.Count, 0);

        var useRelaxedReviewerPromptThresholds = useReviewerPromptThresholds && aiExtracts.Count > 0;
        var parents = Enumerable.Range(0, candidates.Count).ToArray();
        int Find(int index)
        {
            while (parents[index] != index)
            {
                parents[index] = parents[parents[index]];
                index = parents[index];
            }

            return index;
        }

        void Union(int left, int right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (leftRoot == rightRoot)
                return;

            if (candidates[leftRoot].Index <= candidates[rightRoot].Index)
                parents[rightRoot] = leftRoot;
            else
                parents[leftRoot] = rightRoot;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                var score = ScoreOutcomeExtractPair(candidates[i].Extract, candidates[j].Extract, useRelaxedReviewerPromptThresholds);
                if (score >= ResolveOutcomeMergeThreshold(candidates[i].Extract, candidates[j].Extract, useRelaxedReviewerPromptThresholds))
                    Union(i, j);
            }
        }

        var groups = candidates
            .Select((candidate, CandidateIndex) => (candidate.Extract, candidate.Index, Root: Find(CandidateIndex)))
            .GroupBy(x => x.Root)
            .OrderBy(group => group.Min(x => x.Index))
            .ToList();
        var survivors = groups
            .Select(group => SelectOutcomeMergeSurvivor(group.Select(x => (x.Extract, x.Index)).ToList()))
            .ToList();

        return new OutcomeExtractSelection(
            survivors,
            candidates.Count,
            groups.Count,
            Math.Max(0, candidates.Count - survivors.Count));
    }

    private static BidOpsOutcomeSupplierExtract SelectOutcomeMergeSurvivor(
        IReadOnlyList<(BidOpsOutcomeSupplierExtract Extract, int Index)> group)
    {
        var survivor = group
            .OrderByDescending(x => GetOutcomeExtractSourceTrustScore(x.Extract))
            .ThenByDescending(x => x.Extract.StrengthScore)
            .ThenByDescending(x => HasExplicitLotNo(x.Extract))
            .ThenByDescending(x => HasMeaningfulLotContext(x.Extract))
            .ThenByDescending(x => HasPackageIdentity(x.Extract))
            .ThenByDescending(x => x.Extract.ProcurementAgencyServiceFeeAmount.HasValue)
            .ThenByDescending(x => x.Extract.AwardAmount.HasValue)
            .ThenByDescending(x => x.Extract.Confidence)
            .ThenBy(x => x.Index)
            .First()
            .Extract;

        foreach (var fallback in group
                     .Where(x => !ReferenceEquals(x.Extract, survivor))
                     .OrderByDescending(x => x.Extract.StrengthScore)
                     .ThenBy(x => x.Index)
                     .Select(x => x.Extract))
        {
            MergeCoveredOutcomeFallbackIntoSurvivor(survivor, fallback);
        }

        return survivor;
    }

    private static int ResolveOutcomeMergeThreshold(
        BidOpsOutcomeSupplierExtract left,
        BidOpsOutcomeSupplierExtract right,
        bool useReviewerPromptThresholds)
    {
        if (useReviewerPromptThresholds)
            return 65;

        if (IsAiOutcomeExtract(left) && IsPdfStructuredOutcomeExtract(right) ||
            IsAiOutcomeExtract(right) && IsPdfStructuredOutcomeExtract(left))
        {
            return 65;
        }

        if (IsAiOutcomeExtract(left) || IsAiOutcomeExtract(right))
            return 70;

        if (IsPdfStructuredOutcomeExtract(left) || IsPdfStructuredOutcomeExtract(right))
            return 70;

        return 85;
    }

    private static int ScoreOutcomeExtractPair(
        BidOpsOutcomeSupplierExtract left,
        BidOpsOutcomeSupplierExtract right,
        bool useReviewerPromptThresholds)
    {
        if (ReferenceEquals(left, right) ||
            !PairwiseOutcomeHardCompatible(left, right, useReviewerPromptThresholds))
        {
            return 0;
        }

        var score = 0;
        if (SupplierNameCompatible(left.SupplierName, right.SupplierName))
            score += 40;
        else if (useReviewerPromptThresholds && ReviewerPromptAllowsSupplierOverride(left, right))
            score += 35;

        var leftPackageNo = NormalizeCode(left.PackageNo);
        var rightPackageNo = NormalizeCode(right.PackageNo);
        if (!string.IsNullOrWhiteSpace(leftPackageNo) &&
            !string.IsNullOrWhiteSpace(rightPackageNo) &&
            string.Equals(leftPackageNo, rightPackageNo, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        if (SourceSequenceCompatible(left, right))
            score += 30;

        if (EvidenceRowsCompatible(left, right))
            score += 30;

        if (LotNoPairCompatible(left, right))
            score += 25;

        if (LotNamePairCompatible(left, right))
            score += 15;

        if (AmountsCompatible(left.AwardAmount, right.AwardAmount))
            score += 10;

        if (string.Equals(NormalizeOutcomeType(left.OutcomeType), NormalizeOutcomeType(right.OutcomeType), StringComparison.OrdinalIgnoreCase))
            score += 10;

        if (left.SourcePageNo.HasValue &&
            right.SourcePageNo.HasValue &&
            left.SourcePageNo.Value == right.SourcePageNo.Value)
        {
            score += 10;
        }

        if (CoversWeakOutcomeFallback(left, right) ||
            CoversWeakOutcomeFallback(right, left) ||
            CoversReviewerPromptFallback(left, right) ||
            CoversReviewerPromptFallback(right, left))
        {
            score = Math.Max(score, 90);
        }

        return score;
    }

    private static bool PairwiseOutcomeHardCompatible(
        BidOpsOutcomeSupplierExtract left,
        BidOpsOutcomeSupplierExtract right,
        bool useReviewerPromptThresholds)
    {
        if (!string.Equals(NormalizeOutcomeType(left.OutcomeType), NormalizeOutcomeType(right.OutcomeType), StringComparison.OrdinalIgnoreCase))
            return false;

        if (left.Rank.HasValue && right.Rank.HasValue && left.Rank.Value != right.Rank.Value)
            return false;

        if (!SupplierNameCompatible(left.SupplierName, right.SupplierName) &&
            (!useReviewerPromptThresholds || !ReviewerPromptAllowsSupplierOverride(left, right)))
        {
            return false;
        }

        var leftPackageNo = NormalizeCode(left.PackageNo);
        var rightPackageNo = NormalizeCode(right.PackageNo);
        if (!string.IsNullOrWhiteSpace(leftPackageNo) &&
            !string.IsNullOrWhiteSpace(rightPackageNo) &&
            !string.Equals(leftPackageNo, rightPackageNo, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!LotNoPairCompatible(left, right))
            return false;

        return LotNamePairCompatible(left, right);
    }

    private static bool ReviewerPromptAllowsSupplierOverride(
        BidOpsOutcomeSupplierExtract left,
        BidOpsOutcomeSupplierExtract right)
    {
        var leftPackageNo = NormalizeCode(left.PackageNo);
        var rightPackageNo = NormalizeCode(right.PackageNo);
        if (string.IsNullOrWhiteSpace(leftPackageNo) ||
            string.IsNullOrWhiteSpace(rightPackageNo) ||
            !string.Equals(leftPackageNo, rightPackageNo, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return LotNoPairCompatible(left, right) && LotNamePairCompatible(left, right);
    }

    private static bool SourceSequenceCompatible(
        BidOpsOutcomeSupplierExtract left,
        BidOpsOutcomeSupplierExtract right)
    {
        if (string.IsNullOrWhiteSpace(left.SourceSequenceNo) ||
            string.IsNullOrWhiteSpace(right.SourceSequenceNo) ||
            !string.Equals(left.SourceSequenceNo, right.SourceSequenceNo, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (left.SourcePageNo.HasValue &&
            right.SourcePageNo.HasValue &&
            left.SourcePageNo.Value != right.SourcePageNo.Value)
        {
            return false;
        }

        return true;
    }

    private static bool EvidenceRowsCompatible(
        BidOpsOutcomeSupplierExtract left,
        BidOpsOutcomeSupplierExtract right)
    {
        var leftEvidence = NormalizeEvidenceText(FirstMeaningful(left.SourceRowText, left.EvidenceText));
        var rightEvidence = NormalizeEvidenceText(FirstMeaningful(right.SourceRowText, right.EvidenceText));
        if (string.IsNullOrWhiteSpace(leftEvidence) || string.IsNullOrWhiteSpace(rightEvidence))
            return false;

        return string.Equals(leftEvidence, rightEvidence, StringComparison.OrdinalIgnoreCase) ||
               (leftEvidence.Length >= 12 && rightEvidence.Contains(leftEvidence, StringComparison.OrdinalIgnoreCase)) ||
               (rightEvidence.Length >= 12 && leftEvidence.Contains(rightEvidence, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LotNoPairCompatible(
        BidOpsOutcomeSupplierExtract left,
        BidOpsOutcomeSupplierExtract right)
    {
        var leftLotNo = NormalizeLotNoForWeakMerge(left);
        var rightLotNo = NormalizeLotNoForWeakMerge(right);
        if (string.IsNullOrWhiteSpace(leftLotNo) || string.IsNullOrWhiteSpace(rightLotNo))
            return true;

        return string.Equals(leftLotNo, rightLotNo, StringComparison.OrdinalIgnoreCase) ||
               LotNoFragmentCompatible(leftLotNo, rightLotNo) ||
               LotNoFragmentCompatible(rightLotNo, leftLotNo);
    }

    private static bool LotNamePairCompatible(
        BidOpsOutcomeSupplierExtract left,
        BidOpsOutcomeSupplierExtract right)
    {
        var leftLotName = NormalizeLotNameForWeakMerge(left);
        var rightLotName = NormalizeLotNameForWeakMerge(right);
        if (string.IsNullOrWhiteSpace(leftLotName) || string.IsNullOrWhiteSpace(rightLotName))
            return true;

        return LotNameFragmentCompatible(leftLotName, rightLotName) ||
               LotNameFragmentCompatible(rightLotName, leftLotName);
    }

    private static bool AmountsCompatible(decimal? left, decimal? right)
    {
        if (!left.HasValue || !right.HasValue)
            return false;

        return Math.Abs(left.Value - right.Value) < 0.01m;
    }

    private static bool IsAiOutcomeExtract(BidOpsOutcomeSupplierExtract extract)
    {
        return string.Equals(extract.SourceType, BidOpsOutcomeSupplierExtractSourceTypes.AiOutcomeSuppliers, StringComparison.Ordinal);
    }

    private static bool IsPdfStructuredOutcomeExtract(BidOpsOutcomeSupplierExtract extract)
    {
        return string.Equals(extract.SourceType, BidOpsOutcomeSupplierExtractSourceTypes.PdfStructuredTable, StringComparison.Ordinal);
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> AssignExtractionOrder(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts)
    {
        for (var i = 0; i < extracts.Count; i++)
            extracts[i].ExtractionOrder = i;

        return extracts;
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> DedupeOutcomeExtracts(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts)
    {
        var exactMatches = extracts
            .Where(x => !string.IsNullOrWhiteSpace(x.SupplierName))
            .GroupBy(x => string.Join(
                '\u001f',
                BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(x.SupplierName),
                NormalizeLotIdentity(x),
                NormalizeCode(x.PackageNo),
                NormalizeOutcomeType(x.OutcomeType),
                x.Rank?.ToString() ?? string.Empty))
            .Select(x => x
                .OrderByDescending(HasPackageIdentity)
                .ThenByDescending(item => item.ProcurementAgencyServiceFeeAmount.HasValue)
                .ThenByDescending(item => item.AwardAmount.HasValue)
                .ThenByDescending(item => item.Confidence)
                .First())
            .ToList();

        return PruneCoveredWeakOutcomeExtracts(exactMatches);
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> PruneCoveredWeakOutcomeExtracts(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts)
    {
        var ordered = extracts
            .Select((Extract, Index) => (Extract, Index))
            .OrderByDescending(x => GetOutcomeExtractSourceTrustScore(x.Extract))
            .ThenByDescending(x => HasExplicitLotNo(x.Extract))
            .ThenByDescending(x => HasMeaningfulLotContext(x.Extract))
            .ThenByDescending(x => HasPackageIdentity(x.Extract))
            .ThenByDescending(x => x.Extract.ProcurementAgencyServiceFeeAmount.HasValue)
            .ThenByDescending(x => x.Extract.AwardAmount.HasValue)
            .ThenByDescending(x => x.Extract.Confidence)
            .ThenBy(x => x.Index)
            .ToList();

        var selected = new List<(BidOpsOutcomeSupplierExtract Extract, int Index)>();
        foreach (var item in ordered)
        {
            if (!selected.Any(preferred => CoversWeakOutcomeFallback(preferred.Extract, item.Extract)))
                selected.Add(item);
        }

        return selected
            .OrderBy(x => x.Index)
            .Select(x => x.Extract)
            .ToList();
    }

    private static void MergeCoveredOutcomeFallbackIntoSurvivor(
        BidOpsOutcomeSupplierExtract survivor,
        BidOpsOutcomeSupplierExtract fallback)
    {
        if (string.IsNullOrWhiteSpace(survivor.SourceSequenceNo))
            survivor.SourceSequenceNo = fallback.SourceSequenceNo;
        survivor.SourcePageNo ??= fallback.SourcePageNo;
        if (string.IsNullOrWhiteSpace(survivor.SourceTableTitle))
            survivor.SourceTableTitle = fallback.SourceTableTitle;
        if (string.IsNullOrWhiteSpace(survivor.SourceRowText))
            survivor.SourceRowText = fallback.SourceRowText;
        if (string.IsNullOrWhiteSpace(survivor.ProjectName))
            survivor.ProjectName = fallback.ProjectName;
        if (string.IsNullOrWhiteSpace(survivor.ProjectCode))
            survivor.ProjectCode = fallback.ProjectCode;
        if (string.IsNullOrWhiteSpace(survivor.BuyerName))
            survivor.BuyerName = fallback.BuyerName;
        if (string.IsNullOrWhiteSpace(survivor.LotNo))
        {
            survivor.LotNo = fallback.LotNo;
            survivor.RawLotNo = fallback.RawLotNo;
            survivor.LotNoValidationStatus = fallback.LotNoValidationStatus;
            survivor.LotNoValidationReason = fallback.LotNoValidationReason;
        }
        if (string.IsNullOrWhiteSpace(survivor.LotName))
        {
            survivor.RawLotName = fallback.RawLotName;
            survivor.LotName = fallback.LotName;
        }
        if (string.IsNullOrWhiteSpace(survivor.PackageNo))
        {
            survivor.RawPackageNo = fallback.RawPackageNo;
            survivor.PackageNo = fallback.PackageNo;
        }
        if (string.IsNullOrWhiteSpace(survivor.PackageName))
            survivor.PackageName = fallback.PackageName;
        if (string.IsNullOrWhiteSpace(survivor.Category))
            survivor.Category = fallback.Category;
        survivor.Rank ??= fallback.Rank;
        survivor.AwardAmount ??= fallback.AwardAmount;
        survivor.ProcurementAgencyServiceFeeAmount ??= fallback.ProcurementAgencyServiceFeeAmount;
        if (string.IsNullOrWhiteSpace(survivor.EvidenceText))
            survivor.EvidenceText = fallback.EvidenceText;
        foreach (var item in fallback.FieldEvidence)
        {
            if (!survivor.FieldEvidence.ContainsKey(item.Key) ||
                string.IsNullOrWhiteSpace(survivor.FieldEvidence[item.Key]))
            {
                survivor.FieldEvidence[item.Key] = item.Value;
            }
        }
        foreach (var warning in fallback.Warnings)
        {
            if (!survivor.Warnings.Contains(warning, StringComparer.OrdinalIgnoreCase))
                survivor.Warnings.Add(warning);
        }
        survivor.Confidence = Math.Max(survivor.Confidence, Math.Min(fallback.Confidence, 0.9m));

        ScoreOutcomeExtractForPersistence(survivor);
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> EnrichFragmentedOutcomeLotContext(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts)
    {
        var lotContexts = extracts
            .Where(x => HasExplicitLotNo(x) && HasMeaningfulLotContext(x) && LooksLikeStructuredOutcomeLotNo(x.LotNo))
            .Select(x => new OutcomeLotContext(
                x.LotNo,
                x.LotName,
                NormalizeCode(x.LotNo),
                NormalizeEvidenceText(x.LotName)))
            .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedLotNo) && !string.IsNullOrWhiteSpace(x.NormalizedLotName))
            .GroupBy(x => $"{x.NormalizedLotNo}|{x.NormalizedLotName}")
            .Select(x => x.First())
            .ToList();
        if (lotContexts.Count == 0)
            return extracts;

        foreach (var extract in extracts)
        {
            if (HasExplicitLotNo(extract))
                continue;

            var context = FindFragmentedOutcomeLotContext(extract, lotContexts);
            if (context == null)
                continue;

            // PDF 表格抽取会丢列边界，导致行只剩 “2-02 分标名称 包号 供应商”。
            // 只有同一公告内已有完整且唯一的分标上下文时才补齐，避免跨分标误配。
            extract.LotNo = context.LotNo;
            if (string.IsNullOrWhiteSpace(BidOpsTextQuality.CleanExtractedValue(extract.LotName)))
                extract.LotName = context.LotName;
            extract.Confidence = Math.Min(extract.Confidence, 0.78m);
        }

        return extracts;
    }

    private static OutcomeLotContext? FindFragmentedOutcomeLotContext(
        BidOpsOutcomeSupplierExtract extract,
        IReadOnlyList<OutcomeLotContext> contexts)
    {
        var rowLotName = NormalizeEvidenceText(extract.LotName);
        var leadingLotName = ExtractLeadingLotNameFragmentFromOutcomeEvidence(extract.EvidenceText);
        if (string.IsNullOrWhiteSpace(leadingLotName) && string.IsNullOrWhiteSpace(rowLotName))
            return null;

        var candidates = contexts
            .Where(x =>
                (!string.IsNullOrWhiteSpace(rowLotName) &&
                 string.Equals(rowLotName, x.NormalizedLotName, StringComparison.Ordinal)) ||
                LotNameFragmentCompatible(x.NormalizedLotName, leadingLotName))
            .ToList();
        if (candidates.Count <= 1)
            return candidates.Count == 1 ? candidates[0] : null;

        var lotNoFragment = ExtractLeadingLotNoFragmentFromOutcomeEvidence(extract.EvidenceText);
        if (string.IsNullOrWhiteSpace(lotNoFragment))
            return null;

        var byFragment = candidates
            .Where(x => LotNoFragmentCompatible(x.NormalizedLotNo, lotNoFragment))
            .ToList();
        return byFragment.Count == 1 ? byFragment[0] : null;
    }

    private static string ExtractLeadingLotNoFragmentFromOutcomeEvidence(string? evidenceText)
    {
        foreach (var line in SplitSourceLines(evidenceText))
        {
            var match = Regex.Match(
                line,
                @"^\s*(?:\d{1,4}(?:[.、]|\s+)\s*)?(?<value>(?:[A-Za-z0-9]{1,}[-_/][A-Za-z0-9]{2,}|[A-Za-z0-9]{8,}))(?:\s+|$)",
                RegexOptions.CultureInvariant);
            if (match.Success)
                return NormalizeCode(match.Groups["value"].Value);
        }

        return string.Empty;
    }

    private static string ExtractLeadingLotNameFragmentFromOutcomeEvidence(string? evidenceText)
    {
        foreach (var line in SplitSourceLines(evidenceText))
        {
            var match = Regex.Match(
                line,
                @"^\s*(?:\d{1,4}(?:[.、]|\s+)\s*)?(?<prefix>.*?)(?:包\s*[A-Za-z0-9一二三四五六七八九十]+|第?\s*[A-Za-z0-9一二三四五六七八九十]+\s*包)",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                continue;

            var prefix = BidOpsTextQuality.CleanExtractedValue(match.Groups["prefix"].Value);
            for (var i = 0; i < 4; i++)
            {
                var fragment = Regex.Match(
                    prefix,
                    @"^\s*(?:[A-Za-z0-9]{1,}[-_/][A-Za-z0-9]{2,}|[A-Za-z0-9]{8,})(?:\s+|$)",
                    RegexOptions.CultureInvariant);
                if (!fragment.Success)
                    break;

                prefix = prefix[fragment.Length..];
            }

            var normalized = NormalizeEvidenceText(prefix);
            if (!string.IsNullOrWhiteSpace(normalized))
                return normalized;
        }

        return string.Empty;
    }

    private static bool LotNameFragmentCompatible(string normalizedLotName, string normalizedFragment)
    {
        return normalizedFragment.Length >= 4 &&
               (string.Equals(normalizedLotName, normalizedFragment, StringComparison.Ordinal) ||
                normalizedLotName.Contains(normalizedFragment, StringComparison.Ordinal) ||
                normalizedFragment.Contains(normalizedLotName, StringComparison.Ordinal));
    }

    private static bool LotNoFragmentCompatible(string normalizedLotNo, string normalizedFragment)
    {
        if (normalizedFragment.Length < 4)
            return false;

        return normalizedLotNo.StartsWith(normalizedFragment, StringComparison.Ordinal) ||
               normalizedLotNo.EndsWith(normalizedFragment, StringComparison.Ordinal);
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> EnrichFragmentedOutcomeSupplierNames(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts)
    {
        var fullNames = extracts
            .Select(x => BidOpsSupplierNameNormalizer.Clean(x.SupplierName))
            .Where(HasCompleteSupplierNameSuffix)
            .GroupBy(BidOpsSupplierNameNormalizer.NormalizeForMatch)
            .Select(x => x.First())
            .ToList();
        if (fullNames.Count == 0)
            return extracts;

        foreach (var extract in extracts)
        {
            var current = BidOpsSupplierNameNormalizer.Clean(extract.SupplierName);
            if (string.IsNullOrWhiteSpace(current) || HasCompleteSupplierNameSuffix(current))
                continue;

            var matches = fullNames
                .Where(fullName => ShouldExpandSupplierNameFragment(extract, current, fullName))
                .ToList();
            if (matches.Count != 1)
                continue;

            // PDF 行内断字会把 “有限公司” 切成 “有限公”，只在同一公告内存在唯一完整名称且证据支持时补全。
            extract.SupplierName = matches[0];
        }

        return extracts;
    }

    private static bool ShouldExpandSupplierNameFragment(
        BidOpsOutcomeSupplierExtract extract,
        string current,
        string fullName)
    {
        var normalizedCurrent = BidOpsSupplierNameNormalizer.NormalizeForMatch(current);
        var normalizedFullName = BidOpsSupplierNameNormalizer.NormalizeForMatch(fullName);
        if (normalizedCurrent.Length < 6 ||
            normalizedFullName.Length <= normalizedCurrent.Length ||
            !normalizedFullName.StartsWith(normalizedCurrent, StringComparison.Ordinal))
        {
            return false;
        }

        var evidence = NormalizeEvidenceText(extract.EvidenceText);
        return evidence.Contains(normalizedCurrent + "有限", StringComparison.Ordinal) ||
               evidence.Contains(normalizedCurrent + "公司", StringComparison.Ordinal);
    }

    private static bool HasCompleteSupplierNameSuffix(string? value)
    {
        var cleaned = BidOpsSupplierNameNormalizer.Clean(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return false;

        return Regex.IsMatch(
            cleaned,
            "(?:有限责任公司|股份有限公司|集团有限公司|有限公司|分公司|公司|工厂|勘测设计研究院|工程设计有限公司|研究院|设计院|测绘院|勘测院|勘察院|规划院|科学院|检验院|检测院|计量院|研究所|事务所|大学|学院|学校|医院|中心)$",
            RegexOptions.CultureInvariant);
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> PruneLessSpecificPackageRows(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts)
    {
        var preferredBySupplier = extracts
            .GroupBy(PackageOutcomeRankSupplierKey)
            .Select(group => group
                .OrderByDescending(HasExplicitLotNo)
                .ThenByDescending(HasMeaningfulLotContext)
                .ThenByDescending(HasPackageIdentity)
                .ThenByDescending(item => item.ProcurementAgencyServiceFeeAmount.HasValue)
                .ThenByDescending(item => item.AwardAmount.HasValue)
                .ThenByDescending(item => item.Confidence)
                .First())
            .ToList();

        var weakRows = new HashSet<BidOpsOutcomeSupplierExtract>();
        foreach (var group in preferredBySupplier.GroupBy(PackageOutcomeRankKey))
        {
            var first = group.First();
            if (string.IsNullOrWhiteSpace(NormalizeCode(first.PackageNo)))
                continue;

            var explicitLotRows = group.Where(HasExplicitLotNo).ToList();
            var strongRows = group.Where(HasMeaningfulLotContext).ToList();
            if (strongRows.Count == 0)
                continue;

            foreach (var row in group)
            {
                var lessSpecificLotDuplicate = explicitLotRows.Any(strong => IsLessSpecificLotContextDuplicate(row, strong));
                if (strongRows.Contains(row) && !lessSpecificLotDuplicate)
                    continue;

                if (!HasMeaningfulLotContext(row) ||
                    lessSpecificLotDuplicate ||
                    strongRows.Any(strong => IsSupplierNameFragmentOf(row.SupplierName, strong.SupplierName)))
                {
                    weakRows.Add(row);
                }
            }
        }

        return preferredBySupplier.Where(x => !weakRows.Contains(x)).ToList();
    }

    private static string PackageOutcomeRankSupplierKey(BidOpsOutcomeSupplierExtract extract)
    {
        return string.Join(
            '\u001f',
            NormalizeLotIdentity(extract),
            PackageOutcomeRankKey(extract),
            BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(extract.SupplierName));
    }

    private static bool CoversWeakOutcomeFallback(
        BidOpsOutcomeSupplierExtract preferred,
        BidOpsOutcomeSupplierExtract fallback)
    {
        if (ReferenceEquals(preferred, fallback) ||
            !string.Equals(NormalizeOutcomeType(preferred.OutcomeType), NormalizeOutcomeType(fallback.OutcomeType), StringComparison.OrdinalIgnoreCase) ||
            preferred.Rank != fallback.Rank ||
            !SupplierNameCompatible(preferred.SupplierName, fallback.SupplierName))
        {
            return false;
        }

        var preferredPackageNo = NormalizeCode(preferred.PackageNo);
        var fallbackPackageNo = NormalizeCode(fallback.PackageNo);
        if (!string.IsNullOrWhiteSpace(preferredPackageNo) &&
            !string.IsNullOrWhiteSpace(fallbackPackageNo) &&
            !string.Equals(preferredPackageNo, fallbackPackageNo, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!LotContextCompatibleForWeakMerge(preferred, fallback))
            return false;

        if (GetOutcomeExtractSourceTrustScore(preferred) > GetOutcomeExtractSourceTrustScore(fallback))
            return true;

        return IsLessSpecificLotContextDuplicate(fallback, preferred) ||
               (!HasMeaningfulLotContext(fallback) && HasPackageIdentity(preferred));
    }

    private static bool CoversReviewerPromptFallback(
        BidOpsOutcomeSupplierExtract ai,
        BidOpsOutcomeSupplierExtract fallback)
    {
        if (CoversWeakOutcomeFallback(ai, fallback))
            return true;

        if (!string.Equals(NormalizeOutcomeType(ai.OutcomeType), NormalizeOutcomeType(fallback.OutcomeType), StringComparison.OrdinalIgnoreCase) ||
            ai.Rank != fallback.Rank)
        {
            return false;
        }

        var aiPackageNo = NormalizeCode(ai.PackageNo);
        var fallbackPackageNo = NormalizeCode(fallback.PackageNo);
        if (!string.IsNullOrWhiteSpace(aiPackageNo) &&
            !string.IsNullOrWhiteSpace(fallbackPackageNo) &&
            !string.Equals(aiPackageNo, fallbackPackageNo, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return LotContextCompatibleForWeakMerge(ai, fallback) &&
               (HasMeaningfulLotContext(ai) || HasMeaningfulLotContext(fallback));
    }

    private static bool SupplierNameCompatible(string? left, string? right)
    {
        var normalizedLeft = BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(left ?? string.Empty);
        var normalizedRight = BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(right ?? string.Empty);
        return !string.IsNullOrWhiteSpace(normalizedLeft) &&
               !string.IsNullOrWhiteSpace(normalizedRight) &&
               (string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase) ||
                IsSupplierNameFragmentOf(normalizedLeft, normalizedRight) ||
                IsSupplierNameFragmentOf(normalizedRight, normalizedLeft));
    }

    private static bool IsLessSpecificLotContextDuplicate(
        BidOpsOutcomeSupplierExtract row,
        BidOpsOutcomeSupplierExtract strong)
    {
        if (ReferenceEquals(row, strong) ||
            HasExplicitLotNo(row) ||
            !HasExplicitLotNo(strong) ||
            !SupplierNameCompatible(row.SupplierName, strong.SupplierName))
        {
            return false;
        }

        var rowLotName = NormalizeEvidenceText(row.LotName);
        var strongLotName = NormalizeEvidenceText(strong.LotName);
        if (!HasMeaningfulLotContext(row))
            return true;

        return string.IsNullOrWhiteSpace(rowLotName) ||
               string.IsNullOrWhiteSpace(strongLotName) ||
               string.Equals(rowLotName, strongLotName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LotContextCompatibleForWeakMerge(
        BidOpsOutcomeSupplierExtract preferred,
        BidOpsOutcomeSupplierExtract fallback)
    {
        var preferredLotNo = NormalizeLotNoForWeakMerge(preferred);
        var fallbackLotNo = NormalizeLotNoForWeakMerge(fallback);
        var preferredLotName = NormalizeLotNameForWeakMerge(preferred);
        var fallbackLotName = NormalizeLotNameForWeakMerge(fallback);

        if (!string.IsNullOrWhiteSpace(preferredLotNo) && !string.IsNullOrWhiteSpace(fallbackLotNo))
        {
            if (!string.Equals(preferredLotNo, fallbackLotNo, StringComparison.OrdinalIgnoreCase) &&
                !LotNoFragmentCompatible(preferredLotNo, fallbackLotNo) &&
                !LotNoFragmentCompatible(fallbackLotNo, preferredLotNo))
            {
                return false;
            }

            return true;
        }

        if (!string.IsNullOrWhiteSpace(preferredLotNo) || !string.IsNullOrWhiteSpace(fallbackLotNo))
        {
            var knownLotNo = !string.IsNullOrWhiteSpace(preferredLotNo) ? preferredLotNo : fallbackLotNo;
            var other = !string.IsNullOrWhiteSpace(preferredLotNo) ? fallback : preferred;
            var otherLotName = !string.IsNullOrWhiteSpace(preferredLotNo) ? fallbackLotName : preferredLotName;
            if (ContainsLotNoToken(other.EvidenceText, knownLotNo) ||
                LotNoFragmentCompatible(knownLotNo, ExtractLeadingLotNoFragmentFromOutcomeEvidence(other.EvidenceText)))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(preferredLotName) &&
                !string.IsNullOrWhiteSpace(fallbackLotName))
            {
                return LotNameFragmentCompatible(preferredLotName, fallbackLotName) ||
                       LotNameFragmentCompatible(fallbackLotName, preferredLotName);
            }

            return !HasMeaningfulLotContext(other) || string.IsNullOrWhiteSpace(otherLotName);
        }

        if (!string.IsNullOrWhiteSpace(preferredLotName) && !string.IsNullOrWhiteSpace(fallbackLotName))
        {
            return LotNameFragmentCompatible(preferredLotName, fallbackLotName) ||
                   LotNameFragmentCompatible(fallbackLotName, preferredLotName);
        }

        return true;
    }

    private static string NormalizeLotNoForWeakMerge(BidOpsOutcomeSupplierExtract extract)
    {
        var lotNo = NormalizeCode(extract.LotNo);
        if (!string.IsNullOrWhiteSpace(lotNo))
            return lotNo;

        return TrySplitLotNoPrefixFromLotName(extract.LotName, out var embeddedLotNo, out _)
            ? NormalizeCode(embeddedLotNo)
            : string.Empty;
    }

    private static string NormalizeLotNameForWeakMerge(BidOpsOutcomeSupplierExtract extract)
    {
        if (TrySplitLotNoPrefixFromLotName(extract.LotName, out _, out var cleanedLotName))
            return NormalizeEvidenceText(cleanedLotName);

        return NormalizeEvidenceText(extract.LotName);
    }

    private static int GetOutcomeExtractSourceTrustScore(BidOpsOutcomeSupplierExtract extract)
    {
        return extract.SourceType switch
        {
            BidOpsOutcomeSupplierExtractSourceTypes.AiOutcomeSuppliers => 100,
            BidOpsOutcomeSupplierExtractSourceTypes.PdfStructuredTable => 90,
            BidOpsOutcomeSupplierExtractSourceTypes.WrappedTextParser => 80,
            BidOpsOutcomeSupplierExtractSourceTypes.AwardEvidenceParser => 76,
            BidOpsOutcomeSupplierExtractSourceTypes.CandidateEvidenceParser => 76,
            BidOpsOutcomeSupplierExtractSourceTypes.LegacyTextParser => 50,
            _ => 0
        };
    }

    private static string PackageOutcomeRankKey(BidOpsOutcomeSupplierExtract extract)
    {
        return string.Join(
            '\u001f',
            NormalizeCode(extract.PackageNo),
            NormalizeOutcomeType(extract.OutcomeType),
            extract.Rank?.ToString() ?? string.Empty);
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> SanitizeOutcomeExtractsForPersistence(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts,
        string sourceText)
    {
        return SanitizeOutcomeExtractsForPersistence(extracts, sourceText, forceAwardedOutcome: false);
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> SanitizeOutcomeExtractsForPersistence(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts,
        string sourceText,
        bool forceAwardedOutcome)
    {
        return extracts
            .Select(x => SanitizeOutcomeExtractForPersistence(x, sourceText, forceAwardedOutcome))
            .Where(IsPersistableOutcomeExtract)
            .ToList();
    }

    private static BidOpsOutcomeSupplierExtract SanitizeOutcomeExtractForPersistence(
        BidOpsOutcomeSupplierExtract extract,
        string sourceText)
    {
        return SanitizeOutcomeExtractForPersistence(extract, sourceText, forceAwardedOutcome: false);
    }

    private static BidOpsOutcomeSupplierExtract SanitizeOutcomeExtractForPersistence(
        BidOpsOutcomeSupplierExtract extract,
        string sourceText,
        bool forceAwardedOutcome)
    {
        var isNonAwardOutcome = BidOpsOutcomeRecordPolicy.IsNonAwardOutcome(
            extract.SupplierName,
            extract.OutcomeType,
            extract.EvidenceText);
        var outcomeType = forceAwardedOutcome && !isNonAwardOutcome
            ? BidOpsOutcomeTypes.Awarded
            : extract.OutcomeType;
        var rawLotNo = FirstMeaningful(extract.RawLotNo, extract.LotNo);
        var lotNoValidation = ValidateExplicitLotNo(extract.LotNo, extract.EvidenceText, sourceText);
        var selectedLotNoValidation = lotNoValidation;
        var lotNo = lotNoValidation.Accepted ? extract.LotNo : string.Empty;
        var evidenceLotNo = ExtractLeadingLotNoFromOutcomeEvidence(extract.EvidenceText);
        var evidenceLotNoValidation = ValidateExplicitLotNo(evidenceLotNo, extract.EvidenceText, sourceText);
        if (!string.IsNullOrWhiteSpace(evidenceLotNo) &&
            evidenceLotNoValidation.Accepted &&
            (string.IsNullOrWhiteSpace(lotNo) ||
             ShouldPreferEvidenceLotNo(lotNo, evidenceLotNo, extract)))
        {
            lotNo = evidenceLotNo;
            selectedLotNoValidation = evidenceLotNoValidation;
        }

        var lotName = extract.LotName;
        if (TrySplitLotNoPrefixFromLotName(lotName, out var embeddedLotNo, out var cleanedLotName))
        {
            var embeddedLotNoValidation = ValidateExplicitLotNo(embeddedLotNo, extract.EvidenceText, sourceText);
            if (!embeddedLotNoValidation.Accepted && ContainsLotNoToken(sourceText, NormalizeCode(embeddedLotNo)))
            {
                embeddedLotNoValidation = new OutcomeLotNoValidationResult(
                    true,
                    BidOpsLotNoValidationStatuses.Accepted,
                    "source-lot-no-token");
            }

            var embeddedLotNoSupported = embeddedLotNoValidation.Accepted;
            if (embeddedLotNoSupported &&
                (string.IsNullOrWhiteSpace(lotNo) ||
                 ShouldPreferEmbeddedLotNoFromLotName(lotNo, embeddedLotNo, cleanedLotName, extract)))
            {
                lotNo = embeddedLotNo;
                selectedLotNoValidation = embeddedLotNoValidation;
            }

            if (!string.IsNullOrWhiteSpace(lotNo) &&
                string.Equals(NormalizeCode(lotNo), NormalizeCode(embeddedLotNo), StringComparison.OrdinalIgnoreCase))
            {
                lotName = cleanedLotName;
            }
        }

        if (string.IsNullOrWhiteSpace(lotName) && !string.IsNullOrWhiteSpace(lotNo))
        {
            var evidenceLotName = ExtractLotNameFromOutcomeEvidence(extract.EvidenceText, lotNo);
            if (!string.IsNullOrWhiteSpace(evidenceLotName))
                lotName = evidenceLotName;
        }

        var projectName = string.IsNullOrWhiteSpace(extract.ProjectName) ||
            IsSupportedOutcomeProjectName(extract, sourceText)
            ? extract.ProjectName
            : string.Empty;
        var packageName = extract.PackageName;
        if (string.IsNullOrWhiteSpace(projectName) &&
            IsProjectNameColumnValue(packageName, extract.EvidenceText, sourceText) &&
            !IsSameNormalizedValue(packageName, extract.SupplierName) &&
            !IsSameNormalizedValue(packageName, lotName))
        {
            projectName = packageName;
            packageName = string.Empty;
        }
        else if (IsSameNormalizedValue(packageName, projectName))
        {
            packageName = string.Empty;
        }

        extract.RawLotNo = rawLotNo;
        extract.LotNoValidationStatus = selectedLotNoValidation.Status;
        extract.LotNoValidationReason = selectedLotNoValidation.Reason;

        if (!isNonAwardOutcome &&
            string.Equals(outcomeType, extract.OutcomeType, StringComparison.Ordinal) &&
            string.Equals(lotNo, extract.LotNo, StringComparison.Ordinal) &&
            string.Equals(lotName, extract.LotName, StringComparison.Ordinal) &&
            string.Equals(projectName, extract.ProjectName, StringComparison.Ordinal) &&
            string.Equals(packageName, extract.PackageName, StringComparison.Ordinal))
        {
            return ScoreOutcomeExtractForPersistence(extract);
        }

        return ScoreOutcomeExtractForPersistence(new BidOpsOutcomeSupplierExtract
        {
            SourceSequenceNo = extract.SourceSequenceNo,
            SourcePageNo = extract.SourcePageNo,
            SourceTableTitle = extract.SourceTableTitle,
            SourceRowText = extract.SourceRowText,
            SupplierName = extract.SupplierName,
            OutcomeType = isNonAwardOutcome ? BidOpsOutcomeTypes.Failed : outcomeType,
            Rank = extract.Rank,
            AwardAmount = isNonAwardOutcome ? null : extract.AwardAmount,
            ProcurementAgencyServiceFeeAmount = isNonAwardOutcome ? null : extract.ProcurementAgencyServiceFeeAmount,
            ExtractionOrder = extract.ExtractionOrder,
            SourceType = extract.SourceType,
            SourceParserVersion = extract.SourceParserVersion,
            SourceCallId = extract.SourceCallId,
            ProjectName = projectName,
            ProjectCode = extract.ProjectCode,
            BuyerName = extract.BuyerName,
            LotNo = lotNo,
            RawLotNo = rawLotNo,
            LotNoValidationStatus = selectedLotNoValidation.Status,
            LotNoValidationReason = selectedLotNoValidation.Reason,
            RawLotName = extract.RawLotName,
            LotName = lotName,
            RawPackageNo = extract.RawPackageNo,
            PackageNo = extract.PackageNo,
            PackageName = packageName,
            Category = extract.Category,
            EvidenceText = extract.EvidenceText,
            FieldEvidence = new Dictionary<string, string>(extract.FieldEvidence, StringComparer.OrdinalIgnoreCase),
            Warnings = [.. extract.Warnings],
            Confidence = string.Equals(lotNo, extract.LotNo, StringComparison.Ordinal)
                ? extract.Confidence
                : Math.Min(extract.Confidence, 0.78m)
        });
    }

    private static BidOpsOutcomeSupplierExtract ScoreOutcomeExtractForPersistence(BidOpsOutcomeSupplierExtract extract)
    {
        extract.CompletenessScore = ComputeCompletenessScore(extract);
        extract.EvidenceScore = ComputeEvidenceScore(extract);
        extract.SourceTrustScore = GetOutcomeExtractSourceTrustScore(extract);
        extract.StrengthScore = Math.Clamp(
            (extract.CompletenessScore * 45) +
            (extract.EvidenceScore * 30) +
            (extract.SourceTrustScore * 25),
            0,
            10000) / 100;
        extract.StrengthClass = ResolveOutcomeExtractStrengthClass(extract);
        return extract;
    }

    private static int ComputeCompletenessScore(BidOpsOutcomeSupplierExtract extract)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(BidOpsTextQuality.CleanExtractedValue(extract.SupplierName)))
            score += 25;
        if (!string.IsNullOrWhiteSpace(NormalizeOutcomeType(extract.OutcomeType)))
            score += 10;
        if (!string.IsNullOrWhiteSpace(NormalizeCode(extract.PackageNo)) ||
            !string.IsNullOrWhiteSpace(NormalizeEvidenceText(extract.PackageName)))
        {
            score += 15;
        }
        if (!string.IsNullOrWhiteSpace(NormalizeCode(extract.LotNo)))
            score += 20;
        if (!string.IsNullOrWhiteSpace(NormalizeEvidenceText(extract.LotName)))
            score += 10;
        if (!string.IsNullOrWhiteSpace(NormalizeEvidenceText(extract.EvidenceText)))
            score += 10;
        if (extract.AwardAmount.HasValue || extract.ProcurementAgencyServiceFeeAmount.HasValue || extract.Rank.HasValue)
            score += 5;
        if (!string.IsNullOrWhiteSpace(NormalizeEvidenceText(extract.ProjectCode)) ||
            !string.IsNullOrWhiteSpace(NormalizeEvidenceText(extract.ProjectName)) ||
            !string.IsNullOrWhiteSpace(NormalizeEvidenceText(extract.BuyerName)))
        {
            score += 5;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static int ComputeEvidenceScore(BidOpsOutcomeSupplierExtract extract)
    {
        var evidenceText = FirstMeaningful(extract.EvidenceText, extract.SourceRowText);
        var evidence = NormalizeEvidenceText(evidenceText);
        if (string.IsNullOrWhiteSpace(evidence))
            return extract.FieldEvidence.Count > 0 ? 20 : 0;

        var score = evidence.Length is >= 12 and <= 2000 ? 15 : 5;
        if (!string.IsNullOrWhiteSpace(extract.SourceRowText))
            score += 5;
        if (!string.IsNullOrWhiteSpace(extract.SourceSequenceNo) || extract.SourcePageNo.HasValue)
            score += 5;

        var supplier = NormalizeEvidenceText(extract.SupplierName);
        if (!string.IsNullOrWhiteSpace(supplier) &&
            (evidence.Contains(supplier, StringComparison.OrdinalIgnoreCase) ||
             FieldEvidenceContains(extract, "supplierName", supplier)))
        {
            score += 30;
        }

        var packageNo = NormalizeCode(extract.PackageNo);
        if (!string.IsNullOrWhiteSpace(packageNo) &&
            (ContainsPackageToken(evidenceText, packageNo) ||
             FieldEvidenceContainsCode(extract, "packageNo", packageNo)))
        {
            score += 20;
        }
        else if (LooksLikePackageEvidence(evidenceText))
        {
            score += 12;
        }

        var lotNo = NormalizeCode(extract.LotNo);
        if (!string.IsNullOrWhiteSpace(lotNo) &&
            (ContainsLotNoToken(evidenceText, lotNo) ||
             FieldEvidenceContainsCode(extract, "lotNo", lotNo)))
        {
            score += 25;
        }
        else if (string.Equals(extract.LotNoValidationStatus, BidOpsLotNoValidationStatuses.Accepted, StringComparison.Ordinal))
        {
            score += 15;
        }

        var lotName = NormalizeEvidenceText(extract.LotName);
        if (!string.IsNullOrWhiteSpace(lotName) &&
            (evidence.Contains(lotName, StringComparison.OrdinalIgnoreCase) ||
             FieldEvidenceContains(extract, "lotName", lotName)))
        {
            score += 10;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static bool FieldEvidenceContains(
        BidOpsOutcomeSupplierExtract extract,
        string fieldName,
        string normalizedNeedle)
    {
        if (!extract.FieldEvidence.TryGetValue(fieldName, out var value))
            return false;

        var evidence = NormalizeEvidenceText(value);
        return !string.IsNullOrWhiteSpace(evidence) &&
               evidence.Contains(normalizedNeedle, StringComparison.OrdinalIgnoreCase);
    }

    private static bool FieldEvidenceContainsCode(
        BidOpsOutcomeSupplierExtract extract,
        string fieldName,
        string normalizedNeedle)
    {
        if (!extract.FieldEvidence.TryGetValue(fieldName, out var value))
            return false;

        return string.Equals(NormalizeCode(value), normalizedNeedle, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveOutcomeExtractStrengthClass(BidOpsOutcomeSupplierExtract extract)
    {
        if (!IsPersistableOutcomeExtract(extract) ||
            extract.SourceTrustScore <= 0 ||
            extract.StrengthScore < 45)
        {
            return BidOpsOutcomeSupplierStrengthClasses.Unsupported;
        }

        return extract.StrengthScore >= 75 &&
               HasPackageIdentity(extract) &&
               (HasMeaningfulLotContext(extract) || extract.EvidenceScore >= 70)
            ? BidOpsOutcomeSupplierStrengthClasses.Strong
            : BidOpsOutcomeSupplierStrengthClasses.Weak;
    }

    private static string ExtractLeadingLotNoFromOutcomeEvidence(string? evidenceText)
    {
        foreach (var line in SplitSourceLines(evidenceText))
        {
            var pipeDelimitedLotNo = ExtractPipeDelimitedLotNo(line);
            if (!string.IsNullOrWhiteSpace(pipeDelimitedLotNo))
                return pipeDelimitedLotNo;

            var wrappedMatch = Regex.Match(
                line,
                @"^\s*(?:\d+(?:[.、]|\s+)\s*)?(?<prefix>[A-Za-z0-9]{8,})\s+(?<suffix>[A-Za-z0-9]{1,}[-_/][A-Za-z0-9]{2,}(?:[-_/][A-Za-z0-9]{2,})+)(?:\s+\S+){0,8}?\s+(?:包\s*[A-Za-z0-9一二三四五六七八九十]+|第?\s*[A-Za-z0-9一二三四五六七八九十]+\s*包)",
                RegexOptions.CultureInvariant);
            if (wrappedMatch.Success)
                return BidOpsTextQuality.CleanExtractedValue(
                    wrappedMatch.Groups["prefix"].Value + wrappedMatch.Groups["suffix"].Value);

            var match = Regex.Match(
                line,
                @"^\s*(?:\d+(?:[.、]|\s+)\s*)?(?<value>(?:[A-Za-z0-9]{8,}(?:[-_/][A-Za-z0-9]{2,})+[-_/][A-Za-z0-9]{1,}|[A-Za-z0-9]{3,}(?:[-_/][A-Za-z0-9]{2,}){2,}|[A-Za-z0-9]{8,}[-_/][A-Za-z0-9]{2,}|[A-Za-z0-9]{10,}))(?:\s+\S+){0,8}?\s+(?:包\s*[A-Za-z0-9一二三四五六七八九十]+|第?\s*[A-Za-z0-9一二三四五六七八九十]+\s*包)",
                RegexOptions.CultureInvariant);
            if (match.Success)
                return BidOpsTextQuality.CleanExtractedValue(match.Groups["value"].Value);
        }

        return string.Empty;
    }

    private static bool ShouldPreferEvidenceLotNo(
        string currentLotNo,
        string evidenceLotNo,
        BidOpsOutcomeSupplierExtract extract)
    {
        if (string.Equals(NormalizeCode(currentLotNo), NormalizeCode(evidenceLotNo), StringComparison.OrdinalIgnoreCase))
            return false;

        if (!LooksLikeStructuredOutcomeLotNo(evidenceLotNo) ||
            string.IsNullOrWhiteSpace(extract.EvidenceText) ||
            !LooksLikePackageEvidence(extract.EvidenceText))
        {
            return false;
        }

        var supplier = NormalizeEvidenceText(extract.SupplierName);
        var evidence = NormalizeEvidenceText(extract.EvidenceText);
        return !string.IsNullOrWhiteSpace(supplier) &&
               evidence.Contains(supplier, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePackageEvidence(string? value)
    {
        return Regex.IsMatch(
            value ?? string.Empty,
            @"(?:包\s*[A-Za-z0-9一二三四五六七八九十]+|第?\s*[A-Za-z0-9一二三四五六七八九十]+\s*包)",
            RegexOptions.CultureInvariant);
    }

    private static bool TrySplitLotNoPrefixFromLotName(string? value, out string lotNo, out string lotName)
    {
        lotNo = string.Empty;
        lotName = string.Empty;
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(cleaned))
            return false;

        var match = Regex.Match(
            cleaned,
            @"^(?<lotNo>[A-Za-z0-9]{6,}(?:[-_/][A-Za-z0-9]{1,}){1,})(?<lotName>[\u4e00-\u9fa5].+)$",
            RegexOptions.CultureInvariant);
        if (!match.Success)
            return false;

        var candidateLotNo = match.Groups["lotNo"].Value;
        if (!LooksLikeStructuredOutcomeLotNo(candidateLotNo))
            return false;

        var candidateLotName = BidOpsTextQuality.CleanExtractedValue(match.Groups["lotName"].Value);
        if (string.IsNullOrWhiteSpace(candidateLotName))
            return false;

        lotNo = candidateLotNo;
        lotName = candidateLotName;
        return true;
    }

    private static bool ShouldPreferEmbeddedLotNoFromLotName(
        string currentLotNo,
        string embeddedLotNo,
        string cleanedLotName,
        BidOpsOutcomeSupplierExtract extract)
    {
        var current = NormalizeCode(currentLotNo);
        var embedded = NormalizeCode(embeddedLotNo);
        if (string.IsNullOrWhiteSpace(embedded) ||
            string.Equals(current, embedded, StringComparison.OrdinalIgnoreCase) ||
            !LooksLikeStructuredOutcomeLotNo(embeddedLotNo) ||
            !LooksLikePackageEvidence(extract.EvidenceText))
        {
            return false;
        }

        var evidence = NormalizeEvidenceText(extract.EvidenceText);
        if (!evidence.Contains(embedded, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(current) ||
            !evidence.Contains(current, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var lotName = NormalizeEvidenceText(cleanedLotName);
        return !string.IsNullOrWhiteSpace(lotName) &&
               evidence.Contains(embedded + lotName, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProjectCodeForPersistence(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        cleaned = Regex.Replace(
            cleaned,
            @"^(?:code|项目编号|项目编码|采购编号|招标编号|批次编号|采购项目编号|招标项目编号)\s*[:：=]\s*",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var match = Regex.Match(cleaned, @"[A-Za-z0-9][A-Za-z0-9_.\-/]*", RegexOptions.CultureInvariant);
        return match.Success
            ? match.Value
            : cleaned.Trim(' ', '\t', '。', '.', '；', ';', '，', ',', '、', '）', ')');
    }

    private static bool IsExplicitLotNoSupported(string? lotNo, string? evidenceText, string? sourceText)
    {
        return ValidateExplicitLotNo(lotNo, evidenceText, sourceText).Accepted;
    }

    private static OutcomeLotNoValidationResult ValidateExplicitLotNo(string? lotNo, string? evidenceText, string? sourceText)
    {
        var normalizedLotNo = NormalizeCode(lotNo);
        if (string.IsNullOrWhiteSpace(normalizedLotNo))
        {
            return new OutcomeLotNoValidationResult(
                true,
                BidOpsLotNoValidationStatuses.Empty,
                "lot-no-empty");
        }

        // 国网 HTML 表格证据会被整理成 “分标编号 | 分标名称 | 包号 | ...”，这类行本身就是可信来源。
        if (ContainsLabeledLotNo(evidenceText, normalizedLotNo))
        {
            return new OutcomeLotNoValidationResult(
                true,
                BidOpsLotNoValidationStatuses.Accepted,
                "evidence-labeled-lot-no");
        }

        if (ContainsPipeDelimitedLotNoEvidence(evidenceText, normalizedLotNo))
        {
            return new OutcomeLotNoValidationResult(
                true,
                BidOpsLotNoValidationStatuses.Accepted,
                "evidence-pipe-delimited-lot-no");
        }

        if (ContainsInlineOutcomeLotNoEvidence(evidenceText, normalizedLotNo))
        {
            return new OutcomeLotNoValidationResult(
                true,
                BidOpsLotNoValidationStatuses.Accepted,
                "evidence-inline-outcome-lot-no");
        }

        if (ContainsLabeledLotNo(sourceText, normalizedLotNo))
        {
            return new OutcomeLotNoValidationResult(
                true,
                BidOpsLotNoValidationStatuses.Accepted,
                "source-labeled-lot-no");
        }

        if (ContainsLotNoInLabeledTable(sourceText, normalizedLotNo))
        {
            return new OutcomeLotNoValidationResult(
                true,
                BidOpsLotNoValidationStatuses.Accepted,
                "source-labeled-table-lot-no");
        }

        return new OutcomeLotNoValidationResult(
            false,
            BidOpsLotNoValidationStatuses.Rejected,
            "no-explicit-lot-context");
    }

    private static bool ContainsInlineOutcomeLotNoEvidence(string? text, string normalizedLotNo)
    {
        foreach (var line in SplitSourceLines(text))
        {
            var lotNo = ExtractLeadingLotNoFromOutcomeEvidence(line);
            if (string.Equals(NormalizeCode(lotNo), normalizedLotNo, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool ContainsPipeDelimitedLotNoEvidence(string? text, string normalizedLotNo)
    {
        foreach (var line in SplitSourceLines(text))
        {
            var lotNo = ExtractPipeDelimitedLotNo(line);
            if (string.Equals(NormalizeCode(lotNo), normalizedLotNo, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool ContainsLotNoToken(string? text, string normalizedLotNo)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(normalizedLotNo))
            return false;

        foreach (Match match in Regex.Matches(
                     text,
                     @"[A-Za-z0-9]{6,}(?:[-_/][A-Za-z0-9]{1,})+",
                     RegexOptions.CultureInvariant))
        {
            if (string.Equals(NormalizeCode(match.Value), normalizedLotNo, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ContainsPackageToken(string? text, string normalizedPackageNo)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(normalizedPackageNo))
            return false;

        foreach (Match match in Regex.Matches(
                     text,
                     @"(?:包\s*[A-Za-z0-9一二三四五六七八九十]+|第?\s*[A-Za-z0-9一二三四五六七八九十]+\s*包)",
                     RegexOptions.CultureInvariant))
        {
            if (string.Equals(NormalizeCode(match.Value), normalizedPackageNo, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ExtractLotNameFromOutcomeEvidence(string? evidenceText, string lotNo)
    {
        var normalizedLotNo = NormalizeCode(lotNo);
        if (string.IsNullOrWhiteSpace(normalizedLotNo))
            return string.Empty;

        foreach (var line in SplitSourceLines(evidenceText))
        {
            var wrappedMatch = Regex.Match(
                line,
                @"^\s*(?:\d{1,4}(?:[.、]|\s+)\s*)?(?<prefix>[A-Za-z0-9]{8,})\s+(?<suffix>[A-Za-z0-9]{1,}[-_/][A-Za-z0-9]{2,}(?:[-_/][A-Za-z0-9]{2,})+)\s+(?<lotName>.+?)\s+(?:包\s*[A-Za-z0-9一二三四五六七八九十]+|第?\s*[A-Za-z0-9一二三四五六七八九十]+\s*包)(?:\s+|$)",
                RegexOptions.CultureInvariant);
            if (wrappedMatch.Success &&
                string.Equals(
                    NormalizeCode(wrappedMatch.Groups["prefix"].Value + wrappedMatch.Groups["suffix"].Value),
                    normalizedLotNo,
                    StringComparison.OrdinalIgnoreCase))
            {
                var wrappedLotName = BidOpsTextQuality.CleanExtractedValue(wrappedMatch.Groups["lotName"].Value)
                    .Trim(' ', ':', '：', '=', '-', '、', ',', '，', '.', '。', '；', ';');
                if (wrappedLotName.Length is >= 2 and <= 300 &&
                    !ContainsAny(wrappedLotName, "重要事项说明", "网上供应商服务大厅", "服务费通知书", "成交通知书"))
                {
                    return wrappedLotName;
                }
            }

            var match = Regex.Match(
                line,
                @"^\s*(?:\d{1,4}(?:[.、]|\s+)\s*)?(?<lotNo>(?:[A-Za-z0-9]{3,}(?:[-_/][A-Za-z0-9]{1,})+|[A-Za-z0-9]{10,}))\s+(?<lotName>.+?)\s+(?:包\s*[A-Za-z0-9一二三四五六七八九十]+|第?\s*[A-Za-z0-9一二三四五六七八九十]+\s*包)(?:\s+|$)",
                RegexOptions.CultureInvariant);
            if (!match.Success ||
                !string.Equals(NormalizeCode(match.Groups["lotNo"].Value), normalizedLotNo, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lotName = BidOpsTextQuality.CleanExtractedValue(match.Groups["lotName"].Value)
                .Trim(' ', ':', '：', '=', '-', '、', ',', '，', '.', '。', '；', ';');
            if (lotName.Length is < 2 or > 300)
                continue;

            if (ContainsAny(lotName, "重要事项说明", "网上供应商服务大厅", "服务费通知书", "成交通知书"))
                continue;

            return lotName;
        }

        return string.Empty;
    }

    private static string ExtractPipeDelimitedLotNo(string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.Contains('|', StringComparison.Ordinal))
            return string.Empty;

        var cells = line
            .Split('|', StringSplitOptions.TrimEntries)
            .Select(BidOpsTextQuality.CleanExtractedValue)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        if (cells.Length < 3)
            return string.Empty;

        var normalizedFirst = NormalizeCode(cells[0]);
        if (string.IsNullOrWhiteSpace(normalizedFirst) ||
            !LooksLikeStructuredOutcomeLotNo(normalizedFirst))
        {
            return string.Empty;
        }

        return cells.Skip(1).Take(4).Any(LooksLikePackageNo)
            ? cells[0]
            : string.Empty;
    }

    private static bool LooksLikeStructuredOutcomeLotNo(string? value)
    {
        var normalized = NormalizeCode(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return Regex.IsMatch(
            normalized,
            @"^(?:[A-Z0-9]{8,}(?:[-_/][A-Z0-9]{2,})+[-_/][A-Z0-9]{1,}|[A-Z0-9]{3,}(?:[-_/][A-Z0-9]{2,}){2,}|[A-Z0-9]{8,}[-_/][A-Z0-9]{2,}|[A-Z0-9]{10,})$",
            RegexOptions.CultureInvariant);
    }

    private static bool LooksLikePackageNo(string value)
    {
        var normalized = BidOpsTextQuality.CleanExtractedValue(value);
        return Regex.IsMatch(
            normalized,
            @"^(?:包\s*[A-Za-z0-9一二三四五六七八九十]+|第?\s*[A-Za-z0-9一二三四五六七八九十]+\s*包)$",
            RegexOptions.CultureInvariant);
    }

    private static bool ContainsLabeledLotNo(string? text, string normalizedLotNo)
    {
        foreach (var line in SplitSourceLines(text))
        {
            if (ContainsLotNoLabel(line) && NormalizeCode(line).Contains(normalizedLotNo, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool ContainsLotNoInLabeledTable(string? text, string normalizedLotNo)
    {
        var tableWindow = 0;
        foreach (var line in SplitSourceLines(text))
        {
            if (LooksLikeLotNoTableHeader(line))
                tableWindow = 80;

            if (tableWindow > 0 && NormalizeCode(line).Contains(normalizedLotNo, StringComparison.Ordinal))
                return true;

            if (tableWindow > 0)
                tableWindow--;
        }

        return false;
    }

    private static bool LooksLikeLotNoTableHeader(string line)
    {
        if (!ContainsLotNoLabel(line))
            return false;

        return ContainsAny(line, "包号", "包件", "分包", "标包", "中标", "成交", "供应商", "投标人", "分标名称", "标段名称", "项目单位");
    }

    private static bool ContainsLotNoLabel(string value)
    {
        return ContainsAny(value, "分标编号", "标段编号", "分标号", "标段号");
    }

    private static bool IsSupportedOutcomeProjectName(BidOpsOutcomeSupplierExtract extract, string? sourceText)
    {
        var projectName = extract.ProjectName;
        var normalizedProjectName = NormalizeEvidenceText(projectName);
        if (string.IsNullOrWhiteSpace(normalizedProjectName))
            return true;

        if (IsSameNormalizedValue(projectName, extract.SupplierName) ||
            IsSameNormalizedValue(projectName, extract.LotName) ||
            IsSameNormalizedValue(projectName, extract.PackageName))
        {
            return false;
        }

        return ContainsLabeledProjectName(extract.EvidenceText, normalizedProjectName) ||
               ContainsLabeledProjectName(sourceText, normalizedProjectName) ||
               ContainsProjectNameInLabeledTable(sourceText, normalizedProjectName) ||
               (ContainsProjectNameTableHeader(sourceText) &&
                ContainsProjectNameEvidenceValue(extract.EvidenceText, normalizedProjectName));
    }

    private static bool IsProjectNameColumnValue(string? value, string? evidenceText, string? sourceText)
    {
        var normalizedValue = NormalizeEvidenceText(value);
        if (string.IsNullOrWhiteSpace(normalizedValue))
            return false;

        if (!ContainsProjectNameEvidenceValue(evidenceText, normalizedValue))
            return false;

        return ContainsLabeledProjectName(sourceText, normalizedValue) ||
               ContainsProjectNameInLabeledTable(sourceText, normalizedValue);
    }

    private static bool ContainsProjectNameEvidenceValue(string? evidenceText, string normalizedProjectName)
    {
        if (string.IsNullOrWhiteSpace(evidenceText))
            return false;

        var normalizedEvidence = NormalizeEvidenceText(evidenceText);
        if (!normalizedEvidence.Contains(normalizedProjectName, StringComparison.Ordinal))
            return false;

        return !ContainsAny(normalizedProjectName, "公告", "公示");
    }

    private static bool IsSameNormalizedValue(string? left, string? right)
    {
        var normalizedLeft = NormalizeEvidenceText(left);
        var normalizedRight = NormalizeEvidenceText(right);
        return !string.IsNullOrWhiteSpace(normalizedLeft) &&
               string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal);
    }

    private static bool ContainsLabeledProjectName(string? text, string normalizedProjectName)
    {
        foreach (var line in SplitSourceLines(text))
        {
            if (ContainsProjectNameLabel(line) && NormalizeEvidenceText(line).Contains(normalizedProjectName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool ContainsProjectNameInLabeledTable(string? text, string normalizedProjectName)
    {
        var tableWindow = 0;
        foreach (var line in SplitSourceLines(text))
        {
            if (LooksLikeProjectNameTableHeader(line))
                tableWindow = 80;

            if (tableWindow > 0 && NormalizeEvidenceText(line).Contains(normalizedProjectName, StringComparison.Ordinal))
                return true;

            if (tableWindow > 0)
                tableWindow--;
        }

        return false;
    }

    private static bool ContainsProjectNameTableHeader(string? text)
    {
        return SplitSourceLines(text).Any(LooksLikeProjectNameTableHeader);
    }

    private static bool LooksLikeProjectNameTableHeader(string line)
    {
        if (!ContainsProjectNameLabel(line))
            return false;

        return ContainsAny(line, "包号", "包件", "分包", "标包", "中标", "成交", "供应商", "投标人", "分标名称", "标段名称", "项目单位");
    }

    private static bool ContainsProjectNameLabel(string value)
    {
        return ContainsAny(value, "项目名称", "工程名称", "采购项目名称", "招标项目名称", "子项目名称");
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SplitSourceLines(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        foreach (var raw in Regex.Split(value, @"\r?\n|</tr>|<tr\b|</p>|<p\b|</td>|<td\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var line = raw.Trim();
            if (!string.IsNullOrWhiteSpace(line))
                yield return line;
        }
    }

    private static string NormalizeEvidenceText(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        return new string(cleaned
                .Where(x => !char.IsWhiteSpace(x) && !"()（）[]【】{}<>《》,，.。;；:：-_—–/\\|".Contains(x))
                .ToArray())
            .ToUpperInvariant();
    }

    private static bool IsPersistableOutcomeExtract(BidOpsOutcomeSupplierExtract extract)
    {
        var supplierName = BidOpsTextQuality.CleanExtractedValue(extract.SupplierName);
        if (string.IsNullOrWhiteSpace(supplierName))
            return false;

        if (LooksLikePollutedOutcomeEvidence(extract.EvidenceText))
            return false;

        if (HasUnbalancedLeadingBracket(supplierName))
            return false;

        if (BidOpsOutcomeRecordPolicy.IsNonAwardOutcome(
                supplierName,
                extract.OutcomeType,
                extract.EvidenceText))
        {
            // 流标/废标行没有供应商实体，但仍要作为结果公告状态行展示。
            return HasPackageIdentity(extract) || !string.IsNullOrWhiteSpace(extract.EvidenceText);
        }

        if (LooksLikeNonAwardEvidence(extract.EvidenceText))
            return false;

        return LooksLikeSupplierOrganizationName(supplierName);
    }

    private static bool HasUnbalancedLeadingBracket(string value)
    {
        var trimmed = BidOpsTextQuality.CleanExtractedValue(value).Trim();
        if (trimmed.Length == 0)
            return false;

        return (trimmed[0] == '（' && !trimmed.Contains('）')) ||
               (trimmed[0] == '(' && !trimmed.Contains(')')) ||
               (trimmed[0] == '【' && !trimmed.Contains('】')) ||
               (trimmed[0] == '[' && !trimmed.Contains(']'));
    }

    private static bool LooksLikeSupplierOrganizationName(string supplierName)
    {
        var compact = new string(BidOpsTextQuality.CleanExtractedValue(supplierName)
            .Where(x => !char.IsWhiteSpace(x) && !"()（）[]【】{}<>《》,，.。;；:：-_—–/\\|".Contains(x))
            .ToArray());
        if (compact.Length < 4)
            return false;

        if (compact.All(x => char.IsDigit(x) || x is '.' or '．'))
            return false;

        if (Regex.IsMatch(compact, @"^[A-Za-z]{1,4}$", RegexOptions.CultureInvariant))
            return false;

        if (Regex.IsMatch(compact, @"^[0-9.]+(?:万元|万|元)?$", RegexOptions.CultureInvariant))
            return false;

        if (ContainsAny(compact, "序号", "分标名称", "包号", "项目名称", "成交金额", "中标金额"))
            return false;

        if (ContainsAny(compact, "招标代理", "采购代理", "代理机构", "联系人", "联系电话", "开户银行", "银行账号", "代理费", "服务费"))
            return false;

        if (Regex.IsMatch(compact, @"^国网.+(?:供电公司|供电分公司|技培中心|建设分公司)$", RegexOptions.CultureInvariant))
            return false;

        return ContainsAny(
            compact,
            "有限责任公司",
            "股份有限公司",
            "集团有限公司",
            "有限公司",
            "分公司",
            "集团",
            "公司",
            "工厂",
            "厂",
            "勘测设计研究院",
            "工程设计有限公司",
            "研究院",
            "设计院",
            "测绘院",
            "勘测院",
            "勘察院",
            "规划院",
            "科学院",
            "检验院",
            "检测院",
            "计量院",
            "研究所",
            // 成交公告中可能出现个体工商户或服务门店，不能只按“公司/院所”判断供应商实体。
            "服务部",
            "经营部",
            "营业部",
            "门市部",
            "商行",
            "工作室",
            "事务所",
            "大学",
            "学院",
            "学校",
            "医院",
            "中心");
    }

    private static bool LooksLikePollutedOutcomeEvidence(string? evidenceText)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(evidenceText);
        if (string.IsNullOrWhiteSpace(cleaned))
            return false;

        if (cleaned.StartsWith('|') ||
            cleaned.Contains("PDF 表格结构", StringComparison.Ordinal) ||
            cleaned.Contains("PDF表格结构", StringComparison.Ordinal))
        {
            return true;
        }

        return cleaned.Length > 300 &&
               ContainsAny(cleaned, "重要事项说明", "网上供应商服务大厅", "服务费通知书", "成交通知书");
    }

    private static bool LooksLikeNonAwardEvidence(string? evidenceText)
    {
        var evidence = BidOpsTextQuality.CleanExtractedValue(evidenceText);
        if (string.IsNullOrWhiteSpace(evidence))
            return false;

        return ContainsAny(
            evidence,
            "本次流标",
            "本包流标",
            "流标 /",
            "流标/",
            "本次废标",
            "本包废标",
            "废标 /",
            "废标/",
            "采购失败",
            "成交失败",
            "中标失败");
    }

    private static bool HasPackageIdentity(BidOpsOutcomeSupplierExtract extract)
    {
        return !string.IsNullOrWhiteSpace(extract.PackageNo) ||
               !string.IsNullOrWhiteSpace(extract.LotNo) ||
               !string.IsNullOrWhiteSpace(extract.LotName) ||
               !string.IsNullOrWhiteSpace(extract.PackageName);
    }

    private static bool HasExplicitLotNo(BidOpsOutcomeSupplierExtract extract)
    {
        return !string.IsNullOrWhiteSpace(NormalizeCode(extract.LotNo));
    }

    private static bool HasMeaningfulLotContext(BidOpsOutcomeSupplierExtract extract)
    {
        if (HasExplicitLotNo(extract))
            return true;

        var lotName = NormalizeEvidenceText(extract.LotName);
        return lotName.Length >= 4 &&
               lotName is not "未分标段" and not "无分标" and not "不分标";
    }

    private static bool IsSupplierNameFragmentOf(string? fragment, string? fullName)
    {
        var normalizedFragment = NormalizeEvidenceText(fragment);
        var normalizedFullName = NormalizeEvidenceText(fullName);
        return normalizedFragment.Length >= 4 &&
               normalizedFullName.Length >= normalizedFragment.Length + 3 &&
               normalizedFullName.Contains(normalizedFragment, StringComparison.Ordinal);
    }

    private static string NormalizeLotIdentity(BidOpsOutcomeSupplierExtract extract)
    {
        var lotNo = NormalizeCode(extract.LotNo);
        var lotName = NormalizeCode(extract.LotName);
        return $"{lotNo}|{lotName}";
    }

    private static bool PackageLotContextCompatible(
        string extractLotNo,
        string extractLotName,
        string packageLotNo,
        string packageLotName)
    {
        var matchedAnyLotContext = false;
        if (!string.IsNullOrWhiteSpace(extractLotNo) && !string.IsNullOrWhiteSpace(packageLotNo))
        {
            if (!string.Equals(extractLotNo, packageLotNo, StringComparison.Ordinal))
                return false;

            matchedAnyLotContext = true;
        }

        if (!string.IsNullOrWhiteSpace(extractLotName) && !string.IsNullOrWhiteSpace(packageLotName))
        {
            if (!string.Equals(extractLotName, packageLotName, StringComparison.Ordinal))
                return false;

            matchedAnyLotContext = true;
        }

        return matchedAnyLotContext;
    }

    private async Task<BidOpsNoticeAiExtractionRequest> BuildAiSourceAsync(RawNotice raw, CancellationToken ct)
    {
        var text = string.IsNullOrWhiteSpace(raw.TextContentStorageKey)
            ? raw.TextPreview
            : await TryReadFileAsync(raw.TextContentStorageKey, raw.Id, ct);
        if (string.IsNullOrWhiteSpace(text))
            text = raw.TextPreview;

        var html = string.IsNullOrWhiteSpace(raw.HtmlSnapshotStorageKey)
            ? string.Empty
            : await TryReadFileAsync(raw.HtmlSnapshotStorageKey, raw.Id, ct);

        var attachmentQuery = await _rawAttachments.QueryAsync(raw.TenantId, ct);
        var attachments = await attachmentQuery
            .Where(x => x.RawNoticeId == raw.Id &&
                        x.TextExtractStatus == TextExtractStatus.Succeeded &&
                        x.TextContentStorageKey != string.Empty)
            .ToListAsync(ct);

        var attachmentInputs = new List<BidOpsAiAttachmentInput>();
        foreach (var attachment in attachments)
        {
            var attachmentText = await TryReadFileAsync(attachment.TextContentStorageKey, raw.Id, ct);
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

    private static string BuildCombinedSourceText(BidOpsNoticeAiExtractionRequest source)
    {
        var builder = new StringBuilder();
        AppendSourceSection(builder, "Announcement Text", source.Text);
        AppendSourceSection(builder, "Announcement HTML", source.Html);
        foreach (var attachment in source.Attachments)
        {
            AppendSourceSection(builder, $"Attachment: {attachment.FileName}", attachment.Text);
        }

        return builder.ToString();
    }

    private static BidOpsNoticeAiExtractionRequest RemovePdfTableMarkdownSections(
        BidOpsNoticeAiExtractionRequest source)
    {
        return source with
        {
            Text = RemovePdfTableMarkdownSections(source.Text),
            Html = RemovePdfTableMarkdownSections(source.Html),
            Attachments = source.Attachments
                .Select(x => x with { Text = RemovePdfTableMarkdownSections(x.Text) })
                .ToList()
        };
    }

    private static string RemovePdfTableMarkdownSections(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.Contains("PDF 表格结构", StringComparison.Ordinal))
        {
            return value ?? string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var skippingPdfTable = false;
        foreach (var rawLine in Regex.Split(
                     value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'),
                     "\n",
                     RegexOptions.CultureInvariant))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("PDF 表格结构", StringComparison.Ordinal))
            {
                skippingPdfTable = true;
                continue;
            }

            if (skippingPdfTable)
            {
                if (line.Length == 0 || line.StartsWith('|'))
                    continue;

                skippingPdfTable = false;
            }

            builder.AppendLine(rawLine);
        }

        return builder.ToString().Trim();
    }

    private static void AppendSourceSection(StringBuilder builder, string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        builder.AppendLine();
        builder.AppendLine($"## {title}");
        builder.AppendLine(content.Trim());
    }

    private async Task<string> TryReadFileAsync(string storageKey, long rawNoticeId, CancellationToken ct)
    {
        try
        {
            await using var stream = await _fileStore.OpenReadAsync(storageKey, ct);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(ct);
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException or DirectoryNotFoundException)
        {
            _logger.LogWarning(
                ex,
                "BidOps outcome supplier extraction could not read stored text {StorageKey} for raw notice {RawNoticeId}.",
                storageKey,
                rawNoticeId);
            return string.Empty;
        }
    }

    private async Task<NoticeContext> LoadNoticeContextAsync(RawNotice raw, CancellationToken ct)
    {
        var noticeQuery = await _notices.QueryAsync(raw.TenantId, ct);
        var notice = await noticeQuery.Where(x => x.RawNoticeId == raw.Id).FirstOrDefaultAsync(ct);
        if (notice != null)
        {
            var packageQuery = await _packages.QueryAsync(raw.TenantId, ct);
            var packages = await packageQuery
                .Where(x => x.NoticeId == notice.Id)
                .ToListAsync(ct);

            return new NoticeContext(
                notice.Id,
                notice.ProjectName,
                notice.ProjectCode,
                notice.BuyerName,
                notice.Region,
                notice.PublishTime,
                packages.Select(x => PackageSnapshot.FromPackage(x)).ToList());
        }

        var stagingQuery = await _noticeStaging.QueryAsync(raw.TenantId, ct);
        var staging = await stagingQuery.Where(x => x.RawNoticeId == raw.Id).FirstOrDefaultAsync(ct);
        if (staging == null)
            return NoticeContext.Empty;

        var stagingPackageQuery = await _packageStaging.QueryAsync(raw.TenantId, ct);
        var stagingPackages = await stagingPackageQuery
            .Where(x => x.NoticeStagingId == staging.Id)
            .ToListAsync(ct);

        return new NoticeContext(
            null,
            staging.ProjectName,
            staging.ProjectCode,
            staging.BuyerName,
            staging.Region,
            staging.PublishTime,
            stagingPackages.Select(x => PackageSnapshot.FromStaging(x)).ToList());
    }

    private static string NormalizeOutcomeType(string value)
    {
        return value switch
        {
            BidOpsOutcomeTypes.Awarded => BidOpsOutcomeTypes.Awarded,
            BidOpsOutcomeTypes.Shortlisted => BidOpsOutcomeTypes.Shortlisted,
            BidOpsOutcomeTypes.Failed => BidOpsOutcomeTypes.Failed,
            _ => BidOpsOutcomeTypes.Candidate
        };
    }

    private static string ComputeSourceHash(params string[] values)
    {
        var joined = string.Join('\u001f', values.Select(x => x ?? string.Empty));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
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

    private static string FirstPackageNameDistinctFromProject(string? projectName, params string?[] values)
    {
        foreach (var value in values)
        {
            var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
            if (!string.IsNullOrWhiteSpace(cleaned) &&
                !BidOpsTextQuality.IsUnknownMarker(cleaned) &&
                !IsSameNormalizedValue(cleaned, projectName))
            {
                return cleaned;
            }
        }

        return string.Empty;
    }

    private static string Truncate(string? value, int maxLength)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private sealed record OutcomeLotContext(
        string LotNo,
        string LotName,
        string NormalizedLotNo,
        string NormalizedLotName);

    private sealed record OutcomeExtractSelection(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> Extracts,
        int CandidateCount,
        int MergeGroupCount,
        int MergedCandidateCount)
    {
        public static OutcomeExtractSelection Empty { get; } = new(
            Array.Empty<BidOpsOutcomeSupplierExtract>(),
            0,
            0,
            0);
    }

    private sealed record NoticeContext(
        long? NoticeId,
        string ProjectName,
        string ProjectCode,
        string BuyerName,
        string Region,
        DateTime? PublishTime,
        IReadOnlyList<PackageSnapshot> Packages)
    {
        public static NoticeContext Empty { get; } = new(null, string.Empty, string.Empty, string.Empty, string.Empty, null, []);

        public PackageSnapshot FindPackage(BidOpsOutcomeSupplierExtract extract)
        {
            if (Packages.Count == 0)
                return PackageSnapshot.Empty;

            var packageNo = NormalizeCode(extract.PackageNo);
            var lotNo = NormalizeCode(extract.LotNo);
            var lotName = NormalizeCode(extract.LotName);
            var packageName = BidOpsTextQuality.CleanExtractedValue(extract.PackageName);

            if (!string.IsNullOrWhiteSpace(packageNo))
            {
                var packageMatches = Packages
                    .Where(x => NormalizeCode(x.PackageNo) == packageNo)
                    .ToList();
                if (!string.IsNullOrWhiteSpace(lotNo) || !string.IsNullOrWhiteSpace(lotName))
                {
                    var byLotContext = packageMatches
                        .Where(x => PackageLotContextCompatible(lotNo, lotName, NormalizeCode(x.LotNo), NormalizeCode(x.LotName)))
                        .ToList();
                    return byLotContext.Count == 1 ? byLotContext[0] : PackageSnapshot.Empty;
                }

                if (packageMatches.Count == 1)
                    return packageMatches[0];
            }

            if (!string.IsNullOrWhiteSpace(packageName))
            {
                var byPackageName = Packages
                    .Where(x =>
                        x.PackageName.Contains(packageName, StringComparison.OrdinalIgnoreCase) ||
                        packageName.Contains(x.PackageName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (!string.IsNullOrWhiteSpace(lotNo) || !string.IsNullOrWhiteSpace(lotName))
                {
                    var byLotContext = byPackageName
                        .Where(x => PackageLotContextCompatible(lotNo, lotName, NormalizeCode(x.LotNo), NormalizeCode(x.LotName)))
                        .ToList();
                    return byLotContext.Count == 1 ? byLotContext[0] : PackageSnapshot.Empty;
                }

                if (byPackageName.Count == 1)
                    return byPackageName[0];
            }

            return Packages.Count == 1 &&
                   string.IsNullOrWhiteSpace(packageNo) &&
                   string.IsNullOrWhiteSpace(lotNo) &&
                   string.IsNullOrWhiteSpace(lotName)
                ? Packages[0]
                : PackageSnapshot.Empty;
        }
    }

    private sealed record PackageSnapshot(
        long? TenderPackageId,
        string LotNo,
        string LotName,
        string PackageNo,
        string PackageName,
        string Category)
    {
        public static PackageSnapshot Empty { get; } = new(null, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        public static PackageSnapshot FromPackage(TenderPackage package)
        {
            return new PackageSnapshot(
                package.Id,
                package.LotNo,
                package.LotName,
                package.PackageNo,
                package.PackageName,
                package.Category);
        }

        public static PackageSnapshot FromStaging(PackageStaging package)
        {
            return new PackageSnapshot(
                null,
                package.LotNo,
                package.LotName,
                package.PackageNo,
                package.PackageName,
                package.Category);
        }
    }

    private sealed record OutcomeLotNoValidationResult(
        bool Accepted,
        string Status,
        string Reason);

    private static string NormalizeCode(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        return new string(cleaned
            .Where(x => !char.IsWhiteSpace(x) && !":：,，;；".Contains(x))
            .ToArray())
            .ToUpperInvariant();
    }
}
