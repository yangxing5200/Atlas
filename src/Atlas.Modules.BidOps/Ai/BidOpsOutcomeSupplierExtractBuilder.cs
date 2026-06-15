using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Entities.Outcomes;

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

        return textExtracts
            .Concat(wrappedExtracts)
            .Concat(candidateExtracts)
            .Concat(awardExtracts)
            .Where(x => !string.IsNullOrWhiteSpace(x.SupplierName))
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

    private static NoticeOutcomeKind DetermineNoticeKind(
        string title,
        string noticeType,
        string sourceUrl,
        string text)
    {
        var signal = $"{title}\n{noticeType}\n{sourceUrl}\n{text[..Math.Min(text.Length, 2000)]}";
        if (ContainsAny(signal, "CandidateAnnouncement", "中标候选人", "成交候选人", "候选人公示", "推荐的中标", "推荐的成交"))
            return NoticeOutcomeKind.Candidate;

        if (ContainsAny(signal, "AwardAnnouncement", "ResultAnnouncement", "doci-win", "中标结果", "成交结果", "中标公告", "成交公告"))
            return NoticeOutcomeKind.Award;

        return NoticeOutcomeKind.Unknown;
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
