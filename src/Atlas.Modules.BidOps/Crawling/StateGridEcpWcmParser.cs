using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Atlas.Modules.BidOps.Crawling;

public sealed record StateGridEcpApiNotice(
    string Title,
    string DetailUrl,
    string Doctype,
    string MenuId,
    long NoticeId,
    long? FirstPageDocId,
    DateTime? PublishTime,
    string PublishOrgName,
    string ProjectCode);

public static partial class StateGridEcpWcmParser
{
    private const int MaxTextLength = 200_000;

    public static bool TryGetMenuId(string? value, out string menuId)
    {
        menuId = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var matches = MenuIdRegex().Matches(value);
        if (matches.Count == 0)
            return false;

        menuId = matches[^1].Value;
        return true;
    }

    public static IReadOnlyList<StateGridEcpApiNotice> ParseNoticeList(
        string json,
        string portalBaseUrl,
        int maxItems)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<StateGridEcpApiNotice>();

        maxItems = Math.Clamp(maxItems, 1, 50);
        using var document = JsonDocument.Parse(json);
        EnsureSuccessful(document.RootElement);

        if (!TryGetProperty(document.RootElement, "resultValue", out var resultValue) ||
            !TryGetProperty(resultValue, "noteList", out var noteList) ||
            noteList.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<StateGridEcpApiNotice>();
        }

        var notices = new List<StateGridEcpApiNotice>();
        foreach (var item in noteList.EnumerateArray())
        {
            var title = GetString(item, "title");
            var doctype = GetString(item, "doctype");
            var menuId = GetString(item, "firstPageMenuId");
            var noticeId = GetLong(item, "noticeId");
            var firstPageDocId = GetNullableLong(item, "firstPageDocId");
            if (string.IsNullOrWhiteSpace(title) || noticeId <= 0)
                continue;

            if (string.IsNullOrWhiteSpace(doctype))
                doctype = "doci-bid";

            if (string.IsNullOrWhiteSpace(menuId) &&
                !TryGetMenuId(portalBaseUrl, out menuId))
            {
                menuId = string.Empty;
            }

            var detailUrl = BuildPortalDetailUrl(
                portalBaseUrl,
                doctype,
                firstPageDocId ?? noticeId,
                menuId);

            notices.Add(new StateGridEcpApiNotice(
                Trim(title, 500),
                detailUrl,
                doctype,
                menuId,
                noticeId,
                firstPageDocId,
                TryParseDate(GetString(item, "noticePublishTime")),
                Trim(GetString(item, "publishOrgName"), 200),
                Trim(GetString(item, "code"), 100)));

            if (notices.Count >= maxItems)
                break;
        }

