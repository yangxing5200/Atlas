namespace Atlas.Modules.BidOps.Ai.Evidence;

public static class BidOpsCandidateEvidenceParser
{
    public static IReadOnlyList<CandidateEvidence> Extract(IReadOnlyList<BidOpsEvidenceDocument> documents)
    {
        var results = new List<CandidateEvidence>();
        foreach (var document in documents)
            ExtractTables(document, results);

        return results
            .Where(x => !string.IsNullOrWhiteSpace(x.SupplierName))
            .GroupBy(x => new
            {
                Project = x.ProjectCode ?? string.Empty,
                Package = x.NormalizedPackageNo ?? string.Empty,
                Supplier = BidOpsSupplierNameNormalizer.NormalizeForMatch(x.SupplierName),
                Rank = x.Rank
            })
            .Select(x => x.OrderByDescending(item => item.Confidence).First())
            .ToList();
    }

    private static void ExtractTables(
        BidOpsEvidenceDocument document,
        List<CandidateEvidence> results)
    {
        var projectCodeFallback = BidOpsEvidenceText.ExtractProjectCode(document.Text);
        var projectNameFallback = FirstNonEmpty(BidOpsEvidenceText.ExtractProjectName(document.Text), document.Title);

        foreach (var table in BidOpsEvidenceTableParser.Parse(document.Text))
        {
            if (TryExtractHorizontalTop3(document, table, projectCodeFallback, projectNameFallback, results))
                continue;

            ExtractRankingTable(document, table, projectCodeFallback, projectNameFallback, results);
        }
    }

