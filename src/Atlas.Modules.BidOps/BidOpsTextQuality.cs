using System.Net;
using System.Text.RegularExpressions;

namespace Atlas.Modules.BidOps;

internal static partial class BidOpsTextQuality
{
    public const string UnknownPackageMarker = "UNSPECIFIED";

    public static string CleanExtractedValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var cleaned = WebUtility.HtmlDecode(value)
            .Replace('\uFFFD', '?')
            .Replace('\u3000', ' ')
            .Trim();
        cleaned = HorizontalWhitespaceRegex().Replace(cleaned, " ").Trim();

        if (IsUnknownMarker(cleaned) || LooksUnreadablePlaceholder(cleaned))
            return string.Empty;

        return cleaned;
    }

    public static string CleanAndTruncate(string? value, int maxLength)
    {
        var cleaned = CleanExtractedValue(value);
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    public static bool LooksUnreadablePlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var signal = new string(value
            .Trim()
            .Where(x =>
                !char.IsWhiteSpace(x) &&
                !"_-—–/\\|,.，。:：;；()（）[]【】{}<>《》".Contains(x))
            .ToArray());
        if (signal.Length == 0)
            return false;

        if (signal.All(x => x is '?' or '？' or '\uFFFD'))
            return true;

        var questionCount = signal.Count(x => x is '?' or '？' or '\uFFFD');
        return questionCount >= 2 &&
               signal.All(x => x is '?' or '？' or '\uFFFD' || char.IsDigit(x));
    }

    public static bool IsUnknownMarker(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        return normalized.Equals(UnknownPackageMarker, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("NA", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("NULL", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("未识别", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("[^\\S\\r\\n]+", RegexOptions.Compiled)]
    private static partial Regex HorizontalWhitespaceRegex();
}
