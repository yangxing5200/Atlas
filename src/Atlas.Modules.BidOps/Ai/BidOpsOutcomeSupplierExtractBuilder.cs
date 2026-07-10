using System.Globalization;
using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Services;

namespace Atlas.Modules.BidOps.Ai;

public static class BidOpsOutcomeSupplierExtractBuilder
{
    public static IReadOnlyList<BidOpsOutcomeSupplierExtract> Extract(
        string title,
        string noticeType,
        string sourceUrl,
        DateTime? publishTime,
        string text,
        long? rawNoticeId = null)
    {
        var sourceText = text ?? string.Empty;
        var noticeKind = DetermineNoticeKind(title, noticeType, sourceUrl, sourceText);
        var textExtracts = BidOpsOutcomeSupplierTextParser
            .Extract(title, noticeType, sourceText)
            .Select(EnrichDeterministicDiagnostics);
        var wrappedExtracts = BidOpsWrappedOutcomeTableParser
            .Extract(title, noticeType, sourceText)
            .Select(EnrichDeterministicDiagnostics);
        var awardDocuments = new[]
        {
            new BidOpsEvidenceDocument(
                new EvidenceSourceRef(rawNoticeId, null, noticeType, sourceUrl, null, null, null, null, null),
                title,
                noticeType,
                publishTime,
                sourceText)
        };
        IEnumerable<BidOpsOutcomeSupplierExtract> candidateExtracts = noticeKind == NoticeOutcomeKind.Candidate
            ? BidOpsCandidateEvidenceParser.Extract(awardDocuments).Select(ToOutcomeExtract)
            : Enumerable.Empty<BidOpsOutcomeSupplierExtract>();
        IEnumerable<BidOpsOutcomeSupplierExtract> awardExtracts = noticeKind == NoticeOutcomeKind.Award
            ? BidOpsAwardEvidenceParser.Extract(awardDocuments).Select(ToOutcomeExtract)
            : Enumerable.Empty<BidOpsOutcomeSupplierExtract>();

        var extracts = textExtracts
            .Concat(wrappedExtracts)
            .Concat(candidateExtracts)
            .Concat(awardExtracts)
            .Select(EnrichDeterministicDiagnostics)
            .Select(NormalizeNonAwardStatusExtract)
            .Where(x => !string.IsNullOrWhiteSpace(x.SupplierName))
            .ToList();

        return RemoveLessSpecificPackageContextDuplicates(extracts)
            .GroupBy(x => string.Join(
                '\u001f',
                BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(x.SupplierName),
                NormalizeCode(x.LotNo),
                NormalizeCode(x.PackageNo),
                NormalizeOutcomeType(x.OutcomeType),
                x.Rank?.ToString() ?? string.Empty))
            .Select(x => x
                .OrderByDescending(item => item.Confidence)
                .ThenByDescending(item => item.AwardAmount.HasValue)
                .First())
            .ToList();
    }

    private static BidOpsOutcomeSupplierExtract ToOutcomeExtract(CandidateEvidence evidence)
    {
        var sourceRowText = Truncate(evidence.Evidence.EvidenceText ?? string.Empty, 2000);
        return new BidOpsOutcomeSupplierExtract
        {
            SourceSequenceNo = ToSequenceNo(evidence.Evidence.RowIndex),
            SourceRowText = sourceRowText,
            SupplierName = evidence.SupplierName,
            OutcomeType = BidOpsOutcomeTypes.Candidate,
            Rank = evidence.Rank,
            AwardAmount = evidence.FinalQuoteAmount,
            SourceType = BidOpsOutcomeSupplierExtractSourceTypes.CandidateEvidenceParser,
            SourceParserVersion = BidOpsOutcomeSupplierExtractParserVersions.CandidateEvidenceParser,
            ProjectName = evidence.ProjectName ?? string.Empty,
            ProjectCode = evidence.ProjectCode ?? string.Empty,
            LotNo = evidence.LotNo ?? string.Empty,
            RawLotNo = evidence.LotNo ?? string.Empty,
            PackageNo = evidence.PackageNo ?? string.Empty,
            RawPackageNo = evidence.PackageNo ?? string.Empty,
            PackageName = evidence.PackageName ?? string.Empty,
            EvidenceText = sourceRowText,
            FieldEvidence = BuildFieldEvidence(
                ("supplierName", evidence.SupplierName),
                ("lotNo", evidence.LotNo),
                ("packageNo", evidence.PackageNo),
                ("packageName", evidence.PackageName),
                ("projectCode", evidence.ProjectCode),
                ("projectName", evidence.ProjectName),
                ("awardAmount", evidence.FinalQuoteAmount?.ToString(CultureInfo.InvariantCulture))),
            Confidence = ClampConfidence(evidence.Confidence)
        };
    }

