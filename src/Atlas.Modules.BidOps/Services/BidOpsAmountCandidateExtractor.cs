using System.Globalization;
using System.Text.RegularExpressions;
using Atlas.Modules.BidOps.Entities.Outcomes;

namespace Atlas.Modules.BidOps.Services;

public sealed record BidOpsExtractedAmountCandidate(
    string AmountRaw,
    decimal? AmountValue,
    string AmountUnit,
    string AmountType,
    string Status,
    string RejectReason,
    bool IsPotentialFinalAmount,
    decimal Confidence,
    string ContextText,
    string SourceLocation);

public static partial class BidOpsAmountCandidateExtractor
{
    private const int ContextRadius = 42;

    public static IReadOnlyList<BidOpsExtractedAmountCandidate> ExtractTextCandidates(
        string? text,
        string sourceLocationPrefix = "text")
    {
        var source = text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
            return [];

        var candidates = new List<BidOpsExtractedAmountCandidate>();
        var claimedSpans = new HashSet<string>(StringComparer.Ordinal);
        ExtractMoneyCandidates(source, sourceLocationPrefix, candidates, claimedSpans);
        ExtractPercentCandidates(source, sourceLocationPrefix, candidates, claimedSpans);
        ExtractFoldCandidates(source, sourceLocationPrefix, candidates, claimedSpans);
        return candidates;
    }

    public static string ClassifyAmountType(string? context)
    {
        var normalized = NormalizeContext(context);
        if (ContainsAny(normalized, "代理服务费", "代理费", "招标代理服务费", "采购代理服务费", "中标服务费", "成交服务费"))
            return BidOpsAmountCandidateTypes.AgencyFee;
        if (ContainsAny(normalized, "保证金", "投标保证", "响应保证", "履约保证"))
            return BidOpsAmountCandidateTypes.Deposit;
        if (ContainsAny(normalized, "单价", "综合单价", "含税单价", "不含税单价"))
            return BidOpsAmountCandidateTypes.UnitPrice;
        if (ContainsAny(normalized, "最高限价", "最高投标限价", "最高应答限价", "控制价", "限价金额", "最高限额"))
            return BidOpsAmountCandidateTypes.CeilingPrice;
        if (ContainsAny(normalized, "预算金额", "项目预算", "采购预算", "预算价", "预算总额"))
            return BidOpsAmountCandidateTypes.BudgetAmount;
        if (ContainsAny(normalized, "下浮率", "下浮", "降幅", "优惠率"))
            return BidOpsAmountCandidateTypes.ReductionRate;
        if (ContainsAny(normalized, "折扣率", "折扣", "折"))
            return BidOpsAmountCandidateTypes.DiscountRate;
        if (ContainsAny(normalized, "费率", "税率", "比例", "百分比"))
            return BidOpsAmountCandidateTypes.Rate;
        if (ContainsAny(normalized, "中标金额", "中标总金额", "中标价", "中标价格"))
            return normalized.Contains("中标价", StringComparison.Ordinal)
                ? BidOpsAmountCandidateTypes.WinningPrice
                : BidOpsAmountCandidateTypes.WinningAmount;
        if (ContainsAny(normalized, "成交金额", "成交总金额", "成交价", "成交价格"))
            return normalized.Contains("成交价", StringComparison.Ordinal)
                ? BidOpsAmountCandidateTypes.DealPrice
                : BidOpsAmountCandidateTypes.DealAmount;
        if (ContainsAny(normalized, "最终报价", "评审价", "评审价格"))
            return BidOpsAmountCandidateTypes.FinalQuote;
        if (ContainsAny(normalized, "总报价", "报价总价", "投标总价", "应答总价", "响应总价"))
            return BidOpsAmountCandidateTypes.TotalQuote;
        if (ContainsAny(normalized, "投标报价", "投标价"))
            return BidOpsAmountCandidateTypes.BidQuote;
        if (ContainsAny(normalized, "响应报价", "应答报价"))
            return BidOpsAmountCandidateTypes.ResponseQuote;
        if (ContainsAny(normalized, "报价金额", "报价"))
            return BidOpsAmountCandidateTypes.QuoteAmount;

        return BidOpsAmountCandidateTypes.Unknown;
    }

    public static bool IsPotentialFinalAmountType(string amountType)
    {
        return amountType is
            BidOpsAmountCandidateTypes.WinningAmount or
            BidOpsAmountCandidateTypes.DealAmount or
            BidOpsAmountCandidateTypes.WinningPrice or
            BidOpsAmountCandidateTypes.DealPrice or
            BidOpsAmountCandidateTypes.FinalQuote or
            BidOpsAmountCandidateTypes.TotalQuote or
            BidOpsAmountCandidateTypes.BidQuote or
            BidOpsAmountCandidateTypes.ResponseQuote or
            BidOpsAmountCandidateTypes.QuoteAmount;
    }

