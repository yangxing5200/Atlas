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
        var noticeKind = DetermineNoticeKind(title, noticeType, sourceUrl, text);
        var textExtracts = BidOpsOutcomeSupplierTextParser.Extract(title, noticeType, text);
        var wrappedExtracts = BidOpsWrappedOutcomeTableParser.Extract(title, noticeType, text);
        var awardDocuments = new[]
        {
            new BidOpsEvidenceDocument(
                new EvidenceSourceRef(rawNoticeId, null, noticeType, sourceUrl, null, null, null, null, null),
                title,
                noticeType,
                publishTime,
                text)
        };
        IEnumerable<BidOpsOutcomeSupplierExtract> candidateExtracts = noticeKind == NoticeOutcomeKind.Candidate
            ? BidOpsCandidateEvidenceParser.Extract(awardDocuments).Select(x => new BidOpsOutcomeSupplierExtract
            {
                SupplierName = x.SupplierName,
                OutcomeType = BidOpsOutcomeTypes.Candidate,
                Rank = x.Rank,
                AwardAmount = x.FinalQuoteAmount,
                ProjectName = x.ProjectName ?? string.Empty,
                ProjectCode = x.ProjectCode ?? string.Empty,
                LotNo = x.LotNo ?? string.Empty,
                PackageNo = x.PackageNo ?? string.Empty,
                PackageName = x.PackageName ?? string.Empty,
                EvidenceText = x.Evidence.EvidenceText ?? string.Empty,
                Confidence = (decimal)Math.Clamp(x.Confidence, 0d, 1d)
            })
            : Enumerable.Empty<BidOpsOutcomeSupplierExtract>();
        IEnumerable<BidOpsOutcomeSupplierExtract> awardExtracts = noticeKind == NoticeOutcomeKind.Award
            ? BidOpsAwardEvidenceParser.Extract(awardDocuments)
            .Select(x => new BidOpsOutcomeSupplierExtract
            {
                SupplierName = x.AwardedSupplierName,
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                AwardAmount = x.AwardAmount,
                ProjectName = x.ProjectName ?? string.Empty,
                ProjectCode = x.ProjectCode ?? string.Empty,
                BuyerName = x.ProjectUnit ?? string.Empty,
                LotNo = x.LotNo ?? string.Empty,
                LotName = x.LotName ?? string.Empty,
                PackageNo = x.PackageNo ?? string.Empty,
                PackageName = x.PackageName ?? string.Empty,
                EvidenceText = x.Evidence.EvidenceText ?? string.Empty,
                Confidence = (decimal)Math.Clamp(x.Confidence, 0d, 1d)
            })
            : Enumerable.Empty<BidOpsOutcomeSupplierExtract>();

        var extracts = textExtracts
            .Concat(wrappedExtracts)
            .Concat(candidateExtracts)
            .Concat(awardExtracts)
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
            SupplierName = extract.SupplierName,
            OutcomeType = BidOpsOutcomeTypes.Failed,
            Rank = extract.Rank,
            AwardAmount = null,
            ProcurementAgencyServiceFeeAmount = null,
            ExtractionOrder = extract.ExtractionOrder,
            ProjectName = extract.ProjectName,
            ProjectCode = extract.ProjectCode,
            BuyerName = extract.BuyerName,
            LotNo = extract.LotNo,
            LotName = extract.LotName,
            PackageNo = extract.PackageNo,
            PackageName = extract.PackageName,
            Category = extract.Category,
            EvidenceText = extract.EvidenceText,
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
