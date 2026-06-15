using System.Text.RegularExpressions;

namespace Atlas.Modules.BidOps.Ai.Evidence;

public static partial class BidOpsAwardEvidenceParser
{
    public static IReadOnlyList<AwardEvidence> Extract(IReadOnlyList<BidOpsEvidenceDocument> documents)
    {
        var results = new List<AwardEvidence>();
        foreach (var document in documents)
        {
            ExtractTables(document, results);
            ExtractParagraphs(document, results);
        }

        return results
            .Where(x => !string.IsNullOrWhiteSpace(x.AwardedSupplierName))
            .GroupBy(x => new
            {
                Package = x.NormalizedPackageNo ?? string.Empty,
                Supplier = BidOpsSupplierNameNormalizer.NormalizeForMatch(x.AwardedSupplierName),
                Text = x.Evidence.EvidenceText ?? string.Empty
            })
            .Select(x => x.OrderByDescending(item => item.Confidence).First())
            .ToList();
    }

    private static void ExtractTables(
        BidOpsEvidenceDocument document,
        List<AwardEvidence> results)
    {
        var projectCodeFallback = BidOpsEvidenceText.ExtractProjectCode(document.Text);
        var projectNameFallback = FirstNonEmpty(BidOpsEvidenceText.ExtractProjectName(document.Text), document.Title);

        foreach (var table in BidOpsEvidenceTableParser.Parse(document.Text))
        {
            var tableContext = string.IsNullOrWhiteSpace(table.ContextText)
                ? document.Text
                : table.ContextText;
            var projectCodeFallbackForTable = FirstNonEmpty(
                BidOpsEvidenceText.ExtractProjectCode(tableContext),
                projectCodeFallback);
            var lotNoFallback = string.IsNullOrWhiteSpace(table.ContextText)
                ? string.Empty
                : BidOpsEvidenceText.ExtractLotNo(table.ContextText);
            var lotNameFallback = string.IsNullOrWhiteSpace(table.ContextText)
                ? string.Empty
                : BidOpsEvidenceText.ExtractLotName(table.ContextText);
            var supplierIndex = BidOpsEvidenceTableParser.FindColumn(
                table.Headers,
                "中标人",
                "成交人",
                "中标单位",
                "成交单位",
                "中标供应商",
                "成交供应商",
                "供应商名称",
                "WINNER_NAME");
            var packageIndex = BidOpsEvidenceTableParser.FindColumn(
                table.Headers,
                "包号",
                "包件号",
                "包件编号",
                "分包编号",
                "分包号",
                "标包号",
                "采购包号");

            if (supplierIndex < 0 || packageIndex < 0)
                continue;

            var projectIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "项目编号", "采购编号", "招标编号", "批次编号", "PURPRJ_CODE");
            var projectNameIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "项目名称", "采购项目名称", "招标项目名称");
            var projectUnitIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "项目单位", "采购单位", "需求单位", "建设单位", "业主单位", "项目法人");
            var lotIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "分标编号", "标段编号", "分标号", "分标");
            var lotNameIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "分标名称", "标段名称");
            var packageNameIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "包名称", "包件名称", "标包名称");
            var statusIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "中标状态", "成交状态", "状态");
            var amountIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "中标金额", "成交金额", "中标价", "成交价", "金额", "报价");

            foreach (var row in table.Rows)
            {
                var supplier = BidOpsSupplierNameNormalizer.Clean(BidOpsEvidenceTableParser.GetCell(row, supplierIndex));
                var packageNo = BidOpsTextQuality.CleanExtractedValue(BidOpsEvidenceTableParser.GetCell(row, packageIndex));
                if (string.IsNullOrWhiteSpace(supplier) || string.IsNullOrWhiteSpace(packageNo))
                    continue;

                var status = BidOpsEvidenceTableParser.GetCell(row, statusIndex);
                if (IsNonAwardStatus(status))
                    continue;

                var amountText = amountIndex >= 0
                    ? BidOpsEvidenceTableParser.GetCell(row, amountIndex)
                    : row.RawText;
                var amount = amountIndex >= 0 ? BidOpsMoneyNormalizer.TryNormalize(amountText) : null;
                var evidence = document.Source with
                {
                    TableIndex = table.TableIndex,
                    RowIndex = row.RowIndex,
                    ColumnIndex = supplierIndex,
                    EvidenceText = row.RawText
                };

                results.Add(new AwardEvidence(
                    ProjectCode: FirstNonEmpty(BidOpsEvidenceTableParser.GetCell(row, projectIndex), projectCodeFallbackForTable),
                    ProjectName: FirstNonEmpty(BidOpsEvidenceTableParser.GetCell(row, projectNameIndex), projectNameFallback),
                    ProjectUnit: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, projectUnitIndex)),
                    LotNo: EmptyToNull(FirstNonEmpty(BidOpsEvidenceTableParser.GetCell(row, lotIndex), lotNoFallback)),
                    LotName: EmptyToNull(FirstNonEmpty(BidOpsEvidenceTableParser.GetCell(row, lotNameIndex), lotNameFallback)),
                    PackageNo: packageNo,
                    NormalizedPackageNo: EmptyToNull(BidOpsPackageNoNormalizer.Normalize(packageNo)),
                    PackageName: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, packageNameIndex)),
                    AwardedSupplierName: supplier,
                    AwardAmount: amount,
                    AmountSource: amount.HasValue ? "AwardNotice" : "Missing",
                    Evidence: evidence,
                    Confidence: amount.HasValue ? 0.93 : 0.88));
            }
        }
    }

    private static void ExtractParagraphs(
        BidOpsEvidenceDocument document,
        List<AwardEvidence> results)
    {
        var projectCode = BidOpsEvidenceText.ExtractProjectCode(document.Text);
        var projectName = FirstNonEmpty(BidOpsEvidenceText.ExtractProjectName(document.Text), document.Title);
        var rowIndex = 0;
        foreach (var rawLine in SplitLines(document.Text))
        {
            rowIndex++;
            var line = BidOpsTextQuality.CleanExtractedValue(rawLine);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var match = ParagraphAwardRegex().Match(line);
            if (!match.Success)
                continue;

            var packageNo = match.Groups["package"].Value;
            var supplier = BidOpsSupplierNameNormalizer.Clean(match.Groups["supplier"].Value);
            if (string.IsNullOrWhiteSpace(supplier))
                continue;

            results.Add(new AwardEvidence(
                ProjectCode: EmptyToNull(projectCode),
                ProjectName: EmptyToNull(projectName),
                ProjectUnit: null,
                LotNo: null,
                LotName: null,
                PackageNo: packageNo,
                NormalizedPackageNo: EmptyToNull(BidOpsPackageNoNormalizer.Normalize(packageNo)),
                PackageName: null,
                AwardedSupplierName: supplier,
                AwardAmount: null,
                AmountSource: "Missing",
                Evidence: document.Source with
                {
                    RowIndex = rowIndex,
                    EvidenceText = line
                },
                Confidence: 0.72));
        }
    }

    private static IEnumerable<string> SplitLines(string value)
    {
        return value
            .Replace('\t', ' ')
            .Split(['\r', '\n', '。', '；', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string? EmptyToNull(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
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

    private static bool IsNonAwardStatus(string? value)
    {
        var status = BidOpsTextQuality.CleanExtractedValue(value);
        return status.Contains("未中标", StringComparison.Ordinal) ||
               status.Contains("未成交", StringComparison.Ordinal) ||
               status.Contains("流标", StringComparison.Ordinal) ||
               status.Contains("废标", StringComparison.Ordinal) ||
               status.Contains("否决", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?<package>(?:包|分包|标包|采购包|第)\s*[0-9一二三四五六七八九十]+(?:\s*包)?)\s*[:：= ]+\s*(?:中标人|成交人|中标单位|成交单位|中标供应商|成交供应商)?\s*[:：= ]*\s*(?<supplier>[\u4e00-\u9fa5A-Za-z0-9（）()·\-\s]{2,120}(?:有限责任公司|股份有限公司|集团有限公司|有限公司|分公司|集团|公司|工厂|厂|研究院|设计院|测绘院|大学|学院|医院|中心))", RegexOptions.CultureInvariant)]
    private static partial Regex ParagraphAwardRegex();
}
