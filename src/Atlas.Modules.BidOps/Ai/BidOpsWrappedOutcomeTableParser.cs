using System.Globalization;
using System.Text.RegularExpressions;
using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Entities.Outcomes;

namespace Atlas.Modules.BidOps.Ai;

public static class BidOpsWrappedOutcomeTableParser
{
    private static readonly Regex RowStartRegex = new(
        @"^\s*(?<seq>\d{1,4})\s+(?<tail>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PackageRegex = new(
        @"(?<package>(?:包|包件|分包|标包)\s*[A-Za-z0-9一二三四五六七八九十]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NumericRegex = new(
        @"(?<amount>\d{1,12}(?:,\d{3})*(?:\.\d*)?)(?![\d.,])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CodeFragmentRegex = new(
        @"^[A-Za-z0-9][A-Za-z0-9\-_/（）()]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<BidOpsOutcomeSupplierExtract> Extract(
        string? title,
        string? noticeType,
        string? text)
    {
        var source = $"{title}\n{text}";
        if (!BidOpsOutcomeSupplierTextParser.LooksLikeOutcomeNotice(title, noticeType, source))
            return [];

        var lines = SplitLines(source).ToList();
        if (lines.Count == 0)
            return [];

        var outcomeType = DetermineOutcomeType(title, noticeType, source);
        if (outcomeType == string.Empty)
            return [];

        var projectCode = BidOpsEvidenceText.ExtractProjectCode(source);
        var projectName = BidOpsEvidenceText.ExtractProjectName(source);
        var amountUnit = InferAmountUnit(lines);
        var results = new List<BidOpsOutcomeSupplierExtract>();
        var inOutcomeSection = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (LooksLikeOutcomeSectionStart(line))
                inOutcomeSection = true;

            if (!inOutcomeSection)
                continue;

            if (results.Count > 0 && LooksLikeOutcomeSectionBoundary(line))
                inOutcomeSection = false;

            if (!LooksLikeRowStart(line))
                continue;

            if (!TryParseRow(
                    lines,
                    i,
                    outcomeType,
                    projectCode,
                    projectName,
                    amountUnit,
                    out var record,
                    out var nextIndex))
            {
                continue;
            }

            results.Add(record);
            i = Math.Max(i, nextIndex - 1);
        }