    private static BidOpsOutcomeSupplierExtract ToOutcomeExtract(AwardEvidence evidence)
    {
        var sourceRowText = Truncate(evidence.Evidence.EvidenceText ?? string.Empty, 2000);
        return new BidOpsOutcomeSupplierExtract
        {
            SourceSequenceNo = ToSequenceNo(evidence.Evidence.RowIndex),
            SourceRowText = sourceRowText,
            SupplierName = evidence.AwardedSupplierName,
            OutcomeType = BidOpsOutcomeTypes.Awarded,
            AwardAmount = evidence.AwardAmount,
            SourceType = BidOpsOutcomeSupplierExtractSourceTypes.AwardEvidenceParser,
            SourceParserVersion = BidOpsOutcomeSupplierExtractParserVersions.AwardEvidenceParser,
            ProjectName = evidence.ProjectName ?? string.Empty,
            ProjectCode = evidence.ProjectCode ?? string.Empty,
            BuyerName = evidence.ProjectUnit ?? string.Empty,
            LotNo = evidence.LotNo ?? string.Empty,
            RawLotNo = evidence.LotNo ?? string.Empty,
            LotName = evidence.LotName ?? string.Empty,
            RawLotName = evidence.LotName ?? string.Empty,
            PackageNo = evidence.PackageNo ?? string.Empty,
            RawPackageNo = evidence.PackageNo ?? string.Empty,
            PackageName = evidence.PackageName ?? string.Empty,
            EvidenceText = sourceRowText,
            FieldEvidence = BuildFieldEvidence(
                ("supplierName", evidence.AwardedSupplierName),
                ("lotNo", evidence.LotNo),
                ("lotName", evidence.LotName),
                ("packageNo", evidence.PackageNo),
                ("packageName", evidence.PackageName),
                ("projectCode", evidence.ProjectCode),
                ("projectName", evidence.ProjectName),
                ("buyerName", evidence.ProjectUnit),
                ("awardAmount", evidence.AwardAmount?.ToString(CultureInfo.InvariantCulture))),
            Confidence = ClampConfidence(evidence.Confidence)
        };
    }

