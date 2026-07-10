using System.Text.RegularExpressions;
using Atlas.Modules.BidOps.Entities.Outcomes;

namespace Atlas.Modules.BidOps.Ai;

public static class BidOpsPdfTableOutcomeParser
{
    private static readonly Regex PackageRegex = new(
        @"(?<package>(?:包|包件|分包|标包)\s*[A-Za-z0-9一二三四五六七八九十]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SupplierRegex = new(
        @"(?<name>[\u4e00-\u9fa5A-Za-z0-9（）()·\-\s]{2,120}(?:有限责任公司|股份有限公司|集团有限公司|有限公司|分公司|集团|公司|工厂|厂|勘测设计研究院|工程设计有限公司|研究院|设计院|测绘院|勘测院|勘察院|规划院|科学院|检验院|检测院|计量院|研究所|事务所|大学|学院|学校|医院|中心|服务部|经营部|营业部|门市部|商行|工作室))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<BidOpsOutcomeSupplierExtract> Extract(
        string? title,
        string? noticeType,
        string? text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            !text.Contains("PDF 表格结构", StringComparison.Ordinal) ||
            !BidOpsOutcomeSupplierTextParser.LooksLikeOutcomeNotice(title, noticeType, $"{title}\n{text}"))
        {
            return [];
        }

        var outcomeType = DetermineOutcomeType(title, noticeType, text);
        if (string.IsNullOrWhiteSpace(outcomeType))
            return [];

        var results = new List<BidOpsOutcomeSupplierExtract>();
        foreach (var block in ReadPdfTableBlocks(text))
            ExtractBlock(block, outcomeType, results);

        EnrichMissingLotNoFromUniqueLotName(results);