        return notices;
    }

    public static StateGridNoticeDocument ParseNoticeDetail(
        string json,
        StateGridEcpApiNotice notice)
    {
        using var document = JsonDocument.Parse(json);
        EnsureSuccessful(document.RootElement);

        if (!TryGetProperty(document.RootElement, "resultValue", out var resultValue))
            return CreateFallbackDocument(notice);

        var title = string.IsNullOrWhiteSpace(notice.Title)
            ? FindFirstString(
                resultValue,
                "PURPRJ_NAME",
                "NOTICE_TITLE",
                "TITLE") ?? notice.Title
            : notice.Title;
        var publishTime = FindFirstDate(
            resultValue,
            "PUB_TIME",
            "NOTICE_PUBLISH_TIME",
            "NOTICEPUBLISHTIME") ?? notice.PublishTime;

        var builder = new StringBuilder();
        AppendLine(builder, title);
        AppendLine(builder, $"SourceUrl: {notice.DetailUrl}");
        AppendLine(builder, $"NoticeId: {notice.NoticeId}");
        if (notice.FirstPageDocId.HasValue)
            AppendLine(builder, $"FirstPageDocId: {notice.FirstPageDocId.Value}");
        AppendLine(builder, $"Doctype: {notice.Doctype}");
        AppendLine(builder, $"MenuId: {notice.MenuId}");
        AppendLine(builder, $"PublishOrgName: {notice.PublishOrgName}");
        AppendLine(builder, $"ProjectCode: {notice.ProjectCode}");
        if (notice.PublishTime.HasValue)
            AppendLine(builder, $"ListPublishTime: {notice.PublishTime:yyyy-MM-dd}");
        AppendJsonText(resultValue, "resultValue", builder, 0);

        var text = Trim(builder.ToString(), MaxTextLength);
        return new StateGridNoticeDocument(
            Trim(title, 500),
            text,
            BuildSyntheticHtml(title, text),
            publishTime,
            DiscoverAttachments(resultValue, notice.DetailUrl));
    }

    public static StateGridNoticeDocument CreateFallbackDocument(StateGridEcpApiNotice notice)
    {
        var builder = new StringBuilder();
        AppendLine(builder, notice.Title);
        AppendLine(builder, $"SourceUrl: {notice.DetailUrl}");
        AppendLine(builder, $"NoticeId: {notice.NoticeId}");
        if (notice.FirstPageDocId.HasValue)
            AppendLine(builder, $"FirstPageDocId: {notice.FirstPageDocId.Value}");
        AppendLine(builder, $"Doctype: {notice.Doctype}");
        AppendLine(builder, $"MenuId: {notice.MenuId}");
        AppendLine(builder, $"PublishOrgName: {notice.PublishOrgName}");
        AppendLine(builder, $"ProjectCode: {notice.ProjectCode}");
        if (notice.PublishTime.HasValue)
            AppendLine(builder, $"PublishTime: {notice.PublishTime:yyyy-MM-dd}");

        var text = Trim(builder.ToString(), MaxTextLength);
        return new StateGridNoticeDocument(
            notice.Title,
            text,
            BuildSyntheticHtml(notice.Title, text),
            notice.PublishTime,
            Array.Empty<StateGridAttachmentDocument>());
    }

    public static string GetDetailApiPath(string doctype)
    {
        return doctype.Trim().ToLowerInvariant() switch
        {
            "doci-change" => "index/getChangeBid",
            "doci-delay" => "index/getDelayNoticeBid",
            "doci-waste" => "index/getNoticeWaste",
            "doci-win" => "index/getNoticeWin",
            _ => "index/getNoticeBid"
        };
    }

    private static void EnsureSuccessful(JsonElement root)
    {
        if (TryGetProperty(root, "successful", out var successful) &&
            successful.ValueKind == JsonValueKind.True)
        {
            return;
        }

        var hint = GetString(root, "resultHint");
        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(hint)
                ? "State Grid ECP API returned an unsuccessful response."
                : $"State Grid ECP API returned an unsuccessful response: {hint}");
    }

    private static string BuildPortalDetailUrl(
        string portalBaseUrl,
        string doctype,
        long routeDocId,
        string menuId)
    {
        if (!Uri.TryCreate(portalBaseUrl, UriKind.Absolute, out var baseUri))
            baseUri = new Uri("https://ecp.sgcc.com.cn/ecp2.0/portal/");

        var builder = new UriBuilder(baseUri.Scheme, baseUri.Host)
        {
            Path = "/ecp2.0/portal/",
            Fragment = string.IsNullOrWhiteSpace(menuId)
                ? $"/doc/{doctype}/{routeDocId}"
                : $"/doc/{doctype}/{routeDocId}_{menuId}"
        };
        return builder.Uri.ToString();
    }

    private static void AppendJsonText(
        JsonElement element,
        string path,
        StringBuilder builder,
        int depth)
    {
        if (builder.Length >= MaxTextLength || depth > 8)
            return;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var nextPath = string.IsNullOrWhiteSpace(path)
                        ? property.Name
                        : $"{path}.{property.Name}";
                    AppendJsonText(property.Value, nextPath, builder, depth + 1);
                    if (builder.Length >= MaxTextLength)
                        break;
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    AppendJsonText(item, $"{path}[{index}]", builder, depth + 1);
                    index++;
                    if (index >= 50 || builder.Length >= MaxTextLength)
                        break;
                }

                break;
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                var value = GetElementString(element);
                if (!string.IsNullOrWhiteSpace(value))
                    AppendLine(builder, $"{path}: {value}");
                break;
        }
    }

    private static IReadOnlyList<StateGridAttachmentDocument> DiscoverAttachments(
        JsonElement element,
        string detailUrl)
    {
        if (!Uri.TryCreate(detailUrl, UriKind.Absolute, out var baseUri))
            baseUri = new Uri("https://ecp.sgcc.com.cn/ecp2.0/portal/");

        var results = new List<StateGridAttachmentDocument>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DiscoverAttachments(element, baseUri, results, seen, 0);
        return results;
    }

    private static void DiscoverAttachments(
        JsonElement element,
        Uri baseUri,
        List<StateGridAttachmentDocument> results,
        HashSet<string> seen,
        int depth)
    {
        if (depth > 8 || results.Count >= 100)
            return;

        if (element.ValueKind == JsonValueKind.Object)
        {
            TryAddAttachmentFromObject(element, baseUri, results, seen);
            foreach (var property in element.EnumerateObject())
            {
                DiscoverAttachments(property.Value, baseUri, results, seen, depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                DiscoverAttachments(item, baseUri, results, seen, depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            var value = GetElementString(element);
            if (LooksLikeAttachmentUrl(value))
            {
                AddAttachment(
                    baseUri,
                    results,
                    seen,
                    Path.GetFileName(value.Split('?', '#')[0]),
                    value,
                    null);
            }
        }
    }

    private static void TryAddAttachmentFromObject(
        JsonElement element,
        Uri baseUri,
        List<StateGridAttachmentDocument> results,
        HashSet<string> seen)
    {
        var url = FindFirstStringByNameHint(element, "url", "path", "fileid", "file_id", "fileurl", "file_url", "download");
        if (string.IsNullOrWhiteSpace(url) || !LooksLikeAttachmentUrl(url))
            return;

        var name = FindFirstStringByNameHint(element, "filename", "file_name", "name", "title", "docname", "doc_name", "attachname", "attach_name");
        var sizeText = FindFirstStringByNameHint(element, "filesize", "file_size", "size");
        var size = long.TryParse(sizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSize)
            ? parsedSize
            : (long?)null;

        AddAttachment(baseUri, results, seen, name, url, size);
    }

    private static void AddAttachment(
        Uri baseUri,
        List<StateGridAttachmentDocument> results,
        HashSet<string> seen,
        string? fileName,
        string fileUrl,
        long? fileSize)
    {
        if (!Uri.TryCreate(baseUri, fileUrl, out var uri))
            return;

        var normalizedUrl = uri.ToString();
        if (!seen.Add(normalizedUrl))
            return;

        var normalizedName = string.IsNullOrWhiteSpace(fileName)
            ? Path.GetFileName(uri.LocalPath)
            : fileName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "state-grid-attachment";

        results.Add(new StateGridAttachmentDocument(
            Trim(normalizedName, 500),
            normalizedUrl,
            DetectFileType(normalizedUrl),
            fileSize));
    }

    private static string? FindFirstStringByNameHint(JsonElement element, params string[] hints)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var property in element.EnumerateObject())
        {
            var normalized = NormalizeName(property.Name);
            if (hints.Any(hint => normalized.Contains(NormalizeName(hint), StringComparison.OrdinalIgnoreCase)))
            {
                var value = GetElementString(property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        return null;
    }

    private static bool LooksLikeAttachmentUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (AttachmentExtensionRegex().IsMatch(value) &&
            (value.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
             value.StartsWith("/", StringComparison.Ordinal) ||
             value.Contains("download", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("file", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return value.Contains("file", StringComparison.OrdinalIgnoreCase) &&
               (value.Contains("download", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("attach", StringComparison.OrdinalIgnoreCase));
    }

    private static string DetectFileType(string value)
    {
        var path = value.Split('?', '#')[0];
        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(extension))
            return extension;

        foreach (var known in new[] { "pdf", "docx", "doc", "xlsx", "xls", "zip", "rar", "txt", "html", "htm" })
        {
            if (value.Contains($".{known}", StringComparison.OrdinalIgnoreCase))
                return known;
        }

        return "file";
    }

    private static string NormalizeName(string value)
    {
        return value.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
    }

    private static string? FindFirstString(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var value = GetElementString(property.Value);
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }

                var nested = FindFirstString(property.Value, names);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindFirstString(item, names);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
        }

        return null;
    }

    private static DateTime? FindFirstDate(JsonElement element, params string[] names)
    {
        var value = FindFirstString(element, names);
        return TryParseDate(value);
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static string GetString(JsonElement element, string name)
    {
        return TryGetProperty(element, name, out var value)
            ? GetElementString(value)
            : string.Empty;
    }

    private static long GetLong(JsonElement element, string name)
    {
        return GetNullableLong(element, name) ?? 0;
    }

    private static long? GetNullableLong(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            return number;

        var text = GetElementString(value);
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static string GetElementString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static DateTime? TryParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value
            .Trim()
            .Replace('年', '-')
            .Replace('月', '-')
            .Replace("日", string.Empty);
        return DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }

    private static string BuildSyntheticHtml(string title, string text)
    {
        return $"<html><body><h1>{WebUtility.HtmlEncode(title)}</h1><pre>{WebUtility.HtmlEncode(text)}</pre></body></html>";
    }

    private static void AppendLine(StringBuilder builder, string value)
    {
        if (builder.Length >= MaxTextLength || string.IsNullOrWhiteSpace(value))
            return;

        builder.AppendLine(value.Trim());
    }

    private static string Trim(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    [GeneratedRegex("(?<!\\d)\\d{16}(?!\\d)")]
    private static partial Regex MenuIdRegex();

    [GeneratedRegex("\\.(pdf|doc|docx|xls|xlsx|zip|rar|txt|html?)(\\?|#|$)", RegexOptions.IgnoreCase)]
    private static partial Regex AttachmentExtensionRegex();
}