    public static (string Status, string RejectReason, decimal Confidence) ResolveStatus(
        string amountType,
        decimal? amountValue)
    {
        if (!amountValue.HasValue)
            return (BidOpsAmountCandidateStatuses.Unresolved, "未能归一化金额/费率数值。", 0.4m);

        if (IsPotentialFinalAmountType(amountType))
            return (BidOpsAmountCandidateStatuses.Recommended, string.Empty, 0.9m);

        return amountType switch
        {
            BidOpsAmountCandidateTypes.BudgetAmount =>
                (BidOpsAmountCandidateStatuses.Rejected, "预算金额不是最终中标/成交金额。", 0.78m),
            BidOpsAmountCandidateTypes.CeilingPrice =>
                (BidOpsAmountCandidateStatuses.Rejected, "最高限价不是最终中标/成交金额。", 0.78m),
            BidOpsAmountCandidateTypes.AgencyFee =>
                (BidOpsAmountCandidateStatuses.Rejected, "代理服务费不是中标/成交金额。", 0.82m),
            BidOpsAmountCandidateTypes.Deposit =>
                (BidOpsAmountCandidateStatuses.Rejected, "保证金不是中标/成交金额。", 0.82m),
            BidOpsAmountCandidateTypes.UnitPrice =>
                (BidOpsAmountCandidateStatuses.Rejected, "单价不是包级最终金额。", 0.7m),
            BidOpsAmountCandidateTypes.Rate or
            BidOpsAmountCandidateTypes.DiscountRate or
            BidOpsAmountCandidateTypes.ReductionRate =>
                (BidOpsAmountCandidateStatuses.Unresolved, "费率/折扣率需要结合基准金额推导。", 0.68m),
            _ => (BidOpsAmountCandidateStatuses.Unresolved, "金额类型未知，需人工判定。", 0.55m)
        };
    }

    private static void ExtractMoneyCandidates(
        string source,
        string sourceLocationPrefix,
        ICollection<BidOpsExtractedAmountCandidate> candidates,
        ISet<string> claimedSpans)
    {
        foreach (Match match in MoneyRegex().Matches(source))
        {
            if (!match.Success || !ClaimSpan(match, claimedSpans))
                continue;

            var raw = match.Value.Trim();
            var context = ExtractContext(source, match.Index, match.Length);
            var hasCurrencySymbol = !string.IsNullOrWhiteSpace(match.Groups["currency"].Value);
            var explicitUnit = match.Groups["unit"].Value;
            if (string.IsNullOrWhiteSpace(explicitUnit) &&
                !hasCurrencySymbol &&
                !LooksLikeMoneyContext(context))
            {
                continue;
            }

            if (!TryNormalizeMoney(match.Groups["amount"].Value, explicitUnit, context, out var amount, out var unit))
                continue;

            var type = ClassifyAmountType(context);
            var status = ResolveStatus(type, amount);
            candidates.Add(new BidOpsExtractedAmountCandidate(
                raw,
                amount,
                unit,
                type,
                status.Status,
                status.RejectReason,
                IsPotentialFinalAmountType(type),
                status.Confidence,
                context,
                BuildSourceLocation(source, match.Index, sourceLocationPrefix)));
        }
    }

    private static void ExtractPercentCandidates(
        string source,
        string sourceLocationPrefix,
        ICollection<BidOpsExtractedAmountCandidate> candidates,
        ISet<string> claimedSpans)
    {
        foreach (Match match in PercentRegex().Matches(source))
        {
            if (!match.Success || !ClaimSpan(match, claimedSpans))
                continue;

            if (!decimal.TryParse(
                    match.Groups["amount"].Value,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var percent))
            {
                continue;
            }

            var context = ExtractContext(source, match.Index, match.Length);
            var type = ClassifyAmountType(context);
            if (type is not (BidOpsAmountCandidateTypes.DiscountRate or BidOpsAmountCandidateTypes.ReductionRate))
                type = BidOpsAmountCandidateTypes.Rate;

            var value = Math.Round(percent / 100m, 6);
            var status = ResolveStatus(type, value);
            candidates.Add(new BidOpsExtractedAmountCandidate(
                match.Value.Trim(),
                value,
                "rate",
                type,
                status.Status,
                status.RejectReason,
                false,
                status.Confidence,
                context,
                BuildSourceLocation(source, match.Index, sourceLocationPrefix)));
        }
    }

