namespace Atlas.Modules.BidOps.Ai.Evidence;

public static class BidOpsTenderPackageEvidenceParser
{
    public static IReadOnlyList<TenderPackageEvidence> Extract(IReadOnlyList<BidOpsEvidenceDocument> documents)
    {
        var results = new List<TenderPackageEvidence>();
        foreach (var document in documents)
            ExtractTables(document, results);

        return results;
    }

    private static void ExtractTables(
        BidOpsEvidenceDocument document,
        List<TenderPackageEvidence> results)
    {
        var projectCodeFallback = BidOpsEvidenceText.ExtractProjectCode(document.Text);
        var projectNameFallback = FirstNonEmpty(BidOpsEvidenceText.ExtractProjectName(document.Text), document.Title);
        foreach (var table in BidOpsEvidenceTableParser.Parse(document.Text))
        {
            var headers = string.Join('|', table.Headers);
            if (LooksLikeScopeTable(headers))
                ExtractScopeTable(document, table, projectCodeFallback, projectNameFallback, results);
            if (LooksLikeBudgetTable(headers))
                ExtractBudgetTable(document, table, projectCodeFallback, projectNameFallback, results);
            if (LooksLikeQualificationTable(headers))
                ExtractQualificationTable(document, table, projectCodeFallback, projectNameFallback, results);
        }
    }

