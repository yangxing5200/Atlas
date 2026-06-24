using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Ai;
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

        var extracts = shouldAttemptExtraction
            ? await BuildOutcomeExtractsAsync(raw, source, sourceText, reviewerPrompt, ct)
            : [];

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
                Message = shouldAttemptExtraction
                    ? (hasReviewerPrompt ? "AI Provider 未返回可保存的中标/候选厂家线索，请查看后台任务的 AI 返回诊断。" : "未识别到中标/候选厂家线索。")
                    : "非结果/候选公示，已跳过厂家线索抽取。"
            };
        }

        var context = await LoadNoticeContextAsync(raw, ct);
        var now = DateTime.UtcNow;
        var records = new List<OutcomeSupplierRecord>();
        var extractionOrder = 0;

        foreach (var rawExtract in extracts)
        {
            var extract = SanitizeOutcomeExtractForPersistence(rawExtract, sourceText);
            var supplierName = Truncate(BidOpsTextQuality.CleanExtractedValue(extract.SupplierName), 300);
            var supplierNameNormalized = Truncate(BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(supplierName), 191);
            if (string.IsNullOrWhiteSpace(supplierName) || string.IsNullOrWhiteSpace(supplierNameNormalized))
                continue;

            var package = context.FindPackage(extract);
            var packageNoForHash = FirstMeaningful(extract.PackageNo, package.PackageNo);
            var lotNoForHash = FirstMeaningful(extract.LotNo, package.LotNo);
            var lotNameForHash = FirstMeaningful(extract.LotName, package.LotName);
            var sourceHash = ComputeSourceHash(
                raw.Id.ToString(),
                supplierNameNormalized,
                packageNoForHash,
                lotNoForHash,
                lotNameForHash,
                extract.OutcomeType,
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
                ProjectCode = Truncate(FirstMeaningful(extract.ProjectCode, context.ProjectCode), 128),
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
                OutcomeType = NormalizeOutcomeType(extract.OutcomeType),
                Rank = extract.Rank,
                AwardAmount = extract.AwardAmount,
                ProcurementAgencyServiceFeeAmount = extract.ProcurementAgencyServiceFeeAmount,
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
            var syncResult = await _organizationMasterData.SyncOutcomeOrganizationsAsync(raw.TenantId, records, ct);
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
                BuyerCreatedCount = syncResult.BuyerCreatedCount,
                BuyerUpdatedCount = syncResult.BuyerUpdatedCount,
                SupplierCreatedCount = syncResult.SupplierCreatedCount,
                SupplierUpdatedCount = syncResult.SupplierUpdatedCount,
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
            Message = records.Count == 0 ? "识别到文本片段，但未形成有效厂家线索。" : $"已保存 {records.Count} 条公开结果厂家线索。"
        };
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

    private async Task<IReadOnlyList<BidOpsOutcomeSupplierExtract>> BuildOutcomeExtractsAsync(
        RawNotice raw,
        BidOpsNoticeAiExtractionRequest source,
        string sourceText,
        string? reviewerPrompt,
        CancellationToken ct)
    {
        var deterministic = BidOpsOutcomeSupplierExtractBuilder.Extract(
            raw.Title,
            raw.NoticeType,
            raw.DetailUrl,
            raw.PublishTime,
            sourceText,
            raw.Id);

        var aiExtracts = await _aiExtraction.ExtractAsync(
            new BidOpsOutcomeSupplierAiExtractionRequest(
                raw.Title,
                raw.NoticeType,
                raw.DetailUrl,
                raw.PublishTime,
                source.Text,
                deterministic,
                reviewerPrompt,
                source.Html,
                source.Attachments),
            ct);

        return ChooseOutcomeExtractsForPersistence(
            SanitizeOutcomeExtractsForPersistence(deterministic, sourceText),
            SanitizeOutcomeExtractsForPersistence(aiExtracts, sourceText),
            reviewerPrompt);
    }

    private static bool ShouldAttemptOutcomeExtraction(bool looksLikeOutcomeNotice, string? reviewerPrompt)
    {
        return looksLikeOutcomeNotice || !string.IsNullOrWhiteSpace(reviewerPrompt);
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> ChooseOutcomeExtractsForPersistence(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> deterministic,
        IReadOnlyList<BidOpsOutcomeSupplierExtract> aiExtracts,
        string? reviewerPrompt)
    {
        var selected = !string.IsNullOrWhiteSpace(reviewerPrompt)
            ? DedupeOutcomeExtracts(aiExtracts)
            : MergeOutcomeExtracts(deterministic, aiExtracts);

        return AssignExtractionOrder(selected);
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> MergeOutcomeExtracts(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> deterministic,
        IReadOnlyList<BidOpsOutcomeSupplierExtract> aiExtracts)
    {
        if (aiExtracts.Count == 0)
            return deterministic;

        return aiExtracts
            .Select(x => (Extract: x, IsAi: true))
            .Concat(deterministic.Select(x => (Extract: x, IsAi: false)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Extract.SupplierName))
            .GroupBy(x => string.Join(
                '\u001f',
                BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(x.Extract.SupplierName),
                NormalizeLotIdentity(x.Extract),
                NormalizeCode(x.Extract.PackageNo),
                NormalizeOutcomeType(x.Extract.OutcomeType),
                x.Extract.Rank?.ToString() ?? string.Empty))
            .Select(x => x
                .OrderByDescending(item => item.IsAi)
                .ThenByDescending(item => HasPackageIdentity(item.Extract))
                .ThenByDescending(item => item.Extract.ProcurementAgencyServiceFeeAmount.HasValue)
                .ThenByDescending(item => item.Extract.AwardAmount.HasValue)
                .ThenByDescending(item => item.Extract.Confidence)
                .First()
                .Extract)
            .ToList();
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
        return extracts
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
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> SanitizeOutcomeExtractsForPersistence(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts,
        string sourceText)
    {
        return extracts
            .Select(x => SanitizeOutcomeExtractForPersistence(x, sourceText))
            .Where(IsPersistableOutcomeExtract)
            .ToList();
    }

    private static BidOpsOutcomeSupplierExtract SanitizeOutcomeExtractForPersistence(
        BidOpsOutcomeSupplierExtract extract,
        string sourceText)
    {
        var lotNo = string.IsNullOrWhiteSpace(extract.LotNo) ||
            IsExplicitLotNoSupported(extract.LotNo, extract.EvidenceText, sourceText)
            ? extract.LotNo
            : string.Empty;
        if (string.IsNullOrWhiteSpace(lotNo))
        {
            var evidenceLotNo = ExtractLeadingLotNoFromOutcomeEvidence(extract.EvidenceText);
            if (IsExplicitLotNoSupported(evidenceLotNo, extract.EvidenceText, sourceText))
                lotNo = evidenceLotNo;
        }

        var projectName = string.IsNullOrWhiteSpace(extract.ProjectName) ||
            IsSupportedOutcomeProjectName(extract, sourceText)
            ? extract.ProjectName
            : string.Empty;
        var packageName = extract.PackageName;
        if (string.IsNullOrWhiteSpace(projectName) &&
            IsProjectNameColumnValue(packageName, extract.EvidenceText, sourceText) &&
            !IsSameNormalizedValue(packageName, extract.SupplierName) &&
            !IsSameNormalizedValue(packageName, extract.LotName))
        {
            projectName = packageName;
            packageName = string.Empty;
        }
        else if (IsSameNormalizedValue(packageName, projectName))
        {
            packageName = string.Empty;
        }

        if (string.Equals(lotNo, extract.LotNo, StringComparison.Ordinal) &&
            string.Equals(projectName, extract.ProjectName, StringComparison.Ordinal) &&
            string.Equals(packageName, extract.PackageName, StringComparison.Ordinal))
        {
            return extract;
        }

        return new BidOpsOutcomeSupplierExtract
        {
            SupplierName = extract.SupplierName,
            OutcomeType = extract.OutcomeType,
            Rank = extract.Rank,
            AwardAmount = extract.AwardAmount,
            ProcurementAgencyServiceFeeAmount = extract.ProcurementAgencyServiceFeeAmount,
            ExtractionOrder = extract.ExtractionOrder,
            ProjectName = projectName,
            ProjectCode = extract.ProjectCode,
            BuyerName = extract.BuyerName,
            LotNo = lotNo,
            LotName = extract.LotName,
            PackageNo = extract.PackageNo,
            PackageName = packageName,
            Category = extract.Category,
            EvidenceText = extract.EvidenceText,
            Confidence = string.Equals(lotNo, extract.LotNo, StringComparison.Ordinal)
                ? extract.Confidence
                : Math.Min(extract.Confidence, 0.78m)
        };
    }

    private static string ExtractLeadingLotNoFromOutcomeEvidence(string? evidenceText)
    {
        foreach (var line in SplitSourceLines(evidenceText))
        {
            var match = Regex.Match(
                line,
                @"^\s*(?:\d+(?:[.、]|\s+)\s*)?(?<value>[A-Za-z0-9]{3,}(?:[-_/][A-Za-z0-9]{2,}){2,})\s+(?:包\s*[A-Za-z0-9一二三四五六七八九十]+|第?\s*[A-Za-z0-9一二三四五六七八九十]+\s*包)",
                RegexOptions.CultureInvariant);
            if (match.Success)
                return BidOpsTextQuality.CleanExtractedValue(match.Groups["value"].Value);
        }

        return string.Empty;
    }

    private static bool IsExplicitLotNoSupported(string? lotNo, string? evidenceText, string? sourceText)
    {
        var normalizedLotNo = NormalizeCode(lotNo);
        if (string.IsNullOrWhiteSpace(normalizedLotNo))
            return true;

        return ContainsLabeledLotNo(evidenceText, normalizedLotNo) ||
               ContainsLabeledLotNo(sourceText, normalizedLotNo) ||
               ContainsLotNoInLabeledTable(sourceText, normalizedLotNo);
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

        if (LooksLikeNonAwardEvidence(extract.EvidenceText))
            return false;

        return LooksLikeSupplierOrganizationName(supplierName);
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
            "事务所",
            "大学",
            "学院",
            "学校",
            "医院",
            "中心");
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
