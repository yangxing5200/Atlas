using System.Text.RegularExpressions;

namespace Atlas.Modules.BidOps.Ai;

public static partial class BidOpsEcpProcurementTableParser
{
    private const decimal PackageConfidence = 0.98m;
    private const decimal RequirementConfidence = 0.96m;

    public static IReadOnlyList<BidOpsPackageExtract> ExtractPackages(
        string text,
        string defaultCategory)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<BidOpsPackageExtract>();

        var tables = ParseMarkdownTables(text);
        var scopeTable = tables.FirstOrDefault(IsProcurementScopeTable);
        if (scopeTable == null)
            return Array.Empty<BidOpsPackageExtract>();

        var packages = ParseScopePackages(scopeTable, defaultCategory);
        if (packages.Count == 0)
            return Array.Empty<BidOpsPackageExtract>();

        var qualificationTable = tables.FirstOrDefault(IsSupplierQualificationTable);
        if (qualificationTable != null)
            AddQualificationRequirements(packages, qualificationTable);

        return packages
            .Select(x => x.ToExtract())
            .ToList();
    }

    private static List<EcpPackageCandidate> ParseScopePackages(
        MarkdownTable table,
        string defaultCategory)
    {
        var lotCodeIndex = FindColumn(table.Headers, "分标编号", "分标号", "标段编号");
        var lotNameIndex = FindColumn(table.Headers, "分标名称", "标段名称");
        var packageNoIndex = FindColumn(table.Headers, "包号", "包件号", "标包号", "分包编号", "分包号");
        var packageNameIndex = FindColumn(table.Headers, "包名称", "包件名称", "标包名称", "项目名称");
        var servicePeriodIndex = FindColumn(table.Headers, "服务期", "服务期限", "框架协议有效期", "工期");
        var placeIndex = FindColumn(table.Headers, "实施地点", "服务地点", "交货地点", "履约地点");

        if (packageNoIndex < 0 || packageNameIndex < 0)
            return [];

        var packages = new List<EcpPackageCandidate>();
        var currentLotCode = string.Empty;
        var currentLotName = string.Empty;
        foreach (var row in table.Rows)
        {
            var lotCode = GetCell(row, lotCodeIndex);
            var lotName = GetCell(row, lotNameIndex);
            if (!string.IsNullOrWhiteSpace(lotCode))
                currentLotCode = lotCode;
            if (!string.IsNullOrWhiteSpace(lotName))
                currentLotName = lotName;

            var packageNo = GetCell(row, packageNoIndex);
            var packageName = GetCell(row, packageNameIndex);
            if (string.IsNullOrWhiteSpace(packageNo) && string.IsNullOrWhiteSpace(packageName))
                continue;

            packages.Add(new EcpPackageCandidate(
                currentLotCode,
                currentLotName,
                packageNo,
                packageName,
                NormalizeCategory(defaultCategory, currentLotName),
                GetCell(row, placeIndex),
                GetCell(row, servicePeriodIndex)));
        }

        return packages;
    }

    private static void AddQualificationRequirements(
        List<EcpPackageCandidate> packages,
        MarkdownTable table)
    {
        var lotIndex = FindColumn(table.Headers, "分标", "分标编号", "标段");
        var packageNoIndex = FindColumn(table.Headers, "包号", "包件号", "标包号", "分包号");
        var packageNameIndex = FindColumn(table.Headers, "包名称", "包件名称", "标包名称");
        var qualificationIndex = FindColumn(table.Headers, "资质要求", "资格要求");
        var performanceIndex = FindColumn(table.Headers, "业绩要求", "业绩");
        var personnelIndex = FindColumn(table.Headers, "人员要求", "人员");
        if (packageNoIndex < 0 || packageNameIndex < 0)
            return;

        var byExact = packages
            .GroupBy(x => BuildExactKey(x.LotNo, x.PackageNo, x.PackageName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var byLoose = packages
            .GroupBy(x => BuildLooseKey(x.LotNo, x.PackageNo), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var currentLotCode = string.Empty;
        foreach (var row in table.Rows)
        {
            var lotText = GetCell(row, lotIndex);
            var (lotCode, _) = SplitLotText(lotText);
            if (!string.IsNullOrWhiteSpace(lotCode))
                currentLotCode = lotCode;
            else
                lotCode = currentLotCode;

            var packageNo = GetCell(row, packageNoIndex);
            var packageName = GetCell(row, packageNameIndex);
            var package = FindPackage(byExact, byLoose, lotCode, packageNo, packageName);
            if (package == null)
                continue;

            AddQualificationRequirement(package, GetCell(row, qualificationIndex), row.RowIndex);
            AddRequirement(
                package,
                "Performance",
                GetCell(row, performanceIndex),
                row.RowIndex,
                isMandatory: true,
                isRejectRisk: false,
                evidenceType: "PerformanceContract",
                riskLevel: "Medium");
            AddRequirement(
                package,
                "Personnel",
                GetCell(row, personnelIndex),
                row.RowIndex,
                isMandatory: true,
                isRejectRisk: true,
                evidenceType: "PersonnelCertificate",
                riskLevel: "High");
        }
    }

    private static void AddQualificationRequirement(
        EcpPackageCandidate package,
        string value,
        int rowIndex)
    {
        if (IsEmptyRequirement(value))
            return;

        var hasJointVenture = ContainsAny(value, "接受联合体响应", "接受联合体", "联合体");
        var qualificationRemainder = value;
        if (hasJointVenture)
        {
            AddRequirement(
                package,
                "JointVenture",
                "接受联合体响应",
                rowIndex,
                isMandatory: false,
                isRejectRisk: false,
                evidenceType: "BidDocument",
                riskLevel: "Low");

            qualificationRemainder = qualificationRemainder
                .Replace("接受联合体响应", string.Empty, StringComparison.Ordinal)
                .Replace("接受联合体", string.Empty, StringComparison.Ordinal)
                .Replace("联合体", string.Empty, StringComparison.Ordinal)
                .Trim(' ', '。', '；', ';', '/', '，', ',');
        }

        if (IsEmptyRequirement(qualificationRemainder))
            return;

        AddRequirement(
            package,
            "Qualification",
            qualificationRemainder,
            rowIndex,
            isMandatory: true,
            isRejectRisk: true,
            evidenceType: "QualificationCertificate",
            riskLevel: ContainsAny(qualificationRemainder, "许可证", "资质", "认证证书") ? "High" : "Medium");
    }

    private static void AddRequirement(
        EcpPackageCandidate package,
        string type,
        string value,
        int rowIndex,
        bool isMandatory,
        bool isRejectRisk,
        string evidenceType,
        string riskLevel)
    {
        if (IsEmptyRequirement(value))
            return;

        var text = value.Trim();
        if (!package.RequirementKeys.Add($"{type}:{NormalizeKey(text)}"))
            return;

        package.Requirements.Add(new BidOpsRequirementExtract(
            type,
            text.Length <= 2000 ? text : text[..2000],
            null,
            isMandatory,
            isRejectRisk,
            evidenceType,
            riskLevel,
            $"Extracted from State Grid ECP procurement attachment table row {rowIndex}.",
            RequirementConfidence));
    }

    private static EcpPackageCandidate? FindPackage(
        IReadOnlyDictionary<string, EcpPackageCandidate> byExact,
        IReadOnlyDictionary<string, EcpPackageCandidate> byLoose,
        string lotCode,
        string packageNo,
        string packageName)
    {
        return byExact.TryGetValue(BuildExactKey(lotCode, packageNo, packageName), out var exact)
            ? exact
            : byLoose.TryGetValue(BuildLooseKey(lotCode, packageNo), out var loose)
                ? loose
                : null;
    }

    private static bool IsProcurementScopeTable(MarkdownTable table)
    {
        return CountHeaderHits(
            table.Headers,
            "分标编号",
            "分标名称",
            "包号",
            "包名称",
            "采购范围",
            "项目内容",
            "服务期",
            "框架协议有效期",
            "实施地点",
            "交货地点",
            "服务地点") >= 5;
    }

    private static bool IsSupplierQualificationTable(MarkdownTable table)
    {
        return FindColumn(table.Headers, "分标", "分标编号", "标段") >= 0 &&
               FindColumn(table.Headers, "包号", "包件号", "标包号", "分包号") >= 0 &&
               FindColumn(table.Headers, "包名称", "包件名称", "标包名称") >= 0 &&
               FindColumn(table.Headers, "资质要求", "资格要求") >= 0 &&
               FindColumn(table.Headers, "业绩要求", "业绩") >= 0 &&
               FindColumn(table.Headers, "人员要求", "人员") >= 0;
    }

    private static int CountHeaderHits(
        IReadOnlyList<string> headers,
        params string[] aliases)
    {
        return aliases
            .Select(NormalizeHeader)
            .Count(alias => headers.Any(header =>
                NormalizeHeader(header).Contains(alias, StringComparison.OrdinalIgnoreCase)));
    }

    private static int FindColumn(
        IReadOnlyList<string> headers,
        params string[] aliases)
    {
        foreach (var alias in aliases.Select(NormalizeHeader))
        {
            for (var i = 0; i < headers.Count; i++)
            {
                if (NormalizeHeader(headers[i]).Contains(alias, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        return -1;
    }

    private static IReadOnlyList<MarkdownTable> ParseMarkdownTables(string text)
    {
        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var tables = new List<MarkdownTable>();

        for (var i = 0; i + 1 < lines.Length; i++)
        {
            if (!IsMarkdownRow(lines[i]) || !IsMarkdownSeparator(lines[i + 1]))
                continue;

            var headers = SplitMarkdownRow(lines[i]);
            var rows = new List<MarkdownRow>();
            var rowIndex = 1;
            var cursor = i + 2;
            while (cursor < lines.Length && IsMarkdownRow(lines[cursor]))
            {
                var cells = SplitMarkdownRow(lines[cursor]);
                if (cells.Any(x => !string.IsNullOrWhiteSpace(x)))
                {
                    rows.Add(new MarkdownRow(rowIndex, cells));
                    rowIndex++;
                }

                cursor++;
            }

            if (headers.Count > 0 && rows.Count > 0)
            {
                var heading = FindNearbyHeading(lines, i);
                var promotedHeaders = PromoteContinuationHeader(headers, rows, out var promotedRows);
                promotedHeaders = FillBlankQualificationHeaders(heading, promotedHeaders);
                if (promotedRows.Count > 0)
                    tables.Add(new MarkdownTable(heading, promotedHeaders, promotedRows));
            }

            i = cursor;
        }

        return tables;
    }

    private static IReadOnlyList<string> FillBlankQualificationHeaders(
        string heading,
        IReadOnlyList<string> headers)
    {
        if (headers.Count < 6 ||
            !ContainsAny(heading, "专用资格要求", "资格要求") ||
            FindColumn(headers, "资质要求", "资格要求") < 0 ||
            FindColumn(headers, "业绩要求", "业绩") < 0 ||
            FindColumn(headers, "人员要求", "人员") < 0)
        {
            return headers;
        }

        var filled = headers.ToList();
        var defaults = new[] { "分标", "包号", "包名称" };
        for (var i = 0; i < defaults.Length && i < filled.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(filled[i]))
                filled[i] = defaults[i];
        }

        return filled;
    }

    private static IReadOnlyList<string> PromoteContinuationHeader(
        IReadOnlyList<string> headers,
        IReadOnlyList<MarkdownRow> rows,
        out IReadOnlyList<MarkdownRow> promotedRows)
    {
        promotedRows = rows;
        if (rows.Count == 0)
            return headers;

        var mergedHeaders = MergeHeaderRows(headers, rows[0].Cells);
        if (ScoreMarkdownHeaderRow(mergedHeaders) <= ScoreMarkdownHeaderRow(headers))
            return headers;

        promotedRows = rows.Skip(1).ToList();
        return mergedHeaders;
    }

    private static IReadOnlyList<string> MergeHeaderRows(
        IReadOnlyList<string> parent,
        IReadOnlyList<string> child)
    {
        var columnCount = Math.Max(parent.Count, child.Count);
        var merged = new List<string>(columnCount);
        for (var i = 0; i < columnCount; i++)
        {
            var childCell = i < child.Count ? child[i] : string.Empty;
            merged.Add(string.IsNullOrWhiteSpace(childCell) && i < parent.Count
                ? parent[i]
                : childCell);
        }

        return merged;
    }

    private static int ScoreMarkdownHeaderRow(IReadOnlyList<string> headers)
    {
        return CountHeaderHits(
            headers,
            "分标编号",
            "分标名称",
            "分标",
            "标段",
            "包号",
            "包名称",
            "采购范围",
            "项目内容",
            "服务期",
            "服务期限",
            "框架协议有效期",
            "实施地点",
            "交货地点",
            "服务地点",
            "资质要求",
            "资格要求",
            "业绩要求",
            "人员要求");
    }

    private static string FindNearbyHeading(string[] lines, int tableStart)
    {
        for (var i = tableStart - 1; i >= 0 && i >= tableStart - 4; i--)
        {
            var line = lines[i].Trim();
            if (!string.IsNullOrWhiteSpace(line))
                return line.TrimStart('#', ' ');
        }

        return string.Empty;
    }

    private static bool IsMarkdownRow(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 2 &&
               trimmed.StartsWith('|') &&
               trimmed.EndsWith('|');
    }

    private static bool IsMarkdownSeparator(string line)
    {
        if (!IsMarkdownRow(line))
            return false;

        var cells = SplitMarkdownRow(line);
        return cells.Count > 0 &&
               cells.All(cell => cell.All(ch => ch is '-' or ':' or ' '));
    }

    private static IReadOnlyList<string> SplitMarkdownRow(string line)
    {
        return line
            .Trim()
            .Trim('|')
            .Split('|')
            .Select(x => x.Trim())
            .ToList();
    }

    private static string GetCell(MarkdownRow row, int index)
    {
        return index >= 0 && index < row.Cells.Count
            ? row.Cells[index].Trim()
            : string.Empty;
    }

    private static (string Code, string Name) SplitLotText(string value)
    {
        var match = LotTextRegex().Match(value.Trim());
        return match.Success
            ? (match.Groups["code"].Value.Trim(), match.Groups["name"].Value.Trim())
            : (value.Trim(), string.Empty);
    }

    private static string BuildExactKey(string lotCode, string packageNo, string packageName)
    {
        return $"{NormalizeKey(lotCode)}|{NormalizeKey(packageNo)}|{NormalizeKey(packageName)}";
    }

    private static string BuildLooseKey(string lotCode, string packageNo)
    {
        return $"{NormalizeKey(lotCode)}|{NormalizeKey(packageNo)}";
    }

    private static string NormalizeHeader(string value)
    {
        return NormalizeKey(value
            .Replace("／", "/", StringComparison.Ordinal)
            .Replace("/", string.Empty, StringComparison.Ordinal));
    }

    private static string NormalizeKey(string value)
    {
        return string.Concat(value
            .Where(ch => !char.IsWhiteSpace(ch) &&
                         ch is not '|' and not ':' and not '：' and not '-' and not '_' and not '(' and not ')' and not '（' and not '）'))
            .ToLowerInvariant();
    }

    private static string NormalizeCategory(string defaultCategory, string lotName)
    {
        if (!string.IsNullOrWhiteSpace(defaultCategory) &&
            !string.Equals(defaultCategory, "Other", StringComparison.OrdinalIgnoreCase))
        {
            return defaultCategory;
        }

        return ContainsAny(lotName, "服务", "运维", "设计", "咨询") ? "Service" : "Other";
    }

    private static bool IsEmptyRequirement(string value)
    {
        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized) ||
               normalized is "/" or "／" or "无" or "无要求" or "不适用" or "-";
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"^(?<code>\d{6}-\d+)(?<name>.+)$")]
    private static partial Regex LotTextRegex();

    private sealed record MarkdownTable(
        string Heading,
        IReadOnlyList<string> Headers,
        IReadOnlyList<MarkdownRow> Rows);

    private sealed record MarkdownRow(
        int RowIndex,
        IReadOnlyList<string> Cells);

    private sealed class EcpPackageCandidate
    {
        public EcpPackageCandidate(
            string lotNo,
            string lotName,
            string packageNo,
            string packageName,
            string category,
            string deliveryPlace,
            string deliveryPeriod)
        {
            LotNo = lotNo;
            LotName = lotName;
            PackageNo = packageNo;
            PackageName = packageName;
            Category = category;
            DeliveryPlace = deliveryPlace;
            DeliveryPeriod = deliveryPeriod;
        }

        public string LotNo { get; }
        public string LotName { get; }
        public string PackageNo { get; }
        public string PackageName { get; }
        public string Category { get; }
        public string DeliveryPlace { get; }
        public string DeliveryPeriod { get; }
        public List<BidOpsRequirementExtract> Requirements { get; } = [];
        public HashSet<string> RequirementKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

        public BidOpsPackageExtract ToExtract()
        {
            return new BidOpsPackageExtract(
                LotNo,
                string.IsNullOrWhiteSpace(LotName) ? "未分标段" : LotName,
                PackageNo,
                PackageName,
                Category,
                Quantity: null,
                Unit: string.Empty,
                BudgetAmount: null,
                MaxPrice: null,
                DeliveryPlace,
                DeliveryPeriod,
                PackageConfidence,
                Requirements);
        }
    }
}