    private static void ExtractScopeTable(
        BidOpsEvidenceDocument document,
        BidOpsExtractedTable table,
        string projectCodeFallback,
        string projectNameFallback,
        List<TenderPackageEvidence> results)
    {
        var columns = BuildCommonColumns(table);
        var scopeIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "采购范围", "项目内容", "采购内容", "服务内容");
        var deliveryPeriodIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "服务期", "服务期限", "框架协议有效期", "工期", "交货期");
        var placeIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "实施地点", "服务地点", "交货地点", "履约地点");
        if (columns.PackageIndex < 0 || scopeIndex < 0)
            return;

        foreach (var row in table.Rows)
        {
            var packageNo = BidOpsEvidenceTableParser.GetCell(row, columns.PackageIndex);
            if (string.IsNullOrWhiteSpace(packageNo))
                continue;

            results.Add(new TenderPackageEvidence(
                ProjectCode: EmptyToNull(FirstNonEmpty(BidOpsEvidenceTableParser.GetCell(row, columns.ProjectIndex), projectCodeFallback)),
                ProjectName: EmptyToNull(FirstNonEmpty(BidOpsEvidenceTableParser.GetCell(row, columns.ProjectNameIndex), projectNameFallback)),
                LotNo: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.LotIndex)),
                LotName: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.LotNameIndex)),
                PackageNo: packageNo,
                NormalizedPackageNo: EmptyToNull(BidOpsPackageNoNormalizer.Normalize(packageNo)),
                PackageName: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.PackageNameIndex)),
                Category: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.CategoryIndex)),
                ScopeText: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, scopeIndex)),
                BudgetAmount: null,
                MaxPrice: null,
                Quantity: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.QuantityIndex)),
                DeliveryPlace: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, placeIndex)),
                DeliveryPeriod: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, deliveryPeriodIndex)),
                QualificationText: null,
                PerformanceRequirement: null,
                PersonnelRequirement: null,
                Evidence: document.Source with
                {
                    TableIndex = table.TableIndex,
                    RowIndex = row.RowIndex,
                    EvidenceText = row.RawText
                },
                Confidence: 0.9));
        }
    }

    private static void ExtractBudgetTable(
        BidOpsEvidenceDocument document,
        BidOpsExtractedTable table,
        string projectCodeFallback,
        string projectNameFallback,
        List<TenderPackageEvidence> results)
    {
        var columns = BuildCommonColumns(table);
        var budgetIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "预算金额", "概算金额", "估算金额", "预算");
        var maxPriceIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "最高限价", "最高投标限价", "控制价", "限价");
        if (columns.PackageIndex < 0 || (budgetIndex < 0 && maxPriceIndex < 0))
            return;

        foreach (var row in table.Rows)
        {
            var packageNo = BidOpsEvidenceTableParser.GetCell(row, columns.PackageIndex);
            if (string.IsNullOrWhiteSpace(packageNo))
                continue;

            var budget = BidOpsMoneyNormalizer.TryNormalize(BidOpsEvidenceTableParser.GetCell(row, budgetIndex));
            var maxPrice = BidOpsMoneyNormalizer.TryNormalize(BidOpsEvidenceTableParser.GetCell(row, maxPriceIndex));
            results.Add(new TenderPackageEvidence(
                ProjectCode: EmptyToNull(FirstNonEmpty(BidOpsEvidenceTableParser.GetCell(row, columns.ProjectIndex), projectCodeFallback)),
                ProjectName: EmptyToNull(FirstNonEmpty(BidOpsEvidenceTableParser.GetCell(row, columns.ProjectNameIndex), projectNameFallback)),
                LotNo: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.LotIndex)),
                LotName: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.LotNameIndex)),
                PackageNo: packageNo,
                NormalizedPackageNo: EmptyToNull(BidOpsPackageNoNormalizer.Normalize(packageNo)),
                PackageName: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.PackageNameIndex)),
                Category: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.CategoryIndex)),
                ScopeText: null,
                BudgetAmount: budget,
                MaxPrice: maxPrice,
                Quantity: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.QuantityIndex)),
                DeliveryPlace: null,
                DeliveryPeriod: null,
                QualificationText: null,
                PerformanceRequirement: null,
                PersonnelRequirement: null,
                Evidence: document.Source with
                {
                    TableIndex = table.TableIndex,
                    RowIndex = row.RowIndex,
                    EvidenceText = row.RawText
                },
                Confidence: budget.HasValue || maxPrice.HasValue ? 0.92 : 0.8));
        }
    }

    private static void ExtractQualificationTable(
        BidOpsEvidenceDocument document,
        BidOpsExtractedTable table,
        string projectCodeFallback,
        string projectNameFallback,
        List<TenderPackageEvidence> results)
    {
        var columns = BuildCommonColumns(table);
        var qualificationIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "资质要求", "资格要求", "资格条件");
        var performanceIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "业绩要求", "业绩");
        var personnelIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "人员要求", "人员");
        if (columns.PackageIndex < 0 ||
            qualificationIndex < 0 && performanceIndex < 0 && personnelIndex < 0)
        {
            return;
        }

        foreach (var row in table.Rows)
        {
            var packageNo = BidOpsEvidenceTableParser.GetCell(row, columns.PackageIndex);
            if (string.IsNullOrWhiteSpace(packageNo))
                continue;

            results.Add(new TenderPackageEvidence(
                ProjectCode: EmptyToNull(FirstNonEmpty(BidOpsEvidenceTableParser.GetCell(row, columns.ProjectIndex), projectCodeFallback)),
                ProjectName: EmptyToNull(FirstNonEmpty(BidOpsEvidenceTableParser.GetCell(row, columns.ProjectNameIndex), projectNameFallback)),
                LotNo: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.LotIndex)),
                LotName: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.LotNameIndex)),
                PackageNo: packageNo,
                NormalizedPackageNo: EmptyToNull(BidOpsPackageNoNormalizer.Normalize(packageNo)),
                PackageName: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.PackageNameIndex)),
                Category: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, columns.CategoryIndex)),
                ScopeText: null,
                BudgetAmount: null,
                MaxPrice: null,
                Quantity: null,
                DeliveryPlace: null,
                DeliveryPeriod: null,
                QualificationText: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, qualificationIndex)),
                PerformanceRequirement: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, performanceIndex)),
                PersonnelRequirement: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, personnelIndex)),
                Evidence: document.Source with
                {
                    TableIndex = table.TableIndex,
                    RowIndex = row.RowIndex,
                    EvidenceText = row.RawText
                },
                Confidence: 0.88));
        }
    }

    private static bool LooksLikeScopeTable(string headers)
    {
        var normalized = BidOpsEvidenceTableParser.NormalizeHeader(headers);
        return normalized.Contains("包号", StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains("包名称", StringComparison.OrdinalIgnoreCase) &&
               (normalized.Contains("采购范围", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("项目内容", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("服务内容", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeBudgetTable(string headers)
    {
        var normalized = BidOpsEvidenceTableParser.NormalizeHeader(headers);
        return normalized.Contains("包号", StringComparison.OrdinalIgnoreCase) &&
               (normalized.Contains("预算金额", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("最高限价", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("控制价", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeQualificationTable(string headers)
    {
        var normalized = BidOpsEvidenceTableParser.NormalizeHeader(headers);
        return normalized.Contains("包号", StringComparison.OrdinalIgnoreCase) &&
               (normalized.Contains("资质要求", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("资格要求", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("业绩要求", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("人员要求", StringComparison.OrdinalIgnoreCase));
    }

    private static CommonColumns BuildCommonColumns(BidOpsExtractedTable table)
    {
        return new CommonColumns(
            ProjectIndex: BidOpsEvidenceTableParser.FindColumn(table.Headers, "项目编号", "采购编号", "招标编号", "批次编号"),
            ProjectNameIndex: BidOpsEvidenceTableParser.FindColumn(table.Headers, "项目名称", "采购项目名称", "招标项目名称"),
            LotIndex: BidOpsEvidenceTableParser.FindColumn(table.Headers, "分标编号", "标段编号", "分标号", "分标"),
            LotNameIndex: BidOpsEvidenceTableParser.FindColumn(table.Headers, "分标名称", "标段名称"),
            PackageIndex: BidOpsEvidenceTableParser.FindColumn(table.Headers, "包号", "包件号", "分包编号", "分包号", "标包号"),
            PackageNameIndex: BidOpsEvidenceTableParser.FindColumn(table.Headers, "包名称", "包件名称", "标包名称"),
            CategoryIndex: BidOpsEvidenceTableParser.FindColumn(table.Headers, "分类", "品类", "物资类别"),
            QuantityIndex: BidOpsEvidenceTableParser.FindColumn(table.Headers, "数量", "采购数量"));
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

    private sealed record CommonColumns(
        int ProjectIndex,
        int ProjectNameIndex,
        int LotIndex,
        int LotNameIndex,
        int PackageIndex,
        int PackageNameIndex,
        int CategoryIndex,
        int QuantityIndex);
}
