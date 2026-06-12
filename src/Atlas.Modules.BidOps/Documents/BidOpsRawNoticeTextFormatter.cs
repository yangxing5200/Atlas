using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Atlas.Modules.BidOps.Documents;

public static partial class BidOpsRawNoticeTextFormatter
{
    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourceUrl"] = "原始公告地址",
            ["NoticeId"] = "公告编号",
            ["FirstPageDocId"] = "首页文档编号",
            ["Doctype"] = "公告类型",
            ["MenuId"] = "栏目编号",
            ["PublishOrgName"] = "发布单位",
            ["ProjectCode"] = "项目编码",
            ["ListPublishTime"] = "列表发布时间",
            ["PublishTime"] = "发布时间",
            ["fileFlag"] = "是否有附件",
            ["TITLE"] = "公告标题",
            ["NOTICE_TITLE"] = "公告标题",
            ["PURPRJ_NAME"] = "项目名称",
            ["PROJECT_NAME"] = "项目名称",
            ["PURPRJ_CODE"] = "项目编码",
            ["PROJECT_CODE"] = "项目编码",
            ["CODE"] = "项目编码",
            ["PUBLISH_ORG_NAME"] = "发布单位",
            ["ORG_NAME"] = "发布单位",
            ["BID_ORG"] = "采购人",
            ["bidOrgName"] = "采购人",
            ["BUYER_NAME"] = "采购人",
            ["PURCHASER_NAME"] = "采购人",
            ["BID_AGT"] = "代理机构",
            ["BID_AGT_NAME"] = "代理机构",
            ["BIDAGT_NAME"] = "代理机构",
            ["bidagtName"] = "代理机构",
            ["AGENCY_NAME"] = "代理机构",
            ["PUR_TYPE_NAME"] = "采购类型",
            ["NOTICE_TYPE_NAME"] = "公告类型",
            ["PUB_TIME"] = "发布时间",
            ["PUBLISH_TIME"] = "发布时间",
            ["NOTICE_PUBLISH_TIME"] = "发布时间",
            ["UPD_TIME"] = "更新时间",
            ["OPENBID_TIME"] = "开标时间",
            ["BID_OPEN_TIME"] = "开标时间",
            ["BIDBOOK_BUY_END_TIME"] = "采购文件获取截止时间",
            ["BID_END_TIME"] = "投标截止时间",
            ["SIGNUP_END_TIME"] = "报名截止时间",
            ["CONT"] = "公告内容",
            ["NOTICE_CONT"] = "公告内容",
            ["BID_NOTICE_CONT"] = "公告内容",
            ["CHG_NOTICE_CONT"] = "变更内容",
            ["WASTE_NOTICE_CONT"] = "废标内容",
            ["FILE_NAME"] = "附件名称",
            ["fileName"] = "附件名称",
            ["FILE_SIZE"] = "附件大小"
        };

    private static readonly IReadOnlySet<string> HiddenKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ORG_NAME_SM4",
            "PURPRJ_NOTICE_ID",
            "PURPRJ_NOTICE_TYPE",
            "BIDAGT_ID",
            "BID_ORG_ID",
            "FILE_PATH",
            "FILE_E_SIGN_PATH",
            "FILE_E_SIGN_NAME",
            "PURPRJ_NOTICE_ATTACH_ID"
        };

    private static readonly IReadOnlyDictionary<string, string> DoctypeLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["doci-bid"] = "招标/采购公告",
            ["doci-change"] = "变更公告",
            ["doci-delay"] = "延期公告",
            ["doci-waste"] = "废标公告",
            ["doci-win"] = "中标/成交结果公告"
        };

    public static string ToDisplayText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder();
        var seenFacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var leadingTitle = string.Empty;

        foreach (var rawLine in NormalizeNewLines(value).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                AppendBlankLine(builder);
                continue;
            }

            if (TryParseKeyValue(line, out var rawKey, out var rawRawValue))
            {
                var normalizedKey = NormalizeKey(rawKey);
                var cleanValue = CleanValue(rawRawValue);
                if (ShouldHideKey(normalizedKey, cleanValue))
                    continue;

                if (string.IsNullOrWhiteSpace(cleanValue))
                    continue;

                cleanValue = FormatKnownValue(normalizedKey, cleanValue);
                if (!TryGetLabel(rawKey, normalizedKey, out var label))
                {
                    if (!LooksLikePublicText(cleanValue))
                        continue;

                    label = "公开信息";
                }

                if (IsTitleKey(normalizedKey) &&
                    !string.IsNullOrWhiteSpace(leadingTitle) &&
                    string.Equals(cleanValue, leadingTitle, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var factKey = $"{label}\t{cleanValue}";
                if (!seenFacts.Add(factKey))
                    continue;

                AppendLine(builder, FormatFact(label, cleanValue));
                continue;
            }

            var cleanedLine = CleanValue(line);
            if (string.IsNullOrWhiteSpace(cleanedLine))
                continue;

            if (string.IsNullOrWhiteSpace(leadingTitle))
                leadingTitle = cleanedLine;

            AppendLine(builder, cleanedLine);
        }

        return NormalizeDisplayResult(builder.ToString());
    }

    private static bool TryParseKeyValue(
        string line,
        out string key,
        out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var match = KeyValueRegex().Match(line);
        if (!match.Success)
            return false;

        key = match.Groups["key"].Value.Trim();
        value = match.Groups["value"].Value.Trim();
        return key.Length > 0;
    }

    private static bool TryGetLabel(
        string rawKey,
        string normalizedKey,
        out string label)
    {
        if (Labels.TryGetValue(rawKey, out label!))
            return true;

        return Labels.TryGetValue(normalizedKey, out label!);
    }

    private static string NormalizeKey(string rawKey)
    {
        var key = rawKey.Trim();
        var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length > 0)
            key = segments[^1];

        key = ArrayIndexRegex().Replace(key, string.Empty);
        return key;
    }

    private static bool ShouldHideKey(string normalizedKey, string cleanValue)
    {
        if (HiddenKeys.Contains(normalizedKey))
            return true;

        if (normalizedKey.EndsWith("_ID", StringComparison.OrdinalIgnoreCase) &&
            !Labels.ContainsKey(normalizedKey))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(cleanValue);
    }

    private static string FormatKnownValue(string normalizedKey, string value)
    {
        if (normalizedKey.Equals("Doctype", StringComparison.OrdinalIgnoreCase) &&
            DoctypeLabels.TryGetValue(value, out var doctypeLabel))
        {
            return doctypeLabel;
        }

        if (normalizedKey.Equals("fileFlag", StringComparison.OrdinalIgnoreCase))
        {
            return value.Trim() switch
            {
                "1" => "是",
                "0" => "否",
                "true" => "是",
                "false" => "否",
                _ => value
            };
        }

        return value;
    }

    private static bool IsTitleKey(string key)
    {
        return key.Equals("TITLE", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("NOTICE_TITLE", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("PURPRJ_NAME", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("PROJECT_NAME", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePublicText(string value)
    {
        return value.Any(ch => ch >= '\u4e00' && ch <= '\u9fff') ||
               value.Any(char.IsLetter);
    }

    private static string CleanValue(string value)
    {
        var decoded = WebUtility.HtmlDecode(value)
            .Replace("\u00a0", " ", StringComparison.Ordinal);

        decoded = HtmlBreakRegex().Replace(decoded, "\n");
        decoded = HtmlTagRegex().Replace(decoded, string.Empty);
        decoded = NormalizeNewLines(decoded);
        decoded = HorizontalWhitespaceRegex().Replace(decoded, " ");

        var lines = decoded
            .Split('\n')
            .Select(x => x.Trim())
            .Where(x => x.Length > 0);

        return string.Join('\n', lines).Trim();
    }

    private static string FormatFact(string label, string value)
    {
        return value.Contains('\n', StringComparison.Ordinal)
            ? $"{label}：\n{value}"
            : $"{label}：{value}";
    }

    private static void AppendLine(StringBuilder builder, string value)
    {
        if (builder.Length > 0 && builder[^1] != '\n')
            builder.AppendLine();

        builder.AppendLine(value);
    }

    private static void AppendBlankLine(StringBuilder builder)
    {
        if (builder.Length == 0)
            return;

        var text = builder.ToString();
        if (!text.EndsWith("\n\n", StringComparison.Ordinal))
            builder.AppendLine();
    }

    private static string NormalizeNewLines(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static string NormalizeDisplayResult(string value)
    {
        var builder = new StringBuilder();
        var previousBlank = true;
        foreach (var rawLine in NormalizeNewLines(value).Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0)
            {
                if (!previousBlank)
                    builder.AppendLine();
                previousBlank = true;
                continue;
            }

            builder.AppendLine(line);
            previousBlank = false;
        }

        return builder.ToString().Trim();
    }

    [GeneratedRegex("^(?<key>[A-Za-z][A-Za-z0-9_.\\[\\]]*)\\s*[:：]\\s*(?<value>.*)$")]
    private static partial Regex KeyValueRegex();

    [GeneratedRegex("\\[\\d+\\]")]
    private static partial Regex ArrayIndexRegex();

    [GeneratedRegex("<\\s*(br|/p|/div|/li|/tr|/h[1-6])\\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlBreakRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("[\\t\\f\\v ]+")]
    private static partial Regex HorizontalWhitespaceRegex();
}
