using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Entities.Outcomes;

namespace Atlas.Modules.BidOps.Services;

internal static class BidOpsOutcomeRecordPolicy
{
    private static readonly string[] NonAwardSupplierTokens =
    [
        "流标",
        "流标状态",
        "本包流标",
        "本次流标",
        "废标",
        "废标状态",
        "本包废标",
        "本次废标",
        "失败",
        "采购失败",
        "招标失败",
        "成交失败",
        "中标失败",
        "未成交",
        "未中标",
        "无成交人",
        "无中标人",
        "无成交供应商",
        "无中标供应商",
        "终止",
        "终止采购",
        "项目终止"
    ];

    public static bool IsNonAwardOutcome(OutcomeSupplierRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return IsNonAwardOutcome(record.SupplierName, record.OutcomeType, record.EvidenceText);
    }

    public static bool IsNonAwardLifecycleLink(LifecyclePackageLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        return IsNonAwardSupplierName(link.SupplierName);
    }

    public static bool IsNonAwardAwardEvidence(AwardEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return IsNonAwardSupplierName(evidence.AwardedSupplierName);
    }

    public static bool IsNonAwardOutcome(string? supplierName, string? outcomeType, string? evidenceText)
    {
        return string.Equals(outcomeType, BidOpsOutcomeTypes.Failed, StringComparison.OrdinalIgnoreCase) ||
               IsNonAwardSupplierName(supplierName) ||
               (IsNonAwardSupplierName(evidenceText) && string.IsNullOrWhiteSpace(supplierName));
    }

    public static bool IsNonAwardSupplierName(string? value)
    {
        var normalized = NormalizeStatusText(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (NonAwardSupplierTokens.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return true;

        // 公告表格有时把“包1流标/流标状态”落在中标商家列；这类短状态不是供应商实体。
        return normalized.Length <= 16 &&
               (normalized.Contains("流标", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("废标", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("采购失败", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("招标失败", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("成交失败", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("中标失败", StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeOutcomeTypeForPersistence(
        string? outcomeType,
        string? supplierName,
        string? evidenceText,
        string fallback)
    {
        return IsNonAwardOutcome(supplierName, outcomeType, evidenceText)
            ? BidOpsOutcomeTypes.Failed
            : fallback;
    }

    public static decimal? DisplayAwardAmount(OutcomeSupplierRecord record)
    {
        return IsNonAwardOutcome(record) ? null : record.AwardAmount;
    }

    private static string NormalizeStatusText(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        return new string(cleaned
                .Where(x => !char.IsWhiteSpace(x) && !"()（）[]【】{}<>《》,，.。;；:：-_—–/\\|".Contains(x))
                .ToArray())
            .ToUpperInvariant();
    }
}
