using System.Security.Cryptography;
using System.Text;
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
    private readonly IRepository<OutcomeSupplierRecord> _records;
    private readonly IBidOpsFileStore _fileStore;
    private readonly IBidOpsOutcomeSupplierAiExtractionService _aiExtraction;
    private readonly IBidOpsOrganizationMasterDataService _organizationMasterData;
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
        IRepository<OutcomeSupplierRecord> records,
        IBidOpsFileStore fileStore,
        IBidOpsOutcomeSupplierAiExtractionService aiExtraction,
        IBidOpsOrganizationMasterDataService organizationMasterData,
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
        _records = records ?? throw new ArgumentNullException(nameof(records));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _aiExtraction = aiExtraction ?? throw new ArgumentNullException(nameof(aiExtraction));
        _organizationMasterData = organizationMasterData ?? throw new ArgumentNullException(nameof(organizationMasterData));
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
        var extracts = isOutcomeNotice
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
            return new OutcomeSupplierExtractionResultDto
            {
                RawNoticeId = raw.Id,
                IsOutcomeNotice = isOutcomeNotice,
                ExtractedCount = 0,
                SavedCount = 0,
                Message = isOutcomeNotice ? "未识别到中标/候选厂家线索。" : "非结果/候选公示，已跳过厂家线索抽取。"
            };
        }

        var context = await LoadNoticeContextAsync(raw, ct);
        var now = DateTime.UtcNow;
        var records = new List<OutcomeSupplierRecord>();

        foreach (var extract in extracts)
        {
            var supplierName = Truncate(BidOpsTextQuality.CleanExtractedValue(extract.SupplierName), 300);
            var supplierNameNormalized = Truncate(BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(supplierName), 191);
            if (string.IsNullOrWhiteSpace(supplierName) || string.IsNullOrWhiteSpace(supplierNameNormalized))
                continue;

            var package = context.FindPackage(extract);
            var sourceHash = ComputeSourceHash(
                raw.Id.ToString(),
                supplierNameNormalized,
                FirstMeaningful(extract.PackageNo, package.PackageNo),
                FirstMeaningful(extract.LotNo, package.LotNo),
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
                ProjectName = Truncate(FirstMeaningful(extract.ProjectName, context.ProjectName, raw.Title), 500),
                ProjectCode = Truncate(FirstMeaningful(extract.ProjectCode, context.ProjectCode), 128),
                BuyerName = Truncate(FirstMeaningful(extract.BuyerName, context.BuyerName), 300),
                Region = Truncate(context.Region, 128),
                PublishTime = context.PublishTime ?? raw.PublishTime,
                LotNo = Truncate(FirstMeaningful(extract.LotNo, package.LotNo), 128),
                LotName = Truncate(FirstMeaningful(extract.LotName, package.LotName), 300),
                PackageNo = Truncate(FirstMeaningful(extract.PackageNo, package.PackageNo), 128),
                PackageName = Truncate(FirstMeaningful(extract.PackageName, package.PackageName, context.ProjectName, raw.Title), 500),
                Category = Truncate(FirstMeaningful(extract.Category, package.Category), 128),
                SupplierName = supplierName,
                SupplierNameNormalized = supplierNameNormalized,
                OutcomeType = NormalizeOutcomeType(extract.OutcomeType),
                Rank = extract.Rank,
                AwardAmount = extract.AwardAmount,
                ProcurementAgencyServiceFeeAmount = extract.ProcurementAgencyServiceFeeAmount,
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

        return new OutcomeSupplierExtractionResultDto
        {
            RawNoticeId = raw.Id,
            IsOutcomeNotice = true,
            ExtractedCount = extracts.Count,
            SavedCount = records.Count,
            Message = records.Count == 0 ? "识别到文本片段，但未形成有效厂家线索。" : $"已保存 {records.Count} 条公开结果厂家线索。"
        };
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

        return ChooseOutcomeExtractsForPersistence(deterministic, aiExtracts, reviewerPrompt);
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> ChooseOutcomeExtractsForPersistence(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> deterministic,
        IReadOnlyList<BidOpsOutcomeSupplierExtract> aiExtracts,
        string? reviewerPrompt)
    {
        return !string.IsNullOrWhiteSpace(reviewerPrompt)
            ? DedupeOutcomeExtracts(aiExtracts)
            : MergeOutcomeExtracts(deterministic, aiExtracts);
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> MergeOutcomeExtracts(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> deterministic,
        IReadOnlyList<BidOpsOutcomeSupplierExtract> aiExtracts)
    {
        if (aiExtracts.Count == 0)
            return deterministic;

        return deterministic
            .Select(x => (Extract: x, IsAi: false))
            .Concat(aiExtracts.Select(x => (Extract: x, IsAi: true)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Extract.SupplierName))
            .GroupBy(x => string.Join(
                '\u001f',
                BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(x.Extract.SupplierName),
                NormalizeCode(x.Extract.LotNo),
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

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> DedupeOutcomeExtracts(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts)
    {
        return extracts
            .Where(x => !string.IsNullOrWhiteSpace(x.SupplierName))
            .GroupBy(x => string.Join(
                '\u001f',
                BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(x.SupplierName),
                NormalizeCode(x.LotNo),
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

    private static bool HasPackageIdentity(BidOpsOutcomeSupplierExtract extract)
    {
        return !string.IsNullOrWhiteSpace(extract.PackageNo) ||
               !string.IsNullOrWhiteSpace(extract.LotNo) ||
               !string.IsNullOrWhiteSpace(extract.PackageName);
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
            var packageName = BidOpsTextQuality.CleanExtractedValue(extract.PackageName);

            var exact = Packages.FirstOrDefault(x =>
                (!string.IsNullOrWhiteSpace(packageNo) && NormalizeCode(x.PackageNo) == packageNo) ||
                (!string.IsNullOrWhiteSpace(lotNo) && NormalizeCode(x.LotNo) == lotNo));
            if (exact != null)
                return exact;

            if (!string.IsNullOrWhiteSpace(packageName))
            {
                var byName = Packages.FirstOrDefault(x =>
                    x.PackageName.Contains(packageName, StringComparison.OrdinalIgnoreCase) ||
                    packageName.Contains(x.PackageName, StringComparison.OrdinalIgnoreCase));
                if (byName != null)
                    return byName;
            }

            return Packages.Count == 1 && string.IsNullOrWhiteSpace(packageNo) && string.IsNullOrWhiteSpace(lotNo)
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
