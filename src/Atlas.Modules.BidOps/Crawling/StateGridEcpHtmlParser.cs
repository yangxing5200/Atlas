using System.Net;
using System.Text.RegularExpressions;

namespace Atlas.Modules.BidOps.Crawling;

public sealed record StateGridDiscoveredNotice(
    string Title,
    string DetailUrl,
    DateTime? PublishTime);

public sealed record StateGridAttachmentDocument(
    string FileName,
    string FileUrl,
    string FileType,
    long? FileSize);

public sealed record StateGridNoticeDocument(
    string Title,
    string Text,
    string Html,
    DateTime? PublishTime,
    IReadOnlyList<StateGridAttachmentDocument> Attachments);

public static partial class StateGridEcpHtmlParser
{
    private const int MaxTextLength = 200_000;

    public static IReadOnlyList<StateGridDiscoveredNotice> DiscoverNotices(
        string html,
        Uri listUri,
        int maxItems)
    {
        if (string.IsNullOrWhiteSpace(html))
            return Array.Empty<StateGridDiscoveredNotice>();

        maxItems = Math.Clamp(maxItems, 1, 50);
        var results = new List<StateGridDiscoveredNotice>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in AnchorRegex().Matches(html))
        {
            var href = Decode(match.Groups["href"].Value);
            var title = NormalizeText(StripTags(match.Groups["text"].Value));
            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(title))
                continue;

            if (!LooksLikeProcurementNotice(title) && !LooksLikeProcurementNotice(href))
                continue;

            if (!Uri.TryCreate(listUri, href, out var detailUri))
                continue;

            if (!seen.Add(detailUri.ToString()))
                continue;

            results.Add(new StateGridDiscoveredNotice(
                Trim(title, 500),
                detailUri.ToString(),
                TryExtractDate(match.Value)));

            if (results.Count >= maxItems)
                break;
        }

        return results;
    }

    public static StateGridNoticeDocument ExtractDetail(
        string html,
        string fallbackTitle,
        DateTime? fallbackPublishTime)
    {
        var title = ExtractTitle(html);
        if (string.IsNullOrWhiteSpace(title))
            title = fallbackTitle;

        var publishTime = TryExtractDate(html) ?? fallbackPublishTime;
        var text = NormalizeText(StripTags(RemoveNonContentBlocks(html)));
        if (string.IsNullOrWhiteSpace(text))
            text = title;

        return new StateGridNoticeDocument(
            Trim(title, 500),
            Trim(text, MaxTextLength),
            html,
            publishTime,
            DiscoverAttachments(html, new Uri("https://ecp.sgcc.com.cn/ecp2.0/portal/")));
    }

    public static IReadOnlyList<StateGridAttachmentDocument> DiscoverAttachments(
        string html,
        Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(html))
            return Array.Empty<StateGridAttachmentDocument>();

        var results = new List<StateGridAttachmentDocument>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AnchorRegex().Matches(html))
        {
            var href = Decode(match.Groups["href"].Value);
            if (string.IsNullOrWhiteSpace(href) || !LooksLikeAttachment(href))
                continue;

            if (!Uri.TryCreate(baseUri, href, out var uri))
                continue;

            var fileUrl = uri.ToString();
            if (!seen.Add(fileUrl))
                continue;

            var title = NormalizeText(StripTags(match.Groups["text"].Value));
            var fileName = string.IsNullOrWhiteSpace(title)
                ? Path.GetFileName(uri.LocalPath)
                : title;
            results.Add(new StateGridAttachmentDocument(
                Trim(fileName, 500),
                fileUrl,
                DetectFileType(fileUrl),
                null));
        }

        return results;
    }

    private static string ExtractTitle(string html)
    {
        foreach (var regex in new[] { H1Regex(), TitleRegex() })
        {
            var match = regex.Match(html);
            if (match.Success)
                return Trim(NormalizeText(StripTags(match.Groups["text"].Value)), 500);
        }

        return string.Empty;
    }

    private static bool LooksLikeProcurementNotice(string value)
    {
        return ProcurementKeywordRegex().IsMatch(value);
    }

    private static bool LooksLikeAttachment(string value)
    {
        return AttachmentExtensionRegex().IsMatch(value);
    }

    private static string DetectFileType(string value)
    {
        var path = value.Split('?', '#')[0];
        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(extension) ? "file" : extension;
    }

    private static DateTime? TryExtractDate(string value)
    {
        var match = DateRegex().Match(value);
        if (!match.Success)
            return null;

        var normalized = match.Value.Replace('年', '-').Replace('月', '-').Replace("日", string.Empty);
        return DateTime.TryParse(normalized, out var parsed) ? parsed : null;
    }

    private static string RemoveNonContentBlocks(string html)
    {
        var withoutScripts = ScriptRegex().Replace(html, " ");
        return StyleRegex().Replace(withoutScripts, " ");
    }

    private static string StripTags(string html)
    {
        return TagRegex().Replace(html, " ");
    }

    private static string NormalizeText(string value)
    {
        return WhitespaceRegex().Replace(Decode(value), " ").Trim();
    }

    private static string Decode(string value)
    {
        return WebUtility.HtmlDecode(value ?? string.Empty);
    }

    private static string Trim(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    [GeneratedRegex("<a\\b[^>]*href\\s*=\\s*[\"'](?<href>[^\"']+)[\"'][^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AnchorRegex();

    [GeneratedRegex("<h1\\b[^>]*>(?<text>.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex H1Regex();

    [GeneratedRegex("<title\\b[^>]*>(?<text>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex("<script\\b[^>]*>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptRegex();

    [GeneratedRegex("<style\\b[^>]*>.*?</style>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex("\\s+", RegexOptions.Singleline)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("(招标|采购|公告|中标|成交|竞争性谈判|询价|单一来源|bid|tender|notice|bulletin)", RegexOptions.IgnoreCase)]
    private static partial Regex ProcurementKeywordRegex();

    [GeneratedRegex("\\.(pdf|doc|docx|xls|xlsx|zip|rar|txt|html?)(\\?|#|$)", RegexOptions.IgnoreCase)]
    private static partial Regex AttachmentExtensionRegex();

    [GeneratedRegex("\\d{4}[-年/]\\d{1,2}[-月/]\\d{1,2}日?")]
    private static partial Regex DateRegex();
}
