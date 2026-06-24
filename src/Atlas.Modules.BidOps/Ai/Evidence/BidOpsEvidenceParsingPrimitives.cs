using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Atlas.Modules.BidOps.Ai.Evidence;

public static partial class BidOpsPackageNoNormalizer
{
    public static string Normalize(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        cleaned = cleaned
            .Replace("分包编号", string.Empty, StringComparison.Ordinal)
            .Replace("包件编号", string.Empty, StringComparison.Ordinal)
            .Replace("标包编号", string.Empty, StringComparison.Ordinal)
            .Replace("采购包编号", string.Empty, StringComparison.Ordinal)
            .Replace("分包号", string.Empty, StringComparison.Ordinal)
            .Replace("包件号", string.Empty, StringComparison.Ordinal)
            .Replace("标包号", string.Empty, StringComparison.Ordinal)
            .Replace("采购包", string.Empty, StringComparison.Ordinal)
            .Replace("分包", string.Empty, StringComparison.Ordinal)
            .Replace("标包", string.Empty, StringComparison.Ordinal)
            .Replace("包号", string.Empty, StringComparison.Ordinal)
            .Replace("第", string.Empty, StringComparison.Ordinal)
            .Replace("包", string.Empty, StringComparison.Ordinal)
            .Replace("号", string.Empty, StringComparison.Ordinal)
            .Trim(' ', ':', '：', '-', '_', '/', '\\', '#', '（', '）', '(', ')');

        var digitMatch = DigitRegex().Match(cleaned);
        if (digitMatch.Success)
        {
            var digits = digitMatch.Value.TrimStart('0');
            return string.IsNullOrWhiteSpace(digits) ? "0" : digits;
        }

        var chinese = new string(cleaned.Where(IsChineseNumberChar).ToArray());
        if (string.IsNullOrWhiteSpace(chinese))
            return cleaned.Trim();

        var number = ParseChineseNumber(chinese);
        return number > 0 ? number.ToString(CultureInfo.InvariantCulture) : cleaned.Trim();
    }

    private static bool IsChineseNumberChar(char ch)
    {
        return ch is '零' or '〇' or '一' or '二' or '两' or '三' or '四' or '五' or '六' or '七' or '八' or '九' or '十';
    }

    private static int ParseChineseNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        if (!value.Contains('十', StringComparison.Ordinal))
            return ChineseDigit(value[0]);

        var parts = value.Split('十');
        var tens = string.IsNullOrWhiteSpace(parts[0]) ? 1 : ChineseDigit(parts[0][0]);
        var ones = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? ChineseDigit(parts[1][0]) : 0;
        return tens * 10 + ones;
    }

    private static int ChineseDigit(char ch)
    {
        return ch switch
        {
            '一' => 1,
            '二' or '两' => 2,
            '三' => 3,
            '四' => 4,
            '五' => 5,
            '六' => 6,
            '七' => 7,
            '八' => 8,
            '九' => 9,
            _ => 0
        };
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitRegex();
}