    private static void ExtractFoldCandidates(
        string source,
        string sourceLocationPrefix,
        ICollection<BidOpsExtractedAmountCandidate> candidates,
        ISet<string> claimedSpans)
    {
        foreach (Match match in FoldRegex().Matches(source))
        {
            if (!match.Success || !ClaimSpan(match, claimedSpans))
                continue;

            var value = TryParseFold(match.Groups["value"].Value);
            if (!value.HasValue)
                continue;

            var context = ExtractContext(source, match.Index, match.Length);
            var type = BidOpsAmountCandidateTypes.DiscountRate;
            var status = ResolveStatus(type, value.Value);
            candidates.Add(new BidOpsExtractedAmountCandidate(
                match.Value.Trim(),
                value.Value,
                "discount",
                type,
                status.Status,
                status.RejectReason,
                false,
                status.Confidence,
                context,
                BuildSourceLocation(source, match.Index, sourceLocationPrefix)));
        }
    }

    private static bool TryNormalizeMoney(
        string amountText,
        string explicitUnit,
        string context,
        out decimal amount,
        out string unit)
    {
        amount = 0m;
        unit = string.Empty;
        var normalizedAmountText = amountText.Replace(",", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(normalizedAmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            return false;

        var normalizedContext = NormalizeContext(context);
        unit = explicitUnit switch
        {
            "亿元" or "亿" => "亿元",
            "万元" or "万" => "万元",
            "元" => "元",
            _ when ContainsAny(normalizedContext, "单位:亿元", "单位：亿元", "金额单位亿元", "金额单位：亿元") => "亿元",
            _ when ContainsAny(normalizedContext, "单位:万", "单位：万", "金额单位万元", "金额单位：万元", "人民币万元") => "万元",
            _ => "元"
        };

        amount = unit switch
        {
            "亿元" => amount * 100_000_000m,
            "万元" => amount * 10_000m,
            _ => amount
        };
        amount = Math.Round(amount, 2);
        return amount > 0m;
    }

    private static decimal? TryParseFold(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            return Math.Round(number / 10m, 6);

        var digits = value
            .Select(ChineseDigit)
            .Where(x => x >= 0)
            .ToArray();
        if (digits.Length == 1)
            return Math.Round(digits[0] / 10m, 6);
        if (digits.Length == 2)
            return Math.Round((digits[0] * 10m + digits[1]) / 100m, 6);

        return null;
    }

    private static int ChineseDigit(char ch)
    {
        return ch switch
        {
            '零' or '〇' => 0,
            '一' or '壹' => 1,
            '二' or '两' or '贰' => 2,
            '三' or '叁' => 3,
            '四' or '肆' => 4,
            '五' or '伍' => 5,
            '六' or '陆' => 6,
            '七' or '柒' => 7,
            '八' or '捌' => 8,
            '九' or '玖' => 9,
            _ => -1
        };
    }

    private static bool ClaimSpan(Match match, ISet<string> claimedSpans)
    {
        return claimedSpans.Add($"{match.Index}:{match.Length}");
    }

    private static string ExtractContext(string source, int index, int length)
    {
        var start = Math.Max(0, index - ContextRadius);
        var end = Math.Min(source.Length, index + length + ContextRadius);
        return BidOpsTextQuality.CleanExtractedValue(source[start..end]) ?? string.Empty;
    }

    private static string BuildSourceLocation(string source, int index, string sourceLocationPrefix)
    {
        var line = 1;
        for (var i = 0; i < index && i < source.Length; i++)
        {
            if (source[i] == '\n')
                line++;
        }

        return $"{sourceLocationPrefix}:line:{line.ToString(CultureInfo.InvariantCulture)}";
    }

    private static bool LooksLikeMoneyContext(string context)
    {
        var normalized = NormalizeContext(context);
        return ContainsAny(
            normalized,
            "金额",
            "报价",
            "价",
            "预算",
            "限价",
            "费用",
            "保证金",
            "人民币",
            "元",
            "万元",
            "亿元");
    }

    private static string NormalizeContext(string? context)
    {
        return (BidOpsTextQuality.CleanExtractedValue(context) ?? string.Empty)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("　", string.Empty, StringComparison.Ordinal);
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"(?<currency>人民币|RMB|CNY|￥|¥)?\s*(?<amount>(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?)\s*(?<unit>亿元|亿|万元|万|元)?", RegexOptions.CultureInvariant)]
    private static partial Regex MoneyRegex();

    [GeneratedRegex(@"(?<amount>\d+(?:\.\d+)?)\s*(?:%|％)", RegexOptions.CultureInvariant)]
    private static partial Regex PercentRegex();

    [GeneratedRegex(@"(?<value>\d+(?:\.\d+)?|[零〇一二两三四五六七八九壹贰叁肆伍陆柒捌玖]{1,2})\s*折", RegexOptions.CultureInvariant)]
    private static partial Regex FoldRegex();
}