    private static BidOpsOutcomeSupplierExtract EnrichDeterministicDiagnostics(BidOpsOutcomeSupplierExtract extract)
    {
        if (extract.FieldEvidence is null)
            extract.FieldEvidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (extract.Warnings is null)
            extract.Warnings = [];

        if (string.IsNullOrWhiteSpace(extract.SourceRowText))
            extract.SourceRowText = Truncate(extract.EvidenceText, 2000);

        if (string.IsNullOrWhiteSpace(extract.EvidenceText))
            extract.EvidenceText = Truncate(extract.SourceRowText, 2000);

        if (string.IsNullOrWhiteSpace(extract.RawLotNo))
            extract.RawLotNo = extract.LotNo;

        if (string.IsNullOrWhiteSpace(extract.RawLotName))
            extract.RawLotName = extract.LotName;

        if (string.IsNullOrWhiteSpace(extract.RawPackageNo))
            extract.RawPackageNo = extract.PackageNo;

        AddFieldEvidence(extract.FieldEvidence, "supplierName", extract.SupplierName);
        AddFieldEvidence(extract.FieldEvidence, "lotNo", extract.LotNo);
        AddFieldEvidence(extract.FieldEvidence, "lotName", extract.LotName);
        AddFieldEvidence(extract.FieldEvidence, "packageNo", extract.PackageNo);
        AddFieldEvidence(extract.FieldEvidence, "packageName", extract.PackageName);
        AddFieldEvidence(extract.FieldEvidence, "projectCode", extract.ProjectCode);
        AddFieldEvidence(extract.FieldEvidence, "projectName", extract.ProjectName);
        AddFieldEvidence(extract.FieldEvidence, "buyerName", extract.BuyerName);

        return extract;
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> RemoveLessSpecificPackageContextDuplicates(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts)
    {
        return extracts
            .Where(x => !IsLessSpecificPackageContextDuplicate(x, extracts))
            .ToList();
    }

    private static BidOpsOutcomeSupplierExtract NormalizeNonAwardStatusExtract(BidOpsOutcomeSupplierExtract extract)
    {
        if (!BidOpsOutcomeRecordPolicy.IsNonAwardOutcome(
                extract.SupplierName,
                extract.OutcomeType,
                extract.EvidenceText))
        {
            return extract;
        }

        // “流标状态”是公告状态展示行，不是可闭环的中标供应商；金额字段不能跟随表格误入。
        return new BidOpsOutcomeSupplierExtract
        {
            SourceSequenceNo = extract.SourceSequenceNo,
            SourcePageNo = extract.SourcePageNo,
            SourceTableTitle = extract.SourceTableTitle,
            SourceRowText = extract.SourceRowText,
            SupplierName = extract.SupplierName,
            OutcomeType = BidOpsOutcomeTypes.Failed,
            Rank = extract.Rank,
            AwardAmount = null,
            ProcurementAgencyServiceFeeAmount = null,
            ExtractionOrder = extract.ExtractionOrder,
            SourceType = extract.SourceType,
            SourceParserVersion = extract.SourceParserVersion,
            SourceCallId = extract.SourceCallId,
            ProjectName = extract.ProjectName,
            ProjectCode = extract.ProjectCode,
            BuyerName = extract.BuyerName,
            LotNo = extract.LotNo,
            RawLotNo = extract.RawLotNo,
            LotNoValidationStatus = extract.LotNoValidationStatus,
            LotNoValidationReason = extract.LotNoValidationReason,
            RawLotName = extract.RawLotName,
            LotName = extract.LotName,
            RawPackageNo = extract.RawPackageNo,
            PackageNo = extract.PackageNo,
            PackageName = extract.PackageName,
            Category = extract.Category,
            EvidenceText = extract.EvidenceText,
            FieldEvidence = new Dictionary<string, string>(extract.FieldEvidence, StringComparer.OrdinalIgnoreCase),
            Warnings = [.. extract.Warnings],
            Confidence = Math.Min(extract.Confidence, 0.72m)
        };
    }

    private static bool IsLessSpecificPackageContextDuplicate(
        BidOpsOutcomeSupplierExtract current,
        IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts)
    {
        if (HasLotContext(current))
            return false;

        var supplier = BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(current.SupplierName);
        var packageNo = NormalizeCode(current.PackageNo);
        if (string.IsNullOrWhiteSpace(supplier) || string.IsNullOrWhiteSpace(packageNo))
            return false;

        return extracts.Any(other =>
            !ReferenceEquals(current, other) &&
            HasLotContext(other) &&
            string.Equals(BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(other.SupplierName), supplier, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeCode(other.PackageNo), packageNo, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeOutcomeType(other.OutcomeType), NormalizeOutcomeType(current.OutcomeType), StringComparison.OrdinalIgnoreCase) &&
            other.Rank == current.Rank);
    }

    private static bool HasLotContext(BidOpsOutcomeSupplierExtract extract)
    {
        return !string.IsNullOrWhiteSpace(NormalizeCode(extract.LotNo)) ||
               !string.IsNullOrWhiteSpace(NormalizeCode(extract.LotName));
    }

    private static NoticeOutcomeKind DetermineNoticeKind(
        string title,
        string noticeType,
        string sourceUrl,
        string text)
    {
        var typeSignal = noticeType ?? string.Empty;
        if (ContainsAny(typeSignal, "CandidateAnnouncement"))
            return NoticeOutcomeKind.Candidate;

        if (ContainsAny(typeSignal, "AwardAnnouncement", "ResultAnnouncement"))
            return NoticeOutcomeKind.Award;

        var titleSignal = title ?? string.Empty;
        if (ContainsAny(titleSignal, "中标候选人", "成交候选人", "候选人公示", "推荐的中标", "推荐的成交"))
            return NoticeOutcomeKind.Candidate;

        if (ContainsAny(titleSignal, "中标结果", "成交结果", "中标公告", "成交公告", "中标人名单"))
            return NoticeOutcomeKind.Award;

        // doci-win 是国网页面类型，候选人公示也会使用；只有在标题和 noticeType 都无法判断时才作为结果公告兜底。
        if (ContainsAny(sourceUrl ?? string.Empty, "doci-win"))
            return NoticeOutcomeKind.Award;

        var bodySignal = text[..Math.Min(text.Length, 2000)];
        // 中标公告正文常写“中标候选人公示活动已经结束”，不能因此降级成候选人公示。
        if (ContainsAny(bodySignal, "中标公告", "成交公告", "中标结果", "成交结果", "中标人名单", "现将中标人名单公告"))
            return NoticeOutcomeKind.Award;

        if (ContainsAny(bodySignal, "CandidateAnnouncement", "中标候选人", "成交候选人", "候选人公示", "推荐的中标", "推荐的成交"))
            return NoticeOutcomeKind.Candidate;

        if (ContainsAny(bodySignal, "AwardAnnouncement", "ResultAnnouncement"))
            return NoticeOutcomeKind.Award;

        return NoticeOutcomeKind.Unknown;
    }

    private static Dictionary<string, string> BuildFieldEvidence(params (string Name, string? Value)[] items)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
            AddFieldEvidence(result, item.Name, item.Value);

        return result;
    }

    private static void AddFieldEvidence(Dictionary<string, string> fieldEvidence, string name, string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned) || fieldEvidence.ContainsKey(name))
            return;

        fieldEvidence[name] = cleaned;
    }

    private static decimal ClampConfidence(double value)
    {
        return (decimal)Math.Clamp(value, 0d, 1d);
    }

    private static string ToSequenceNo(int? rowIndex)
    {
        return rowIndex.HasValue
            ? rowIndex.Value.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string Truncate(string? value, int maxLength)
    {
        var cleaned = value ?? string.Empty;
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
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

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private enum NoticeOutcomeKind
    {
        Unknown,
        Candidate,
        Award
    }
}