public static partial class BidOpsMoneyNormalizer
{
    public static decimal? TryNormalize(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return null;

        if (LooksLikeRateOrScore(cleaned))
            return null;

        var match = AmountRegex().Match(cleaned.Replace(",", string.Empty, StringComparison.Ordinal));
        if (!match.Success ||
            !decimal.TryParse(match.Groups["amount"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        var unit = match.Groups["unit"].Value;
        if (unit is "万元" or "万")
            amount *= 10_000m;

        return Math.Round(amount, 2);
    }

    private static bool LooksLikeRateOrScore(string value)
    {
        return value.Contains('%', StringComparison.Ordinal) ||
               value.Contains('％', StringComparison.Ordinal) ||
               value.Contains("百分比", StringComparison.Ordinal) ||
               value.Contains("费率", StringComparison.Ordinal) ||
               value.Contains("折扣率", StringComparison.Ordinal) ||
               value.Contains("折扣", StringComparison.Ordinal) ||
               value.Contains("评分", StringComparison.Ordinal) ||
               value.Contains("得分", StringComparison.Ordinal) ||
               Regex.IsMatch(value, @"\d+(?:\.\d+)?\s*分(?:\s|$)", RegexOptions.CultureInvariant);
    }

    [GeneratedRegex(@"(?<amount>\d+(?:\.\d+)?)(?:\s*)(?<unit>万元|万|元)?", RegexOptions.CultureInvariant)]
    private static partial Regex AmountRegex();
}

public static partial class BidOpsSupplierNameNormalizer
{
    private static readonly string[] Labels =
    [
        "第一中标候选人", "第二中标候选人", "第三中标候选人",
        "第一成交候选人", "第二成交候选人", "第三成交候选人",
        "第一推荐候选人", "第二推荐候选人", "第三推荐候选人",
        "中标候选人", "成交候选人", "推荐候选人",
        "中标供应商", "成交供应商", "中标单位", "成交单位",
        "中标人", "成交人", "应答人", "投标人", "供应商名称", "供应商", "单位名称", "名称"
    ];

    public static string Clean(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        cleaned = RankPrefixRegex().Replace(cleaned, string.Empty);
        foreach (var label in Labels)
            cleaned = Regex.Replace(cleaned, $"^{Regex.Escape(label)}\\s*[:：=]?\\s*", string.Empty, RegexOptions.CultureInvariant);

        cleaned = cleaned.Trim(' ', ':', '：', '=', '-', '、', ',', '，', '.', '。', '；', ';');
        var companyMatch = OrganizationRegex().Match(cleaned);
        if (companyMatch.Success)
            cleaned = companyMatch.Groups["name"].Value;

        return cleaned.Trim(' ', ':', '：', '=', '-', '、', ',', '，', '.', '。', '；', ';');
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

    [GeneratedRegex(@"^第?[一二三四五六七八九十\d]+(?:名|位)?", RegexOptions.CultureInvariant)]
    private static partial Regex RankPrefixRegex();

    [GeneratedRegex(@"(?<name>[\u4e00-\u9fa5A-Za-z0-9（）()·\-\s]{2,120}(?:有限责任公司|股份有限公司|集团有限公司|有限公司|分公司|集团|公司|工厂|厂|勘测设计研究院|工程设计有限公司|研究院|设计院|测绘院|勘测院|勘察院|规划院|科学院|检验院|检测院|计量院|研究所|事务所|大学|学院|学校|医院|中心))", RegexOptions.CultureInvariant)]
    private static partial Regex OrganizationRegex();
}

internal static partial class BidOpsEvidenceText
{
    public static string ExtractProjectCode(string? text)
    {
        return TrimProjectCode(ExtractFirst(
            text,
            @"(?:项目编号|项目编码|项目代码|采购编号|招标编号|批次编号|采购项目编号|招标项目编号|项目采购编号|招标采购编号|PURPRJ_CODE|PROJECT_CODE|ProjectCode|BID_BATCH_CODE)[^\S\r\n]*(?:[（(][^）)]{1,40}[）)])?[^\S\r\n]*[:：=][^\S\r\n]*(?<value>[A-Za-z0-9_.\-/（）()]{3,100})"));
    }

    public static string ExtractLotNo(string? text)
    {
        return TrimCode(ExtractFirst(
            text,
            @"(?:分标编号|标段编号|标段号|分标号|分包编号|标包编号|包件编号)\s*(?:[（(][^）)]{1,40}[）)])?\s*[:：=]\s*(?<value>[A-Za-z0-9_.\-/（）()]{2,100})"));
    }

    public static string ExtractLotName(string? text)
    {
        return ExtractFirst(
            text,
            @"(?:分标名称|标段名称|分标名|标段名)\s*(?:[（(][^）)]{1,40}[）)])?\s*[:：=]\s*(?<value>[^\r\n。；;]{2,200})");
    }

    public static string ExtractProjectName(string? text)
    {
        return ExtractFirst(
            text,
            @"(?:项目名称|采购项目名称|招标项目名称|工程名称|PURPRJ_NAME|PROJECT_NAME)\s*[:：=]\s*(?<value>[^\r\n。；;]{2,200})");
    }

    public static int? ParseRank(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return null;

        if (cleaned.Contains("第一", StringComparison.Ordinal) || cleaned.Contains("第1", StringComparison.Ordinal))
            return 1;
        if (cleaned.Contains("第二", StringComparison.Ordinal) || cleaned.Contains("第2", StringComparison.Ordinal))
            return 2;
        if (cleaned.Contains("第三", StringComparison.Ordinal) || cleaned.Contains("第3", StringComparison.Ordinal))
            return 3;

        var match = RankRegex().Match(cleaned);
        if (!match.Success)
            return null;

        return int.TryParse(match.Groups["rank"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)
            ? rank
            : null;
    }

    public static double Similarity(string? left, string? right)
    {
        var a = NormalizeForSimilarity(left);
        var b = NormalizeForSimilarity(right);
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return 0d;
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return 1d;
        if (a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase))
            return 0.88d;

        var overlap = a.Intersect(b).Count();
        var denominator = Math.Max(a.Length, b.Length);
        return denominator == 0 ? 0d : (double)overlap / denominator;
    }

    private static string ExtractFirst(string? text, string pattern)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        foreach (var candidate in new[] { text, ToPlainText(text) }.Distinct(StringComparer.Ordinal))
        {
            var match = Regex.Match(candidate, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
                return BidOpsTextQuality.CleanExtractedValue(match.Groups["value"].Value);
        }

        return string.Empty;
    }

    private static string NormalizeForSimilarity(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return new string(cleaned.Where(x => !char.IsWhiteSpace(x) && !char.IsPunctuation(x)).ToArray());
    }

    private static string TrimProjectCode(string value)
    {
        return TrimCode(value);
    }

    private static string TrimCode(string value)
    {
        return BidOpsTextQuality.CleanExtractedValue(value)
            .Trim(' ', '\t', '。', '.', '；', ';', '，', ',', '、', '）', ')');
    }

    public static string ToPlainText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decoded = WebUtility.HtmlDecode(value);
        decoded = Regex.Replace(decoded, @"</?(?:p|div|br|li|tr|table|tbody|thead|tfoot)\b[^>]*>", "\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        decoded = Regex.Replace(decoded, @"<[^>]+>", string.Empty, RegexOptions.CultureInvariant);
        decoded = Regex.Replace(decoded, @"[^\S\r\n]+", " ", RegexOptions.CultureInvariant);
        decoded = Regex.Replace(decoded, @"\n{2,}", "\n", RegexOptions.CultureInvariant);
        return decoded.Trim();
    }

    [GeneratedRegex(@"(?<rank>\d{1,2})", RegexOptions.CultureInvariant)]
    private static partial Regex RankRegex();
}

internal sealed record BidOpsExtractedTable(
    int TableIndex,
    IReadOnlyList<string> Headers,
    IReadOnlyList<BidOpsExtractedRow> Rows)
{
    public string ContextText { get; init; } = string.Empty;
}

internal sealed record BidOpsExtractedRow(
    int RowIndex,
    IReadOnlyList<string> Cells,
    string RawText);

internal static class BidOpsEvidenceTableParser
{
    public static IReadOnlyList<BidOpsExtractedTable> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var tables = new List<BidOpsExtractedTable>();
        var tableIndex = 0;
        ExtractHtmlTables(text, tables, ref tableIndex);

        for (var i = 0; i < lines.Length; i++)
        {
            if (IsPipeRow(lines[i]))
            {
                var header = SplitPipeRow(lines[i]);
                var rows = new List<BidOpsExtractedRow>();
                var cursor = i + 1;
                if (cursor < lines.Length && IsMarkdownSeparator(lines[cursor]))
                    cursor++;

                var rowIndex = 1;
                while (cursor < lines.Length && IsPipeRow(lines[cursor]))
                {
                    var cells = SplitPipeRow(lines[cursor]);
                    if (cells.Any(x => !string.IsNullOrWhiteSpace(x)))
                        rows.Add(new BidOpsExtractedRow(rowIndex++, PadCells(cells, header.Count), lines[cursor].Trim()));
                    cursor++;
                }

                if (header.Count > 1 && rows.Count > 0)
                {
                    tables.Add(new BidOpsExtractedTable(tableIndex++, header, rows)
                    {
                        ContextText = BuildLineContext(lines, i)
                    });
                    i = cursor - 1;
                    continue;
                }
            }

            if (LooksLikeWhitespaceHeader(lines[i]))
            {
                var header = SplitWhitespaceRow(lines[i]);
                var rows = new List<BidOpsExtractedRow>();
                var cursor = i + 1;
                var rowIndex = 1;
                while (cursor < lines.Length && !string.IsNullOrWhiteSpace(lines[cursor]))
                {
                    if (LooksLikeWhitespaceHeader(lines[cursor]) && rows.Count > 0)
                        break;

                    var cells = SplitWhitespaceRow(lines[cursor]);
                    if (cells.Count > 1 && cells.Any(x => !string.IsNullOrWhiteSpace(x)))
                        rows.Add(new BidOpsExtractedRow(rowIndex++, PadCells(cells, header.Count), lines[cursor].Trim()));
                    cursor++;
                }

                if (header.Count > 1 && rows.Count > 0)
                {
                    tables.Add(new BidOpsExtractedTable(tableIndex++, header, rows)
                    {
                        ContextText = BuildLineContext(lines, i)
                    });
                    i = cursor - 1;
                }
            }
        }

        return tables;
    }

    private static void ExtractHtmlTables(
        string text,
        List<BidOpsExtractedTable> tables,
        ref int tableIndex)
    {
        foreach (Match tableMatch in Regex.Matches(text, @"<table\b[^>]*>(?<body>.*?)</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant))
        {
            var htmlRows = new List<(IReadOnlyList<string> Cells, bool IsHeader, string RawText)>();
            foreach (Match rowMatch in Regex.Matches(tableMatch.Groups["body"].Value, @"<tr\b[^>]*>(?<row>.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant))
            {
                var cells = new List<string>();
                var hasHeaderCell = false;
                foreach (Match cellMatch in Regex.Matches(rowMatch.Groups["row"].Value, @"<(?<tag>t[hd])\b[^>]*>(?<cell>.*?)</t[hd]>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant))
                {
                    hasHeaderCell |= string.Equals(cellMatch.Groups["tag"].Value, "th", StringComparison.OrdinalIgnoreCase);
                    var cell = HtmlCellToText(cellMatch.Groups["cell"].Value);
                    cells.Add(cell);
                }

                if (cells.Count <= 1 || cells.All(string.IsNullOrWhiteSpace))
                    continue;

                htmlRows.Add((cells, hasHeaderCell, string.Join(" | ", cells)));
            }

            if (htmlRows.Count < 2)
                continue;

            var headerIndex = htmlRows.FindIndex(x => x.IsHeader || HeaderSignalCount(x.Cells) >= 2);
            if (headerIndex < 0 || headerIndex >= htmlRows.Count - 1)
                continue;

            var header = htmlRows[headerIndex].Cells;
            var rows = new List<BidOpsExtractedRow>();
            var rowIndex = 1;
            foreach (var row in htmlRows.Skip(headerIndex + 1))
            {
                if (HeaderSignalCount(row.Cells) >= 2)
                    break;

                rows.Add(new BidOpsExtractedRow(rowIndex++, PadCells(row.Cells, header.Count), row.RawText));
            }

            if (rows.Count > 0)
            {
                tables.Add(new BidOpsExtractedTable(tableIndex++, header, rows)
                {
                    ContextText = BuildTextContext(text, tableMatch.Index)
                });
            }
        }
    }

    private static string HtmlCellToText(string html)
    {
        var decoded = WebUtility.HtmlDecode(html);
        decoded = Regex.Replace(decoded, @"</?(?:p|div|br|li)\b[^>]*>", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        decoded = Regex.Replace(decoded, @"<[^>]+>", string.Empty, RegexOptions.CultureInvariant);
        return BidOpsTextQuality.CleanExtractedValue(decoded);
    }

    public static int FindColumn(IReadOnlyList<string> headers, params string[] aliases)
    {
        var normalizedAliases = aliases.Select(NormalizeHeader).ToArray();
        for (var i = 0; i < headers.Count; i++)
        {
            var header = NormalizeHeader(headers[i]);
            if (normalizedAliases.Any(alias => header.Contains(alias, StringComparison.OrdinalIgnoreCase)))
                return i;
        }

        return -1;
    }

    public static string GetCell(BidOpsExtractedRow row, int index)
    {
        return index >= 0 && index < row.Cells.Count
            ? row.Cells[index].Trim()
            : string.Empty;
    }

    public static string NormalizeHeader(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch) ||
                ch is '|' or '/' or '／' or ':' or '：' or '-' or '_' or '（' or '）' or '(' or ')' or ',' or '，')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    private static bool IsPipeRow(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Count(x => x == '|') >= 2;
    }

    private static bool IsMarkdownSeparator(string line)
    {
        if (!IsPipeRow(line))
            return false;

        var cells = SplitPipeRow(line);
        return cells.Count > 0 && cells.All(cell => cell.All(ch => ch is '-' or ':' or ' '));
    }

    private static IReadOnlyList<string> SplitPipeRow(string line)
    {
        return line
            .Trim()
            .Trim('|')
            .Split('|')
            .Select(x => x.Trim())
            .ToList();
    }

    private static bool LooksLikeWhitespaceHeader(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.Any(char.IsWhiteSpace))
            return false;

        return HeaderSignalCount(SplitWhitespaceRow(line)) >= 2;
    }

    private static int HeaderSignalCount(IReadOnlyList<string> cells)
    {
        var normalized = NormalizeHeader(string.Join(' ', cells));
        return new[]
        {
            "项目编号", "项目名称", "项目单位", "分标编号", "分标名称", "包号", "包名称",
            "中标人", "成交人", "中标状态", "推荐候选人", "应答人", "投标人名称",
            "最终报价", "报价", "预算金额", "最高限价", "采购范围", "资质要求"
        }.Count(alias => normalized.Contains(NormalizeHeader(alias), StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> SplitWhitespaceRow(string line)
    {
        var parts = Regex.Split(line.Trim(), @"\s{2,}|\t+", RegexOptions.CultureInvariant)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();
        if (parts.Count > 1)
            return parts;

        return Regex.Split(line.Trim(), @"\s+", RegexOptions.CultureInvariant)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();
    }

    private static IReadOnlyList<string> PadCells(IReadOnlyList<string> cells, int count)
    {
        if (cells.Count >= count)
            return cells;

        var result = cells.ToList();
        while (result.Count < count)
            result.Add(string.Empty);
        return result;
    }

    private static string BuildLineContext(IReadOnlyList<string> lines, int tableStartLine)
    {
        var start = Math.Max(0, tableStartLine - 40);
        return string.Join('\n', lines.Skip(start).Take(tableStartLine - start));
    }

    private static string BuildTextContext(string text, int tableStartIndex)
    {
        const int maxContextLength = 8000;
        var start = Math.Max(0, tableStartIndex - maxContextLength);
        return BidOpsEvidenceText.ToPlainText(text[start..tableStartIndex]);
    }
}