        return results
            .Where(x => !string.IsNullOrWhiteSpace(x.SupplierName))
            .GroupBy(x => new
            {
                Supplier = BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(x.SupplierName),
                x.OutcomeType,
                x.Rank,
                Lot = NormalizeCode(x.LotNo),
                Package = NormalizeCode(x.PackageNo)
            })
            .Select(x => x.OrderByDescending(item => item.Confidence).First())
            .ToList();
    }

    private static bool TryParseRow(
        IReadOnlyList<string> lines,
        int startIndex,
        string outcomeType,
        string projectCode,
        string projectName,
        AmountUnitHint amountUnit,
        out BidOpsOutcomeSupplierExtract record,
        out int nextIndex)
    {
        record = new BidOpsOutcomeSupplierExtract();
        nextIndex = startIndex + 1;

        var start = RowStartRegex.Match(lines[startIndex]);
        if (!start.Success)
            return false;

        var buffer = new List<string> { start.Groups["tail"].Value };
        var endIndex = startIndex + 1;
        for (; endIndex < lines.Count && buffer.Count < 90; endIndex++)
        {
            var line = lines[endIndex];
            if (buffer.Count > 2 && LooksLikeRowStart(line) && BufferLooksComplete(buffer))
                break;

            if (BufferLooksComplete(buffer) && LooksLikeOutcomeSectionBoundary(line))
                break;

            buffer.Add(line);
        }

        nextIndex = endIndex;

        if (!TryLocatePackage(buffer, out var packageLineIndex, out var packageMatch))
            return false;

        var prePackageSegments = new List<string>();
        for (var i = 0; i < packageLineIndex; i++)
            prePackageSegments.Add(buffer[i]);

        var packageLine = buffer[packageLineIndex];
        var packagePrefix = packageLine[..packageMatch.Index];
        var packageNo = BidOpsTextQuality.CleanExtractedValue(packageMatch.Groups["package"].Value);
        if (!string.IsNullOrWhiteSpace(packagePrefix))
            prePackageSegments.Add(packagePrefix);

        var lotNo = BuildLotNo(prePackageSegments);
        var lotName = BuildLotName(prePackageSegments);
        var packageTail = packageLine[(packageMatch.Index + packageMatch.Length)..];

        var supplierFragments = new List<string>();
        if (!string.IsNullOrWhiteSpace(packageTail))
            supplierFragments.Add(packageTail);

        decimal? amount = null;
        var consumedAmount = false;
        for (var i = packageLineIndex + 1; i < buffer.Count; i++)
        {
            var line = buffer[i];
            if (supplierFragments.Count > 0 && LooksLikeStatusLine(line))
                break;

            if (TryReadAmount(line, buffer, i, amountUnit, out var beforeAmount, out amount, out var extraConsumed, out var isRate))
            {
                if (!string.IsNullOrWhiteSpace(beforeAmount))
                    supplierFragments.Add(beforeAmount);

                consumedAmount = true;
                i += extraConsumed;
                if (isRate)
                    amount = null;
                break;
            }

            supplierFragments.Add(line);
        }

        if (!consumedAmount)
            amount = null;

        var supplierName = BidOpsSupplierNameNormalizer.Clean(JoinFragments(supplierFragments));
        if (string.IsNullOrWhiteSpace(supplierName))
            return false;

        record = new BidOpsOutcomeSupplierExtract
        {
            SupplierName = supplierName,
            OutcomeType = outcomeType,
            Rank = outcomeType == BidOpsOutcomeTypes.Candidate ? ExtractRank(buffer) ?? 1 : ExtractRank(buffer),
            AwardAmount = amount,
            ProjectCode = projectCode,
            ProjectName = projectName,
            LotNo = lotNo,
            LotName = lotName,
            PackageNo = packageNo,
            EvidenceText = Truncate(string.Join(" ", buffer.Take(24)), 1000),
            Confidence = amount.HasValue ? 0.92m : 0.86m
        };
        return true;
    }

    private static bool BufferLooksComplete(IReadOnlyList<string> buffer)
    {
        if (!TryLocatePackage(buffer, out _, out _))
            return false;

        var compact = JoinFragments(buffer);
        return !string.IsNullOrWhiteSpace(BidOpsSupplierNameNormalizer.Clean(compact));
    }

    private static bool TryLocatePackage(
        IReadOnlyList<string> buffer,
        out int packageLineIndex,
        out Match packageMatch)
    {
        for (var i = 0; i < buffer.Count; i++)
        {
            var match = PackageRegex.Match(buffer[i]);
            if (!match.Success)
                continue;

            packageLineIndex = i;
            packageMatch = match;
            return true;
        }

        packageLineIndex = -1;
        packageMatch = Match.Empty;
        return false;
    }

    private static string BuildLotNo(IReadOnlyList<string> segments)
    {
        var parts = segments
            .Select(CleanSegment)
            .Where(x => CodeFragmentRegex.IsMatch(x) && x.Any(char.IsDigit))
            .ToList();
        return Truncate(string.Concat(parts), 128);
    }

    private static string BuildLotName(IReadOnlyList<string> segments)
    {
        var parts = segments
            .Select(CleanSegment)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !(CodeFragmentRegex.IsMatch(x) && x.Any(char.IsDigit)))
            .ToList();
        return Truncate(JoinFragments(parts), 300);
    }

    private static bool TryReadAmount(
        string line,
        IReadOnlyList<string> buffer,
        int index,
        AmountUnitHint amountUnit,
        out string beforeAmount,
        out decimal? amount,
        out int extraConsumed,
        out bool isRate)
    {
        beforeAmount = string.Empty;
        amount = null;
        extraConsumed = 0;
        isRate = false;

        var match = NumericRegex.Match(line);
        if (!match.Success)
            return false;

        beforeAmount = line[..match.Index].Trim();
        var rawAmount = match.Groups["amount"].Value.Replace(",", string.Empty, StringComparison.Ordinal);
        var afterAmount = line[(match.Index + match.Length)..];

        if (LooksLikePercent(afterAmount))
        {
            isRate = true;
            return true;
        }

        if (index + 1 < buffer.Count)
        {
            var next = CleanSegment(buffer[index + 1]);
            if (LooksLikePercent(next))
            {
                isRate = true;
                extraConsumed = 1;
                return true;
            }

            if (LooksLikeNumericContinuation(rawAmount, next))
            {
                rawAmount += next;
                extraConsumed = 1;
            }
        }

        if (!decimal.TryParse(rawAmount.TrimEnd('.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            return false;

        if (amountUnit == AmountUnitHint.TenThousandYuan || ContainsAny(line, "万元", "万"))
            parsed *= 10_000m;

        amount = Math.Round(parsed, 2);
        return true;
    }

    private static bool LooksLikeNumericContinuation(string currentAmount, string nextLine)
    {
        if (string.IsNullOrWhiteSpace(nextLine))
            return false;

        if (!nextLine.All(char.IsDigit))
            return false;

        return currentAmount.Contains(".", StringComparison.Ordinal) ||
               currentAmount.EndsWith(".", StringComparison.Ordinal);
    }

    private static int? ExtractRank(IReadOnlyList<string> buffer)
    {
        var compact = JoinFragments(buffer);
        if (ContainsAny(compact, "第一名", "排序第一", "综合排序第一"))
            return 1;
        if (ContainsAny(compact, "第二名", "排序第二", "综合排序第二"))
            return 2;
        if (ContainsAny(compact, "第三名", "排序第三", "综合排序第三"))
            return 3;

        var match = Regex.Match(compact, @"第?(?<rank>\d{1,2})(?:名|位)", RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups["rank"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)
            ? rank
            : null;
    }

    private static bool LooksLikeRowStart(string line)
    {
        var match = RowStartRegex.Match(line);
        if (!match.Success)
            return false;

        var tail = match.Groups["tail"].Value;
        if (ContainsAny(tail, "年", "月", "日", "联系电话", "联系地址"))
            return false;

        return Regex.IsMatch(tail, @"[A-Za-z0-9]{2,}[-_/]", RegexOptions.CultureInvariant) ||
               PackageRegex.IsMatch(tail);
    }

    private static bool LooksLikeOutcomeSectionStart(string line)
    {
        var compact = NormalizeHeaderText(line);
        return ContainsAny(
            compact,
            "推荐中标候选人",
            "推荐成交候选人",
            "中标候选人",
            "成交候选人",
            "推荐中标人",
            "推荐成交人",
            "中标人",
            "成交人",
            "成交供应商",
            "中标供应商",
            "投标总价",
            "报价");
    }

    private static bool LooksLikeOutcomeSectionBoundary(string line)
    {
        var compact = NormalizeHeaderText(line);
        return ContainsAny(
            compact,
            "否决原因",
            "否决投标",
            "被否决",
            "流标原因",
            "公示期",
            "投标人对以上结果",
            "如有异议",
            "招标代理机构",
            "采购代理机构",
            "联系电话",
            "联系邮箱");
    }

    private static bool LooksLikeStatusLine(string line)
    {
        var compact = NormalizeHeaderText(line);
        return ContainsAny(
            compact,
            "满足招",
            "标文件要求",
            "采购文件要求",
            "综合排序",
            "综合排",
            "评审情况",
            "评标情况",
            "一级建造师",
            "二级建造师",
            "注册证书",
            "证书编号");
    }

    private static string DetermineOutcomeType(string? title, string? noticeType, string source)
    {
        var signal = $"{title}\n{noticeType}\n{source}";
        if (ContainsAny(signal, "CandidateAnnouncement", "中标候选人", "成交候选人", "候选人公示", "推荐的中标", "推荐的成交"))
            return BidOpsOutcomeTypes.Candidate;

        if (ContainsAny(signal, "AwardAnnouncement", "ResultAnnouncement", "中标结果", "成交结果", "中标公告", "成交公告"))
            return BidOpsOutcomeTypes.Awarded;

        return string.Empty;
    }

    private static AmountUnitHint InferAmountUnit(IReadOnlyList<string> lines)
    {
        var header = NormalizeHeaderText(string.Join(' ', lines.Take(80)));
        if (ContainsAny(header, "万元", "万人民币", "人民币万元"))
            return AmountUnitHint.TenThousandYuan;

        if (ContainsAny(header, "元", "人民币元"))
            return AmountUnitHint.Yuan;

        return AmountUnitHint.Unknown;
    }

    private static IEnumerable<string> SplitLines(string source)
    {
        return source
            .Replace('\t', ' ')
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(BidOpsTextQuality.CleanExtractedValue)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string JoinFragments(IEnumerable<string> fragments)
    {
        return string.Concat(fragments.Select(CleanSegment))
            .Trim(' ', ':', '：', '=', '-', '、', ',', '，', '.', '。', '；', ';');
    }

    private static string CleanSegment(string? value)
    {
        return BidOpsTextQuality.CleanExtractedValue(value)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("　", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static bool LooksLikePercent(string value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return cleaned.StartsWith('%') ||
               cleaned.StartsWith('％') ||
               cleaned.Contains("折扣", StringComparison.Ordinal) ||
               cleaned.Contains("费率", StringComparison.Ordinal);
    }

    private static string NormalizeHeaderText(string value)
    {
        return string.Concat(BidOpsTextQuality.CleanExtractedValue(value).Where(x => !char.IsWhiteSpace(x)));
    }

    private static string NormalizeCode(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return string.Concat(cleaned.Where(x => !char.IsWhiteSpace(x) && !":：,，;；".Contains(x))).ToUpperInvariant();
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

    private static string Truncate(string? value, int maxLength)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private enum AmountUnitHint
    {
        Unknown,
        Yuan,
        TenThousandYuan
    }
}