        return results
            .Where(x => !string.IsNullOrWhiteSpace(x.SupplierName))
            .Where(HasReliableLotContext)
            .GroupBy(x => string.Join(
                '\u001f',
                BidOpsOutcomeSupplierTextParser.NormalizeSupplierName(x.SupplierName),
                NormalizeCode(x.LotNo),
                NormalizeEvidenceText(x.LotName),
                NormalizeCode(x.PackageNo),
                NormalizeOutcomeType(x.OutcomeType),
                x.Rank?.ToString() ?? string.Empty))
            .Select(x => x.OrderByDescending(item => item.Confidence).First())
            .ToList();
    }

    private static void ExtractBlock(
        IReadOnlyList<string> block,
        string outcomeType,
        List<BidOpsOutcomeSupplierExtract> results)
    {
        var header = PdfTableHeader.None;
        foreach (var line in block)
        {
            var cells = ParseMarkdownCells(line);
            if (cells.Count == 0 || IsMarkdownSeparator(cells))
                continue;

            if (TryBuildHeader(cells, out var parsedHeader))
            {
                header = parsedHeader;
                continue;
            }

            if (TryParseRow(cells, header, outcomeType, out var extract))
                results.Add(extract);
        }
    }

    private static bool TryParseRow(
        IReadOnlyList<string> cells,
        PdfTableHeader header,
        string defaultOutcomeType,
        out BidOpsOutcomeSupplierExtract extract)
    {
        extract = new BidOpsOutcomeSupplierExtract();
        if (cells.Count < 3)
            return false;

        var allText = BidOpsTextQuality.CleanExtractedValue(string.Join(' ', cells));
        if (string.IsNullOrWhiteSpace(allText) ||
            allText.Length > 500 ||
            ContainsAny(allText, "重要事项说明", "网上供应商服务大厅", "服务费通知书", "成交通知书"))
        {
            return false;
        }

        var seq = ExtractSequence(cells.ElementAtOrDefault(0));
        var lotNo = NormalizeLotNoCandidate(GetHeaderCell(cells, header.LotNoIndex));
        var lotName = CleanLotName(GetHeaderCell(cells, header.LotNameIndex));
        var packageNo = CleanPackageNo(GetHeaderCell(cells, header.PackageNoIndex));
        var supplierName = CleanSupplierName(GetHeaderCell(cells, header.SupplierIndex));

        if (header.IsGeneric || string.IsNullOrWhiteSpace(packageNo) || string.IsNullOrWhiteSpace(supplierName))
        {
            InferGenericRow(cells, out var inferredLotNo, out var inferredLotName, out var inferredPackageNo, out var inferredSupplierName);
            lotNo = FirstMeaningful(lotNo, inferredLotNo);
            lotName = FirstMeaningful(lotName, inferredLotName);
            packageNo = FirstMeaningful(packageNo, inferredPackageNo);
            supplierName = FirstMeaningful(supplierName, inferredSupplierName);
        }

        if (TrySplitLotNoPrefix(lotName, out var embeddedLotNo, out var cleanedLotName))
        {
            lotNo = FirstMeaningful(lotNo, embeddedLotNo);
            lotName = cleanedLotName;
        }

        var outcomeType = IsNonAwardStatus(supplierName)
            ? BidOpsOutcomeTypes.Failed
            : defaultOutcomeType;
        if (outcomeType == BidOpsOutcomeTypes.Failed)
            supplierName = "流标";

        if (string.IsNullOrWhiteSpace(packageNo) ||
            string.IsNullOrWhiteSpace(supplierName) ||
            (!IsNonAwardStatus(supplierName) && !LooksLikeSupplierName(supplierName)))
        {
            return false;
        }

        extract = new BidOpsOutcomeSupplierExtract
        {
            SupplierName = supplierName,
            OutcomeType = outcomeType,
            SourceType = BidOpsOutcomeSupplierExtractSourceTypes.PdfStructuredTable,
            SourceParserVersion = BidOpsOutcomeSupplierExtractParserVersions.PdfStructuredTable,
            SourceSequenceNo = seq,
            LotNo = lotNo,
            LotName = lotName,
            PackageNo = packageNo,
            EvidenceText = BuildEvidence(seq, lotNo, lotName, packageNo, supplierName),
            Confidence = ConfidenceFor(lotNo, lotName, supplierName)
        };
        return true;
    }

    private static void InferGenericRow(
        IReadOnlyList<string> cells,
        out string lotNo,
        out string lotName,
        out string packageNo,
        out string supplierName)
    {
        lotNo = string.Empty;
        lotName = string.Empty;
        packageNo = string.Empty;
        supplierName = string.Empty;

        var packageIndex = -1;
        for (var i = 0; i < cells.Count; i++)
        {
            packageNo = CleanPackageNo(cells[i]);
            if (!string.IsNullOrWhiteSpace(packageNo))
            {
                packageIndex = i;
                break;
            }
        }

        if (packageIndex < 0)
            return;

        var firstDataIndex = IsSequenceCell(cells[0]) ? 1 : 0;
        var lotNoIndex = -1;
        for (var i = firstDataIndex; i < packageIndex; i++)
        {
            var candidate = NormalizeLotNoCandidate(cells[i]);
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            lotNo = candidate;
            lotNoIndex = i;
            break;
        }

        var lotNameStart = lotNoIndex >= 0 ? lotNoIndex + 1 : firstDataIndex;
        var lotNameCells = cells
            .Skip(Math.Max(firstDataIndex, lotNameStart))
            .Take(Math.Max(0, packageIndex - Math.Max(firstDataIndex, lotNameStart)))
            .Where(x => string.IsNullOrWhiteSpace(NormalizeLotNoCandidate(x)))
            .ToList();
        lotName = CleanLotName(string.Join(string.Empty, lotNameCells));

        for (var i = packageIndex + 1; i < cells.Count; i++)
        {
            supplierName = CleanSupplierName(cells[i]);
            if (!string.IsNullOrWhiteSpace(supplierName))
                return;
        }
    }

    private static IEnumerable<IReadOnlyList<string>> ReadPdfTableBlocks(string text)
    {
        var block = new List<string>();
        var inBlock = false;
        foreach (var rawLine in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.Trim();
            if (IsPdfTableMarker(line))
            {
                if (block.Count > 0)
                {
                    yield return block.ToList();
                    block.Clear();
                }

                inBlock = true;
                continue;
            }

            if (!inBlock)
                continue;

            if (line.StartsWith("|", StringComparison.Ordinal))
            {
                block.Add(line);
                continue;
            }

            if (line.Length == 0)
                continue;

            if (block.Count > 0)
            {
                yield return block.ToList();
                block.Clear();
            }

            inBlock = false;
        }

        if (block.Count > 0)
            yield return block.ToList();
    }

    private static bool TryBuildHeader(IReadOnlyList<string> cells, out PdfTableHeader header)
    {
        header = PdfTableHeader.None;
        if (cells.Count == 0)
            return false;

        if (cells.All(x => Regex.IsMatch(x, @"^列\d+$", RegexOptions.CultureInvariant)))
        {
            header = new PdfTableHeader(-1, -1, -1, -1, IsGeneric: true);
            return true;
        }

        var supplierIndex = FindColumn(cells, "成交供应商", "中标供应商", "成交人", "中标人", "供应商");
        var packageIndex = FindColumn(cells, "包号", "包件号", "分包号", "标包号");
        if (supplierIndex < 0 || packageIndex < 0)
            return false;

        header = new PdfTableHeader(
            FindColumn(cells, "分标编号", "标段编号", "分标号"),
            FindColumn(cells, "分标名称", "标段名称"),
            packageIndex,
            supplierIndex,
            IsGeneric: false);
        return true;
    }

    private static int FindColumn(IReadOnlyList<string> cells, params string[] labels)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            var normalized = NormalizeHeader(cells[i]);
            if (labels.Any(label => normalized.Contains(NormalizeHeader(label), StringComparison.OrdinalIgnoreCase)))
                return i;
        }

        return -1;
    }

    private static List<string> ParseMarkdownCells(string line)
    {
        var parts = line.Trim().Split('|').ToList();
        if (parts.Count > 0 && string.IsNullOrWhiteSpace(parts[0]))
            parts.RemoveAt(0);
        if (parts.Count > 0 && string.IsNullOrWhiteSpace(parts[^1]))
            parts.RemoveAt(parts.Count - 1);

        return parts
            .Select(BidOpsTextQuality.CleanExtractedValue)
            .ToList();
    }

    private static bool IsMarkdownSeparator(IReadOnlyList<string> cells)
    {
        return cells.Count > 0 && cells.All(x => Regex.IsMatch(x, @"^\s*:?-{2,}:?\s*$", RegexOptions.CultureInvariant));
    }

    private static bool IsPdfTableMarker(string line)
    {
        var cleaned = line.TrimStart('#', ' ');
        return cleaned.StartsWith("PDF 表格结构", StringComparison.Ordinal) ||
               cleaned.StartsWith("PDF表格结构", StringComparison.Ordinal);
    }

    private static string GetHeaderCell(IReadOnlyList<string> cells, int index)
    {
        return index >= 0 && index < cells.Count ? cells[index] : string.Empty;
    }

    private static string NormalizeLotNoCandidate(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value)
            .Replace('－', '-')
            .Replace('—', '-')
            .Replace('–', '-')
            .Replace('／', '/')
            .Replace(" ", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(cleaned) ||
            cleaned.StartsWith("-", StringComparison.Ordinal) ||
            cleaned.Any(ch => ch >= '\u4e00' && ch <= '\u9fff'))
        {
            return string.Empty;
        }

        var reversed = Regex.Match(
            cleaned,
            @"^(?<suffix>\d{1,3}-\d{2}-\d{2})(?<prefix>[A-Za-z0-9]{8,})$",
            RegexOptions.CultureInvariant);
        if (reversed.Success)
            cleaned = reversed.Groups["prefix"].Value + reversed.Groups["suffix"].Value;

        var projectSeparated = Regex.Match(
            cleaned,
            @"^(?<project>[A-Za-z0-9]{6})-(?<tail>[A-Za-z0-9]{6,}-[A-Za-z0-9]{1,})$",
            RegexOptions.CultureInvariant);
        if (projectSeparated.Success)
            cleaned = projectSeparated.Groups["project"].Value + projectSeparated.Groups["tail"].Value;

        return LooksLikeStructuredLotNo(cleaned) ? cleaned : string.Empty;
    }

    private static bool TrySplitLotNoPrefix(string? value, out string lotNo, out string lotName)
    {
        lotNo = string.Empty;
        lotName = string.Empty;
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value).Replace(" ", string.Empty, StringComparison.Ordinal);
        var match = Regex.Match(
            cleaned,
            @"^(?<lotNo>[A-Za-z0-9]{6,}(?:[-_/][A-Za-z0-9]{2,}){1,})(?<lotName>[\u4e00-\u9fa5].+)$",
            RegexOptions.CultureInvariant);
        if (!match.Success)
            return false;

        var parsedLotNo = NormalizeLotNoCandidate(match.Groups["lotNo"].Value);
        if (string.IsNullOrWhiteSpace(parsedLotNo))
            return false;

        lotNo = parsedLotNo;
        lotName = CleanLotName(match.Groups["lotName"].Value);
        return !string.IsNullOrWhiteSpace(lotName);
    }

    private static string CleanLotName(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value)
            .Trim(' ', ':', '：', '=', '、', ',', '，', '.', '。', '；', ';');
        return cleaned.Length <= 300 ? cleaned : cleaned[..300];
    }

    private static string CleanPackageNo(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        var match = PackageRegex.Match(cleaned);
        return match.Success ? match.Groups["package"].Value : string.Empty;
    }

    private static string CleanSupplierName(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (IsNonAwardStatus(cleaned))
            return "流标";

        var match = SupplierRegex.Match(cleaned);
        if (!match.Success)
            return string.Empty;

        return match.Groups["name"].Value.Trim(' ', ':', '：', '=', '-', '、', ',', '，', '.', '。', '；', ';');
    }

    private static bool LooksLikeSupplierName(string value)
    {
        return IsNonAwardStatus(value) || SupplierRegex.IsMatch(value);
    }

    private static bool IsNonAwardStatus(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return cleaned is "流标" or "废标" or "采购失败" or "成交失败" or "中标失败" ||
               cleaned.Contains("流标", StringComparison.Ordinal) ||
               cleaned.Contains("废标", StringComparison.Ordinal);
    }

    private static string ExtractSequence(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        var match = Regex.Match(cleaned, @"^\d{1,4}$", RegexOptions.CultureInvariant);
        return match.Success ? match.Value : string.Empty;
    }

    private static bool IsSequenceCell(string? value)
    {
        return !string.IsNullOrWhiteSpace(ExtractSequence(value));
    }

    private static string BuildEvidence(
        string seq,
        string lotNo,
        string lotName,
        string packageNo,
        string supplierName)
    {
        return string.Join(
            ' ',
            new[] { seq, lotNo, lotName, packageNo, supplierName }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static decimal ConfidenceFor(string lotNo, string lotName, string supplierName)
    {
        if (IsNonAwardStatus(supplierName))
            return 0.76m;

        if (!string.IsNullOrWhiteSpace(lotNo) && !string.IsNullOrWhiteSpace(lotName))
            return 0.94m;

        if (!string.IsNullOrWhiteSpace(lotNo) || !string.IsNullOrWhiteSpace(lotName))
            return 0.86m;

        return 0.74m;
    }

    private static bool HasReliableLotContext(BidOpsOutcomeSupplierExtract extract)
    {
        var lotNo = NormalizeCode(extract.LotNo);
        return !string.IsNullOrWhiteSpace(lotNo) && !LooksLikeShiftedPdfLotNo(lotNo);
    }

    private static bool LooksLikeShiftedPdfLotNo(string lotNo)
    {
        // 部分 PDF 表格结构会把上一行尾号和下一行分标编号粘在一起，例如 299906FF01-9013003。
        var match = Regex.Match(
            lotNo,
            @"^\d{3,4}(?<rest>[A-Z0-9]{6,}(?:[-_/][A-Z0-9]{1,})+)$",
            RegexOptions.CultureInvariant);
        return match.Success && LooksLikeStructuredLotNo(match.Groups["rest"].Value);
    }

    private static void EnrichMissingLotNoFromUniqueLotName(List<BidOpsOutcomeSupplierExtract> extracts)
    {
        var contexts = extracts
            .Where(x => !string.IsNullOrWhiteSpace(x.LotNo) && !string.IsNullOrWhiteSpace(x.LotName))
            .GroupBy(x => NormalizeEvidenceText(x.LotName))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) &&
                        x.Select(item => NormalizeCode(item.LotNo)).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
            .ToDictionary(
                x => x.Key,
                x => x.First().LotNo,
                StringComparer.OrdinalIgnoreCase);

        foreach (var extract in extracts)
        {
            if (!string.IsNullOrWhiteSpace(extract.LotNo) || string.IsNullOrWhiteSpace(extract.LotName))
                continue;

            if (!contexts.TryGetValue(NormalizeEvidenceText(extract.LotName), out var lotNo))
                continue;

            extract.LotNo = lotNo;
            extract.EvidenceText = BuildEvidence(string.Empty, extract.LotNo, extract.LotName, extract.PackageNo, extract.SupplierName);
            extract.Confidence = Math.Min(extract.Confidence, 0.82m);
        }
    }

    private static string DetermineOutcomeType(string? title, string? noticeType, string text)
    {
        var signal = $"{title}\n{noticeType}\n{text}";
        if (ContainsAny(signal, "CandidateAnnouncement", "中标候选人", "成交候选人", "候选人公示"))
            return BidOpsOutcomeTypes.Candidate;

        if (ContainsAny(signal, "AwardAnnouncement", "ResultAnnouncement", "中标结果", "成交结果", "中标公告", "成交公告", "成交人", "中标人"))
            return BidOpsOutcomeTypes.Awarded;

        return string.Empty;
    }

    private static string NormalizeOutcomeType(string value)
    {
        return value switch
        {
            BidOpsOutcomeTypes.Awarded => BidOpsOutcomeTypes.Awarded,
            BidOpsOutcomeTypes.Shortlisted => BidOpsOutcomeTypes.Shortlisted,
            BidOpsOutcomeTypes.Failed => BidOpsOutcomeTypes.Failed,
            _ => BidOpsOutcomeTypes.Candidate
        };
    }

    private static string NormalizeCode(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return string.Concat(cleaned.Where(x => !char.IsWhiteSpace(x) && !":：,，;；".Contains(x))).ToUpperInvariant();
    }

    private static string NormalizeHeader(string value)
    {
        return string.Concat(BidOpsTextQuality.CleanExtractedValue(value).Where(x => !char.IsWhiteSpace(x)));
    }

    private static string NormalizeEvidenceText(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return string.Concat(cleaned.Where(x => !char.IsWhiteSpace(x) && !"()（）[]【】{}<>《》,，.。;；:：-_—–/\\|".Contains(x))).ToUpperInvariant();
    }

    private static bool LooksLikeStructuredLotNo(string value)
    {
        var normalized = NormalizeCode(value);
        return Regex.IsMatch(normalized, @"^(?:[A-Z0-9]{6,}(?:[-_/][A-Z0-9]{1,})+|[A-Z0-9]{10,})$", RegexOptions.CultureInvariant) &&
               normalized.Any(char.IsDigit);
    }

    private static string FirstMeaningful(params string?[] values)
    {
        foreach (var value in values)
        {
            var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
            if (!string.IsNullOrWhiteSpace(cleaned))
                return cleaned;
        }

        return string.Empty;
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record PdfTableHeader(
        int LotNoIndex,
        int LotNameIndex,
        int PackageNoIndex,
        int SupplierIndex,
        bool IsGeneric)
    {
        public static PdfTableHeader None { get; } = new(-1, -1, -1, -1, IsGeneric: true);
    }
}
