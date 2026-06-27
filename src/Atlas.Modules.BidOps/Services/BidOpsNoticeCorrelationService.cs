using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Entities.Crawling;
using System.Text.RegularExpressions;

namespace Atlas.Modules.BidOps.Services;

public static class BidOpsNoticeCorrelationService
{
    public static BidOpsNoticeMatch Match(
        RawNotice raw,
        IReadOnlyList<BidOpsEvidenceDocument> documents,
        IReadOnlyList<AwardEvidence> awards,
        string stage,
        DateTime? awardPublishTime)
    {
        ArgumentNullException.ThrowIfNull(raw);
        documents ??= [];
        awards ??= [];

        var text = string.Join('\n', documents.Select(x => x.Text));
        var searchable = string.Join('\n', raw.Title, raw.SourceNoticeId, raw.TextPreview, text);
        var reasons = new List<string>();
        var missing = new List<string>();
        var confidence = 0d;
        var projectCodes = awards.Select(x => x.ProjectCode).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var projectNames = awards.Select(x => x.ProjectName).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var packageNos = awards.Select(x => x.NormalizedPackageNo).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var lotNos = awards.Select(x => x.LotNo).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var suppliers = awards.Select(x => x.AwardedSupplierName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var noticeProjectCode = FirstNonEmpty(BidOpsEvidenceText.ExtractProjectCode(searchable), MatchingProjectCode(projectCodes, searchable));
        if (!string.IsNullOrWhiteSpace(noticeProjectCode) &&
            projectCodes.Any(x => string.Equals(x, noticeProjectCode, StringComparison.OrdinalIgnoreCase)))
        {
            confidence += 0.45;
            reasons.Add("ProjectCode exact match");
        }
        else if (projectCodes.Length > 0)
        {
            missing.Add("ProjectCode not matched");
        }

        var noticeProjectName = FirstNonEmpty(BidOpsEvidenceText.ExtractProjectName(searchable), raw.Title);
        if (projectNames.Any(x => BidOpsEvidenceText.Similarity(x, noticeProjectName) >= 0.72))
        {
            confidence += 0.22;
            reasons.Add("ProjectName high similarity");
        }
        else if (projectNames.Length > 0)
        {
            missing.Add("ProjectName not matched");
        }

        if (lotNos.Any(x => searchable.Contains(x, StringComparison.OrdinalIgnoreCase)))
        {
            confidence += 0.12;
            reasons.Add("LotNo exact match");
        }

        if (packageNos.Any(x => ContainsNormalizedPackageNo(searchable, x)))
        {
            confidence += 0.13;
            reasons.Add("Normalized PackageNo exact match");
        }
        else if (packageNos.Length > 0)
        {
            missing.Add("PackageNo not matched");
        }

        if (string.Equals(stage, "Candidate", StringComparison.OrdinalIgnoreCase) &&
            suppliers.Any(supplier => BidOpsSupplierNameNormalizer.NormalizeForMatch(searchable).Contains(
                BidOpsSupplierNameNormalizer.NormalizeForMatch(supplier),
                StringComparison.OrdinalIgnoreCase)))
        {
            confidence += 0.08;
            reasons.Add("Awarded supplier appears in candidate notice text");
        }

        if (string.Equals(stage, "Candidate", StringComparison.OrdinalIgnoreCase) && LooksLikeCandidateNotice(raw))
        {
            confidence += 0.12;
            reasons.Add("Notice type/title indicates candidate announcement");
        }
        else if (string.Equals(stage, "Tender", StringComparison.OrdinalIgnoreCase) && LooksLikeTenderNotice(raw))
        {
            confidence += 0.1;
            reasons.Add("Notice type/title indicates tender/procurement announcement");
        }
        else
        {
            missing.Add("Notice type/title did not match expected stage");
        }

        if (awardPublishTime.HasValue && raw.PublishTime.HasValue)
        {
            if (raw.PublishTime.Value <= awardPublishTime.Value.AddDays(7))
            {
                confidence += 0.08;
                reasons.Add("Publish time sequence is reasonable");
            }
            else
            {
                confidence = Math.Max(0d, confidence - 0.15);
                missing.Add("Publish time is after award notice");
            }
        }

        var level = confidence >= 0.75
            ? "High"
            : confidence >= 0.45
                ? "Medium"
                : "Low";
        var missingReason = confidence <= 0
            ? "project code/name/package hint not matched"
            : missing.Count == 0 ? null : string.Join("; ", missing.Distinct(StringComparer.OrdinalIgnoreCase));

        return new BidOpsNoticeMatch(
            raw.Id,
            raw.Title,
            raw.NoticeType,
            raw.DetailUrl,
            raw.PublishTime,
            Math.Min(1d, Math.Round(confidence, 3)),
            reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            missingReason,
            level,
            missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static string MatchingProjectCode(IReadOnlyList<string> projectCodes, string text)
    {
        return projectCodes.FirstOrDefault(x => text.Contains(x, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
    }

    private static bool ContainsNormalizedPackageNo(string text, string? normalizedPackageNo)
    {
        if (string.IsNullOrWhiteSpace(normalizedPackageNo))
            return false;

        if (Regex.IsMatch(
                text,
                $@"(?:包号|包件号|分包编号|分包号|标包号|包|分包|标包)\s*0*{Regex.Escape(normalizedPackageNo)}(?:\D|$)",
                RegexOptions.CultureInvariant))
        {
            return true;
        }

        return text.Split(['\r', '\n', '。', '；', ';', '|'], StringSplitOptions.RemoveEmptyEntries)
            .Select(BidOpsPackageNoNormalizer.Normalize)
            .Any(x => string.Equals(x, normalizedPackageNo, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeCandidateNotice(RawNotice raw)
    {
        return ContainsAny($"{raw.NoticeType} {raw.Title}", "Candidate", "候选人", "推荐成交候选", "推荐中标候选", "公示");
    }

    private static bool LooksLikeTenderNotice(RawNotice raw)
    {
        var signal = $"{raw.NoticeType} {raw.Title}";
        if (ContainsAny(signal, "Candidate", "Award", "中标", "成交结果", "候选"))
            return false;

        return ContainsAny(signal, "Tender", "Procurement", "招标公告", "采购公告", "公开谈判采购", "竞争性谈判采购", "询价采购");
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
            if (!string.IsNullOrWhiteSpace(cleaned))
                return cleaned;
        }

        return string.Empty;
    }
}