    private static bool TryExtractHorizontalTop3(
        BidOpsEvidenceDocument document,
        BidOpsExtractedTable table,
        string projectCodeFallback,
        string projectNameFallback,
        List<CandidateEvidence> results)
    {
        var packageIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "包号", "包件号", "分包编号", "分包号", "标包号");
        var firstCandidateIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "第一候选人", "第一中标候选人", "第一成交候选人", "第一推荐候选人");
        if (packageIndex < 0 || firstCandidateIndex < 0)
            return false;

        var projectIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "项目编号", "采购编号", "招标编号", "批次编号");
        var projectNameIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "项目名称", "采购项目名称", "招标项目名称");
        var lotIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "分标编号", "标段编号", "分标号", "分标");
        var packageNameIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "包名称", "包件名称", "标包名称");
        var candidateColumns = new[]
        {
            new CandidateColumn(1, firstCandidateIndex, BidOpsEvidenceTableParser.FindColumn(table.Headers, "第一报价", "第一候选人报价", "第一最终报价")),
            new CandidateColumn(2, BidOpsEvidenceTableParser.FindColumn(table.Headers, "第二候选人", "第二中标候选人", "第二成交候选人", "第二推荐候选人"), BidOpsEvidenceTableParser.FindColumn(table.Headers, "第二报价", "第二候选人报价", "第二最终报价")),
            new CandidateColumn(3, BidOpsEvidenceTableParser.FindColumn(table.Headers, "第三候选人", "第三中标候选人", "第三成交候选人", "第三推荐候选人"), BidOpsEvidenceTableParser.FindColumn(table.Headers, "第三报价", "第三候选人报价", "第三最终报价"))
        };

        foreach (var row in table.Rows)
        {
            var packageNo = BidOpsEvidenceTableParser.GetCell(row, packageIndex);
            if (string.IsNullOrWhiteSpace(packageNo))
                continue;

            foreach (var candidate in candidateColumns.Where(x => x.SupplierIndex >= 0))
            {
                var supplier = BidOpsSupplierNameNormalizer.Clean(BidOpsEvidenceTableParser.GetCell(row, candidate.SupplierIndex));
                if (string.IsNullOrWhiteSpace(supplier))
                    continue;

                var quote = candidate.AmountIndex >= 0
                    ? BidOpsMoneyNormalizer.TryNormalize(BidOpsEvidenceTableParser.GetCell(row, candidate.AmountIndex))
                    : null;

                results.Add(new CandidateEvidence(
                    ProjectCode: EmptyToNull(FirstNonEmpty(BidOpsEvidenceTableParser.GetCell(row, projectIndex), projectCodeFallback)),
                    ProjectName: EmptyToNull(FirstNonEmpty(BidOpsEvidenceTableParser.GetCell(row, projectNameIndex), projectNameFallback)),
                    LotNo: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, lotIndex)),
                    PackageNo: packageNo,
                    NormalizedPackageNo: EmptyToNull(BidOpsPackageNoNormalizer.Normalize(packageNo)),
                    PackageName: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, packageNameIndex)),
                    SupplierName: supplier,
                    Rank: candidate.Rank,
                    FinalQuoteAmount: quote,
                    Quality: null,
                    Duration: null,
                    QualificationStatus: null,
                    EvaluationText: null,
                    Evidence: document.Source with
                    {
                        TableIndex = table.TableIndex,
                        RowIndex = row.RowIndex,
                        ColumnIndex = candidate.SupplierIndex,
                        EvidenceText = row.RawText
                    },
                    Confidence: quote.HasValue ? 0.93 : 0.86));
            }
        }

        return true;
    }

    private static void ExtractRankingTable(
        BidOpsEvidenceDocument document,
        BidOpsExtractedTable table,
        string projectCodeFallback,
        string projectNameFallback,
        List<CandidateEvidence> results)
    {
        var supplierIndex = BidOpsEvidenceTableParser.FindColumn(
            table.Headers,
            "推荐候选人",
            "中标候选人",
            "成交候选人",
            "应答人",
            "投标人名称",
            "投标人",
            "供应商名称",
            "供应商");
        var packageIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "包号", "包件号", "分包编号", "分包号", "标包号");
        if (supplierIndex < 0 || packageIndex < 0)
            return;

        var projectIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "项目编号", "采购编号", "招标编号", "批次编号");
        var projectNameIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "项目名称", "采购项目名称", "招标项目名称");
        var lotIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "分标编号", "标段编号", "分标号", "分标");
        var packageNameIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "包名称", "包件名称", "标包名称");
        var rankIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "排名", "排序", "推荐顺序", "名次");
        var quoteIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "最终报价", "应答报价", "投标报价", "评审价", "报价");
        var qualityIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "质量", "质量标准");
        var durationIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "工期", "服务期", "交货期");
        var qualificationIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "资格条件", "资格", "资质", "资格能力");
        var evaluationIndex = BidOpsEvidenceTableParser.FindColumn(table.Headers, "评标情况", "评审情况", "评审");

        var currentProjectCode = projectCodeFallback;
        var currentProjectName = projectNameFallback;
        var currentLotNo = string.Empty;
        var currentPackageNo = string.Empty;
        var currentPackageName = string.Empty;

        foreach (var row in table.Rows)
        {
            currentProjectCode = FillDown(currentProjectCode, BidOpsEvidenceTableParser.GetCell(row, projectIndex));
            currentProjectName = FillDown(currentProjectName, BidOpsEvidenceTableParser.GetCell(row, projectNameIndex));
            currentLotNo = FillDown(currentLotNo, BidOpsEvidenceTableParser.GetCell(row, lotIndex));
            currentPackageNo = FillDown(currentPackageNo, BidOpsEvidenceTableParser.GetCell(row, packageIndex));
            currentPackageName = FillDown(currentPackageName, BidOpsEvidenceTableParser.GetCell(row, packageNameIndex));

            var supplier = BidOpsSupplierNameNormalizer.Clean(BidOpsEvidenceTableParser.GetCell(row, supplierIndex));
            if (string.IsNullOrWhiteSpace(supplier) || string.IsNullOrWhiteSpace(currentPackageNo))
                continue;

            var rank = BidOpsEvidenceText.ParseRank(BidOpsEvidenceTableParser.GetCell(row, rankIndex));
            var quote = quoteIndex >= 0
                ? BidOpsMoneyNormalizer.TryNormalize(BidOpsEvidenceTableParser.GetCell(row, quoteIndex))
                : BidOpsMoneyNormalizer.TryNormalize(row.RawText);

            results.Add(new CandidateEvidence(
                ProjectCode: EmptyToNull(currentProjectCode),
                ProjectName: EmptyToNull(currentProjectName),
                LotNo: EmptyToNull(currentLotNo),
                PackageNo: currentPackageNo,
                NormalizedPackageNo: EmptyToNull(BidOpsPackageNoNormalizer.Normalize(currentPackageNo)),
                PackageName: EmptyToNull(currentPackageName),
                SupplierName: supplier,
                Rank: rank,
                FinalQuoteAmount: quote,
                Quality: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, qualityIndex)),
                Duration: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, durationIndex)),
                QualificationStatus: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, qualificationIndex)),
                EvaluationText: EmptyToNull(BidOpsEvidenceTableParser.GetCell(row, evaluationIndex)),
                Evidence: document.Source with
                {
                    TableIndex = table.TableIndex,
                    RowIndex = row.RowIndex,
                    ColumnIndex = supplierIndex,
                    EvidenceText = row.RawText
                },
                Confidence: quote.HasValue ? 0.91 : 0.82));
        }
    }

    private static string FillDown(string current, string next)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(next);
        return string.IsNullOrWhiteSpace(cleaned) ? current : cleaned;
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

    private sealed record CandidateColumn(int Rank, int SupplierIndex, int AmountIndex);
}
