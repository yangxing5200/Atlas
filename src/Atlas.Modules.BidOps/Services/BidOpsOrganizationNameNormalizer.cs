using System.Text.RegularExpressions;

namespace Atlas.Modules.BidOps.Services;

public static partial class BidOpsOrganizationNameNormalizer
{
    public static string Clean(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        cleaned = LeadingLabelRegex().Replace(cleaned, string.Empty);
        cleaned = cleaned.Trim(' ', ':', '：', '=', '-', '、', ',', '，', '.', '。', '；', ';');
        return cleaned.Length <= 300 ? cleaned : cleaned[..300];
    }

    public static string NormalizeForMatch(string? value)
    {
        var cleaned = Clean(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        return new string(cleaned
                .Where(x => !char.IsWhiteSpace(x) && !"()（）[]【】{}<>《》,，.。;；:：-_—–/\\|".Contains(x))
                .ToArray())
            .ToUpperInvariant();
    }

    [GeneratedRegex(@"^(?:采购人|招标人|项目单位|建设单位|业主单位|需求单位|中标人|成交人|供应商|厂家)\s*[:：=]?\s*", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingLabelRegex();
}
