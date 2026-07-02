using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Staging;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Atlas.Modules.BidOps.Services;

public static class BidOpsReviewQualityEvaluator
{
    public static ReviewQualityEvaluation EvaluateNotice(
        NoticeStaging notice,
        IReadOnlyCollection<PackageStaging> packages,
        IReadOnlyCollection<RequirementStaging> requirements,
        IReadOnlyCollection<ProcurementDetailStaging>? procurementDetails = null)
    {
        ArgumentNullException.ThrowIfNull(notice);
        packages ??= Array.Empty<PackageStaging>();
        requirements ??= Array.Empty<RequirementStaging>();
        procurementDetails ??= Array.Empty<ProcurementDetailStaging>();

        var issues = new List<ReviewQualityIssueDraft>();
        if (!LooksLikeProcurementNotice(notice, packages, procurementDetails))
            return BuildEvaluation(issues);

        if (IsMissingValue(notice.ProjectCode))
        {
            issues.Add(new ReviewQualityIssueDraft(
                BidOpsReviewQualityIssueTypes.MissingProjectCode,
                ReviewQualityRiskLevel.Medium,
                nameof(NoticeStaging.ProjectCode),
                "采购编号/项目编号未识别，建议复核公告正文或附件表格。",
                Evidence: new Dictionary<string, object?>
                {
                    ["noticeStagingId"] = notice.Id,
                    ["noticeType"] = notice.NoticeType
                }));
        }

        if (packages.Count == 0)
        {
            issues.Add(new ReviewQualityIssueDraft(
                BidOpsReviewQualityIssueTypes.MissingLotOrPackage,
                ReviewQualityRiskLevel.High,
                "Packages",
                "前置公告未识别到分标/包件明细，无法形成可审核的包件闭环。",
                Evidence: new Dictionary<string, object?>
                {
                    ["noticeStagingId"] = notice.Id,
                    ["noticeType"] = notice.NoticeType
                }));
        }
        else
        {
            AddPackageIdentityIssues(packages, issues);
            AddDuplicatePackageIssues(packages, issues);
        }

        AddRequirementCoverageIssues(requirements, issues);
        AddProcurementDetailEvidenceIssues(procurementDetails, issues);
        AddAmountEvidenceIssues(packages, procurementDetails, issues);

        return BuildEvaluation(issues);
    }

    public static ReviewQualityEvaluation EvaluateOutcomeNotice(
        RawNotice raw,
        NoticeStaging? notice,
        IReadOnlyCollection<OutcomeSupplierRecord> records,
        IReadOnlyCollection<PackageStaging> packages)
    {
        ArgumentNullException.ThrowIfNull(raw);
        records ??= Array.Empty<OutcomeSupplierRecord>();
        packages ??= Array.Empty<PackageStaging>();

        var issues = new List<ReviewQualityIssueDraft>();
        if (!LooksLikeOutcomeNotice(raw, notice, records))
            return BuildEvaluation(issues);

        if (records.Count == 0)
        {
            issues.Add(new ReviewQualityIssueDraft(
                BidOpsReviewQualityIssueTypes.OriginalEvidenceMissing,
                ReviewQualityRiskLevel.High,
                "OutcomeSuppliers",
                "结果/候选公告未识别到可保存的厂家线索，无法形成采购闭环。",
                Evidence: new Dictionary<string, object?>
                {
                    ["rawNoticeId"] = raw.Id,
                    ["noticeType"] = FirstMeaningful(raw.NoticeType, notice?.NoticeType),
                    ["title"] = raw.Title
                }));
            return BuildEvaluation(issues);
        }

        var candidateNotice = LooksLikeCandidateNotice(raw, notice);
        foreach (var record in records.OrderBy(x => x.ExtractionOrder).ThenBy(x => x.Id))
        {
            AddOutcomeSupplierIdentityIssues(record, issues);
            AddOutcomeAmountEvidenceIssues(record, issues);
            AddOutcomePackageMatchIssues(record, packages, issues);

            if (candidateNotice &&
                string.Equals(record.OutcomeType, BidOpsOutcomeTypes.Candidate, StringComparison.OrdinalIgnoreCase) &&
                !record.Rank.HasValue)
            {
                issues.Add(new ReviewQualityIssueDraft(
                    BidOpsReviewQualityIssueTypes.MissingCandidateRank,
                    ReviewQualityRiskLevel.Medium,
                    nameof(OutcomeSupplierRecord.Rank),
                    "候选人公示缺少候选排名，建议复核公告表格顺序或原文排名。",
                    OutcomeSupplierRecordId: record.Id,
                    Evidence: OutcomeRecordEvidence(record)));
            }
        }

        return BuildEvaluation(issues);
    }

    private static void AddPackageIdentityIssues(
        IEnumerable<PackageStaging> packages,
        ICollection<ReviewQualityIssueDraft> issues)
    {
        foreach (var package in packages)
        {
            if (IsMissingValue(package.PackageNo))
            {
                issues.Add(new ReviewQualityIssueDraft(
                    BidOpsReviewQualityIssueTypes.MissingLotOrPackage,
                    ReviewQualityRiskLevel.Medium,
                    nameof(PackageStaging.PackageNo),
                    "包号未识别，后续与中标/候选公告匹配时容易错配。",
                    PackageStagingId: package.Id,
                    Evidence: PackageEvidence(package)));
            }

            if (IsMissingValue(package.PackageName))
            {
                issues.Add(new ReviewQualityIssueDraft(
                    BidOpsReviewQualityIssueTypes.MissingLotOrPackage,
                    ReviewQualityRiskLevel.Medium,
                    nameof(PackageStaging.PackageName),
                    "包名称未识别，建议复核采购一览表或项目概况表。",
                    PackageStagingId: package.Id,
                    Evidence: PackageEvidence(package)));
            }

            if (IsMissingValue(package.LotNo) && IsMissingValue(package.LotName))
            {
                issues.Add(new ReviewQualityIssueDraft(
                    BidOpsReviewQualityIssueTypes.MissingLotOrPackage,
                    ReviewQualityRiskLevel.Medium,
                    nameof(PackageStaging.LotNo),
                    "分标编号/分标名称均未识别，包件归属不清晰。",
                    PackageStagingId: package.Id,
                    Evidence: PackageEvidence(package)));
            }
        }
    }

    private static void AddDuplicatePackageIssues(
        IReadOnlyCollection<PackageStaging> packages,
        ICollection<ReviewQualityIssueDraft> issues)
    {
        var duplicateCompositeGroups = packages
            .Where(x => !IsMissingValue(x.PackageNo) && (!IsMissingValue(x.LotNo) || !IsMissingValue(x.LotName)))
            .GroupBy(x => $"{NormalizeKey(x.LotNo)}|{NormalizeKey(x.LotName)}|{NormalizeKey(x.PackageNo)}")
            .Where(x => x.Count() > 1);

        foreach (var group in duplicateCompositeGroups)
        {
            var groupItems = group.ToList();
            issues.Add(new ReviewQualityIssueDraft(
                BidOpsReviewQualityIssueTypes.DuplicatePackageIdentity,
                ReviewQualityRiskLevel.High,
                nameof(PackageStaging.PackageNo),
                "同一分标编号/分标名称/包号出现多条包件，建议复核是否为重复解析或缺少行项目区分。",
                Evidence: new Dictionary<string, object?>
                {
                    ["packageStagingIds"] = groupItems.Select(x => x.Id).ToArray(),
                    ["lotNo"] = groupItems[0].LotNo,
                    ["lotName"] = groupItems[0].LotName,
                    ["packageNo"] = groupItems[0].PackageNo,
                    ["count"] = groupItems.Count
                }));
        }

        var duplicatePackageNoWithoutLotGroups = packages
            .Where(x => !IsMissingValue(x.PackageNo) && IsMissingValue(x.LotNo) && IsMissingValue(x.LotName))
            .GroupBy(x => NormalizeKey(x.PackageNo))
            .Where(x => x.Count() > 1);

        foreach (var group in duplicatePackageNoWithoutLotGroups)
        {
            var groupItems = group.ToList();
            issues.Add(new ReviewQualityIssueDraft(
                BidOpsReviewQualityIssueTypes.DuplicatePackageIdentity,
                ReviewQualityRiskLevel.High,
                nameof(PackageStaging.PackageNo),
                "多个包件使用相同包号且缺少分标信息，无法可靠区分包件。",
                Evidence: new Dictionary<string, object?>
                {
                    ["packageStagingIds"] = groupItems.Select(x => x.Id).ToArray(),
                    ["packageNo"] = groupItems[0].PackageNo,
                    ["count"] = groupItems.Count
                }));
        }
    }

    private static void AddRequirementCoverageIssues(
        IReadOnlyCollection<RequirementStaging> requirements,
        ICollection<ReviewQualityIssueDraft> issues)
    {
        var missing = new List<string>();
        if (!requirements.Any(x => MatchesRequirement(x, "Qualification", "资质", "资格", "许可证", "证书")))
            missing.Add(BidOpsReviewQualityIssueTypes.MissingQualificationRequirement);
        if (!requirements.Any(x => MatchesRequirement(x, "Performance", "业绩", "合同案例", "类似项目")))
            missing.Add(BidOpsReviewQualityIssueTypes.MissingPerformanceRequirement);
        if (!requirements.Any(x => MatchesRequirement(x, "Personnel", "人员", "项目负责人", "负责人", "项目经理")))
            missing.Add(BidOpsReviewQualityIssueTypes.MissingPersonnelRequirement);

        if (missing.Count == 0)
            return;

        if (missing.Count == 3)
        {
            issues.Add(new ReviewQualityIssueDraft(
                BidOpsReviewQualityIssueTypes.MissingQualificationRequirement,
                ReviewQualityRiskLevel.Medium,
                "Requirements",
                "资质、业绩、人员要求均未识别，建议复核响应供应商专用资格要求表。",
                Evidence: new Dictionary<string, object?>
                {
                    ["missingRequirementTypes"] = missing.ToArray(),
                    ["requirementCount"] = requirements.Count
                }));
            return;
        }

        foreach (var issueType in missing)
        {
            issues.Add(new ReviewQualityIssueDraft(
                issueType,
                ReviewQualityRiskLevel.Low,
                "Requirements",
                $"{FormatMissingRequirementName(issueType)}未识别，建议抽查专用资格要求表。",
                Evidence: new Dictionary<string, object?>
                {
                    ["missingRequirementType"] = issueType,
                    ["requirementCount"] = requirements.Count
                }));
        }
    }

    private static void AddProcurementDetailEvidenceIssues(
        IReadOnlyCollection<ProcurementDetailStaging> procurementDetails,
        ICollection<ReviewQualityIssueDraft> issues)
    {
        var missingOriginalRows = procurementDetails
            .Where(x => string.IsNullOrWhiteSpace(x.OriginalRowJson))
            .Select(x => x.Id)
            .Take(20)
            .ToArray();
        if (missingOriginalRows.Length == 0)
            return;

        issues.Add(new ReviewQualityIssueDraft(
            BidOpsReviewQualityIssueTypes.OriginalEvidenceMissing,
            ReviewQualityRiskLevel.Medium,
            nameof(ProcurementDetailStaging.OriginalRowJson),
            "部分采购明细缺少原始行 JSON，异常定位和人工追溯会受影响。",
            Evidence: new Dictionary<string, object?>
            {
                ["sampleProcurementDetailStagingIds"] = missingOriginalRows,
                ["sampleCount"] = missingOriginalRows.Length,
                ["totalDetailCount"] = procurementDetails.Count
            }));
    }

    private static void AddAmountEvidenceIssues(
        IReadOnlyCollection<PackageStaging> packages,
        IReadOnlyCollection<ProcurementDetailStaging> procurementDetails,
        ICollection<ReviewQualityIssueDraft> issues)
    {
        var packagesById = packages.ToDictionary(x => x.Id);
        foreach (var detail in procurementDetails.Where(x => !string.IsNullOrWhiteSpace(x.OriginalRowJson)))
        {
            var amountFields = GetNormalizedAmountFields(detail, packagesById).ToList();
            if (amountFields.Count == 0)
                continue;

            foreach (var cell in ExtractAmountCells(detail))
            {
                if (cell.IsRateLike)
                {
                    var matchedRateAmount = amountFields.FirstOrDefault(x => AmountClose(x.Value, cell.NumericValue));
                    if (!string.IsNullOrWhiteSpace(matchedRateAmount.FieldName))
                    {
                        issues.Add(new ReviewQualityIssueDraft(
                            BidOpsReviewQualityIssueTypes.RateOrDiscountInAmountColumn,
                            ReviewQualityRiskLevel.High,
                            matchedRateAmount.FieldName,
                            "百分比、折扣率、税率或评分权重疑似进入金额字段，应按费率字段保留或置空金额。",
                            PackageStagingId: detail.PackageStagingId,
                            ProcurementDetailStagingId: detail.Id,
                            Evidence: AmountEvidence(detail, cell, matchedRateAmount.Value, null)));
                    }

                    continue;
                }

                if (!cell.HasTenThousandUnit)
                    continue;

                var expectedYuan = Math.Round(cell.NumericValue * 10000m, 2);
                if (amountFields.Any(x => AmountClose(x.Value, expectedYuan)))
                    continue;

                var matchedUnscaledAmount = amountFields.FirstOrDefault(x => AmountClose(x.Value, cell.NumericValue));
                if (string.IsNullOrWhiteSpace(matchedUnscaledAmount.FieldName))
                    continue;

                issues.Add(new ReviewQualityIssueDraft(
                    BidOpsReviewQualityIssueTypes.AmbiguousAmountUnit,
                    ReviewQualityRiskLevel.High,
                    matchedUnscaledAmount.FieldName,
                    "原始表头/单元格明确为万元，但暂存金额疑似未乘以 10000。",
                    PackageStagingId: detail.PackageStagingId,
                    ProcurementDetailStagingId: detail.Id,
                    Evidence: AmountEvidence(detail, cell, matchedUnscaledAmount.Value, expectedYuan)));
            }
        }
    }

    private static void AddOutcomeSupplierIdentityIssues(
        OutcomeSupplierRecord record,
        ICollection<ReviewQualityIssueDraft> issues)
    {
        if (IsMissingValue(record.SupplierName))
        {
            issues.Add(new ReviewQualityIssueDraft(
                BidOpsReviewQualityIssueTypes.MissingSupplierName,
                ReviewQualityRiskLevel.High,
                nameof(OutcomeSupplierRecord.SupplierName),
                "中标/候选厂家名称缺失，结果记录无法用于供应商闭环分析。",
                OutcomeSupplierRecordId: record.Id,
                Evidence: OutcomeRecordEvidence(record)));
        }

        if (IsMissingValue(record.LotNo) && IsMissingValue(record.PackageNo))
        {
            issues.Add(new ReviewQualityIssueDraft(
                BidOpsReviewQualityIssueTypes.MissingLotOrPackage,
                ReviewQualityRiskLevel.High,
                nameof(OutcomeSupplierRecord.PackageNo),
                "分标编号和包号均缺失，无法可靠匹配前置公告包件。",
                OutcomeSupplierRecordId: record.Id,
                Evidence: OutcomeRecordEvidence(record)));
        }
    }

    private static void AddOutcomeAmountEvidenceIssues(
        OutcomeSupplierRecord record,
        ICollection<ReviewQualityIssueDraft> issues)
    {
        if (!record.AwardAmount.HasValue || string.IsNullOrWhiteSpace(record.EvidenceText))
            return;

        var amount = record.AwardAmount.Value;
        foreach (var evidenceNumber in ExtractEvidenceNumbers(record.EvidenceText))
        {
            if (evidenceNumber.IsRateLike && AmountClose(amount, evidenceNumber.Value))
            {
                issues.Add(new ReviewQualityIssueDraft(
                    BidOpsReviewQualityIssueTypes.RateOrDiscountInAmountColumn,
                    ReviewQualityRiskLevel.High,
                    nameof(OutcomeSupplierRecord.AwardAmount),
                    "公告证据显示为百分比、折扣率、费率或得分，但进入了中标金额字段。",
                    OutcomeSupplierRecordId: record.Id,
                    Evidence: OutcomeAmountEvidence(record, evidenceNumber, null)));
                continue;
            }

            if (!evidenceNumber.HasTenThousandUnit)
                continue;

            var expectedYuan = Math.Round(evidenceNumber.Value * 10000m, 2);
            if (AmountClose(amount, expectedYuan))
                continue;

            if (!AmountClose(amount, evidenceNumber.Value))
                continue;

            issues.Add(new ReviewQualityIssueDraft(
                BidOpsReviewQualityIssueTypes.AmbiguousAmountUnit,
                ReviewQualityRiskLevel.High,
                nameof(OutcomeSupplierRecord.AwardAmount),
                "公告证据明确为万元/万，但中标金额疑似未乘以 10000。",
                OutcomeSupplierRecordId: record.Id,
                Evidence: OutcomeAmountEvidence(record, evidenceNumber, expectedYuan)));
        }
    }

    private static void AddOutcomePackageMatchIssues(
        OutcomeSupplierRecord record,
        IReadOnlyCollection<PackageStaging> packages,
        ICollection<ReviewQualityIssueDraft> issues)
    {
        if (record.TenderPackageId.HasValue || packages.Count == 0)
            return;

        var matches = FindPackageMatches(record, packages).ToList();
        if (matches.Count == 1)
            return;

        if (matches.Count > 1)
        {
            issues.Add(new ReviewQualityIssueDraft(
                BidOpsReviewQualityIssueTypes.LifecycleMatchConflict,
                ReviewQualityRiskLevel.High,
                nameof(OutcomeSupplierRecord.PackageNo),
                "中标/候选结果可匹配到多个采购包件；仅凭包号和成交人无法唯一定位，建议补充分标编号或分标名称后再确认。",
                OutcomeSupplierRecordId: record.Id,
                Evidence: new Dictionary<string, object?>
                {
                    ["outcomeSupplierRecordId"] = record.Id,
                    ["lotNo"] = record.LotNo,
                    ["lotName"] = record.LotName,
                    ["packageNo"] = record.PackageNo,
                    ["supplierName"] = record.SupplierName,
                    ["matchedPackageKeys"] = matches.Select(x => new
                    {
                        x.Id,
                        x.LotNo,
                        x.LotName,
                        x.PackageNo,
                        x.PackageName
                    }).ToArray(),
                    ["matchedPackageStagingIds"] = matches.Select(x => x.Id).ToArray()
                }));
            return;
        }

        issues.Add(new ReviewQualityIssueDraft(
            BidOpsReviewQualityIssueTypes.LifecycleMatchMissing,
            ReviewQualityRiskLevel.Medium,
            nameof(OutcomeSupplierRecord.PackageNo),
            "中标/候选结果暂未匹配到前置公告包件，闭环分析可能缺少服务内容和资质要求。",
            OutcomeSupplierRecordId: record.Id,
            Evidence: OutcomeRecordEvidence(record)));
    }

    private static ReviewQualityEvaluation BuildEvaluation(IReadOnlyList<ReviewQualityIssueDraft> issues)
    {
        var score = 100;
        foreach (var issue in issues)
        {
            score -= issue.Severity switch
            {
                ReviewQualityRiskLevel.High => 30,
                ReviewQualityRiskLevel.Medium => 15,
                ReviewQualityRiskLevel.Low => 5,
                _ => 0
            };
        }

        score = Math.Clamp(score, 0, 100);
        var highRiskCount = issues.Count(x => x.Severity == ReviewQualityRiskLevel.High);
        var riskLevel = score < 60 || highRiskCount > 0
            ? ReviewQualityRiskLevel.High
            : score < 85 || issues.Any(x => x.Severity == ReviewQualityRiskLevel.Medium)
                ? ReviewQualityRiskLevel.Medium
                : ReviewQualityRiskLevel.Low;

        var recommendation = riskLevel switch
        {
            ReviewQualityRiskLevel.Low => ReviewRecommendation.BatchConfirmCandidate,
            ReviewQualityRiskLevel.High when issues.Any(IsLikelyReparseIssue) => ReviewRecommendation.NeedsReparse,
            _ => ReviewRecommendation.NeedsReview
        };

        return new ReviewQualityEvaluation(
            score,
            riskLevel,
            issues.Count,
            highRiskCount,
            recommendation,
            issues);
    }

    private static bool IsLikelyReparseIssue(ReviewQualityIssueDraft issue)
    {
        return issue.IssueType == BidOpsReviewQualityIssueTypes.MissingLotOrPackage ||
               issue.IssueType == BidOpsReviewQualityIssueTypes.AmbiguousAmountUnit ||
               issue.IssueType == BidOpsReviewQualityIssueTypes.RateOrDiscountInAmountColumn ||
               issue.IssueType == BidOpsReviewQualityIssueTypes.OriginalEvidenceMissing ||
               issue.IssueType == BidOpsReviewQualityIssueTypes.DuplicatePackageIdentity;
    }

    private static bool LooksLikeProcurementNotice(
        NoticeStaging notice,
        IReadOnlyCollection<PackageStaging> packages,
        IReadOnlyCollection<ProcurementDetailStaging> procurementDetails)
    {
        if (packages.Count > 0 || procurementDetails.Count > 0)
            return true;

        var type = NormalizeKey(notice.NoticeType);
        return type.Contains("PROCUREMENT", StringComparison.Ordinal) ||
               type.Contains("TENDER", StringComparison.Ordinal) ||
               type.Contains("采购", StringComparison.Ordinal) ||
               type.Contains("招标", StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, object?> PackageEvidence(PackageStaging package)
    {
        return new Dictionary<string, object?>
        {
            ["packageStagingId"] = package.Id,
            ["lotNo"] = package.LotNo,
            ["lotName"] = package.LotName,
            ["packageNo"] = package.PackageNo,
            ["packageName"] = package.PackageName
        };
    }

    private static IReadOnlyDictionary<string, object?> OutcomeRecordEvidence(OutcomeSupplierRecord record)
    {
        return new Dictionary<string, object?>
        {
            ["outcomeSupplierRecordId"] = record.Id,
            ["rawNoticeId"] = record.RawNoticeId,
            ["noticeType"] = record.NoticeType,
            ["projectCode"] = record.ProjectCode,
            ["projectName"] = record.ProjectName,
            ["lotNo"] = record.LotNo,
            ["lotName"] = record.LotName,
            ["packageNo"] = record.PackageNo,
            ["packageName"] = record.PackageName,
            ["supplierName"] = record.SupplierName,
            ["rank"] = record.Rank,
            ["awardAmount"] = record.AwardAmount,
            ["evidenceText"] = record.EvidenceText
        };
    }

    private static IReadOnlyDictionary<string, object?> OutcomeAmountEvidence(
        OutcomeSupplierRecord record,
        EvidenceNumber evidenceNumber,
        decimal? expectedYuan)
    {
        return new Dictionary<string, object?>
        {
            ["outcomeSupplierRecordId"] = record.Id,
            ["rawNoticeId"] = record.RawNoticeId,
            ["fieldName"] = nameof(OutcomeSupplierRecord.AwardAmount),
            ["awardAmount"] = record.AwardAmount,
            ["sourceNumericValue"] = evidenceNumber.Value,
            ["sourceToken"] = evidenceNumber.Token,
            ["expectedYuan"] = expectedYuan,
            ["evidenceText"] = record.EvidenceText
        };
    }

    private static IReadOnlyDictionary<string, object?> AmountEvidence(
        ProcurementDetailStaging detail,
        AmountCell cell,
        decimal normalizedAmount,
        decimal? expectedYuan)
    {
        return new Dictionary<string, object?>
        {
            ["procurementDetailStagingId"] = detail.Id,
            ["packageStagingId"] = detail.PackageStagingId,
            ["sourceSheetName"] = detail.SourceSheetName,
            ["tableIndex"] = detail.TableIndex,
            ["rowIndex"] = detail.RowIndex,
            ["originalHeader"] = cell.Header,
            ["originalValue"] = cell.Value,
            ["sourceNumericValue"] = cell.NumericValue,
            ["normalizedAmount"] = normalizedAmount,
            ["expectedYuan"] = expectedYuan
        };
    }

    private static IEnumerable<(string FieldName, decimal Value)> GetNormalizedAmountFields(
        ProcurementDetailStaging detail,
        IReadOnlyDictionary<long, PackageStaging> packagesById)
    {
        foreach (var field in new (string FieldName, decimal? Value)[]
        {
            (nameof(ProcurementDetailStaging.ProcurementAmount), detail.ProcurementAmount),
            (nameof(ProcurementDetailStaging.BudgetAmount), detail.BudgetAmount),
            (nameof(ProcurementDetailStaging.ItemEstimatedAmount), detail.ItemEstimatedAmount),
            (nameof(ProcurementDetailStaging.PackageEstimatedAmount), detail.PackageEstimatedAmount),
            (nameof(ProcurementDetailStaging.MaxPrice), detail.MaxPrice),
            (nameof(ProcurementDetailStaging.ResponseGuaranteeAmount), detail.ResponseGuaranteeAmount)
        })
        {
            if (field.Value.HasValue && field.Value.Value > 0)
                yield return (field.FieldName, field.Value.Value);
        }

        if (!detail.PackageStagingId.HasValue || !packagesById.TryGetValue(detail.PackageStagingId.Value, out var package))
            yield break;

        if (package.BudgetAmount.HasValue && package.BudgetAmount.Value > 0)
            yield return (nameof(PackageStaging.BudgetAmount), package.BudgetAmount.Value);
        if (package.MaxPrice.HasValue && package.MaxPrice.Value > 0)
            yield return (nameof(PackageStaging.MaxPrice), package.MaxPrice.Value);
    }

    private static IEnumerable<AmountCell> ExtractAmountCells(ProcurementDetailStaging detail)
    {
        JsonDocument? document;
        try
        {
            document = JsonDocument.Parse(detail.OriginalRowJson);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (document)
        {
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var property in document.RootElement.EnumerateObject())
        {
            var valueText = JsonElementToText(property.Value);
            if (!TryReadDecimal(valueText, out var numericValue))
                continue;

            var header = property.Name;
            var rateLike = LooksLikeRateHeader(header);
            var tenThousandUnit = ContainsTenThousandUnit(header) || ContainsTenThousandUnit(valueText);
            if (!rateLike && !tenThousandUnit && !LooksLikeMoneyHeader(header))
                continue;

            yield return new AmountCell(header, valueText, numericValue, tenThousandUnit, rateLike);
        }
        }
    }

    private static IEnumerable<EvidenceNumber> ExtractEvidenceNumbers(string value)
    {
        var rateContext = LooksLikeRateHeader(value);
        foreach (Match match in Regex.Matches(value, @"-?\d+(?:\.\d+)?\s*(万元|万人民币|万|元|%|％)?", RegexOptions.CultureInvariant))
        {
            if (!TryReadDecimal(match.Value, out var numericValue))
                continue;

            var token = match.Value.Trim();
            var hasTenThousandUnit = ContainsTenThousandUnit(token);
            var isRateLike = rateContext ||
                token.Contains('%', StringComparison.Ordinal) ||
                token.Contains('％', StringComparison.Ordinal);
            yield return new EvidenceNumber(token, numericValue, hasTenThousandUnit, isRateLike);
        }
    }

    private static IEnumerable<PackageStaging> FindPackageMatches(
        OutcomeSupplierRecord record,
        IEnumerable<PackageStaging> packages)
    {
        var packageList = packages.ToList();
        var lotNo = NormalizeKey(record.LotNo);
        var lotName = NormalizeKey(record.LotName);
        var packageNo = NormalizeKey(record.PackageNo);
        var packageName = NormalizeKey(record.PackageName);

        if (!string.IsNullOrWhiteSpace(packageNo))
        {
            var byPackageNo = packageList
                .Where(x => NormalizeKey(x.PackageNo) == packageNo)
                .ToList();
            if (!string.IsNullOrWhiteSpace(lotNo) || !string.IsNullOrWhiteSpace(lotName))
            {
                var byLotContext = byPackageNo
                    .Where(x => LotContextCompatible(lotNo, lotName, NormalizeKey(x.LotNo), NormalizeKey(x.LotName)))
                    .ToList();
                return byLotContext;
            }

            return byPackageNo;
        }

        if (string.IsNullOrWhiteSpace(packageName))
            return [];

        var byPackageName = packageList
            .Where(x => NormalizeKey(x.PackageName) == packageName)
            .ToList();
        if (!string.IsNullOrWhiteSpace(lotNo) || !string.IsNullOrWhiteSpace(lotName))
        {
            var byLotContext = byPackageName
                .Where(x => LotContextCompatible(lotNo, lotName, NormalizeKey(x.LotNo), NormalizeKey(x.LotName)))
                .ToList();
            return byLotContext;
        }

        return byPackageName;
    }

    private static bool LotContextCompatible(
        string recordLotNo,
        string recordLotName,
        string packageLotNo,
        string packageLotName)
    {
        var matchedAnyLotContext = false;
        if (!string.IsNullOrWhiteSpace(recordLotNo) && !string.IsNullOrWhiteSpace(packageLotNo))
        {
            if (!string.Equals(recordLotNo, packageLotNo, StringComparison.Ordinal))
                return false;

            matchedAnyLotContext = true;
        }

        if (!string.IsNullOrWhiteSpace(recordLotName) && !string.IsNullOrWhiteSpace(packageLotName))
        {
            if (!string.Equals(recordLotName, packageLotName, StringComparison.Ordinal))
                return false;

            matchedAnyLotContext = true;
        }

        return matchedAnyLotContext;
    }

    private static bool LooksLikeOutcomeNotice(
        RawNotice raw,
        NoticeStaging? notice,
        IReadOnlyCollection<OutcomeSupplierRecord> records)
    {
        if (records.Count > 0)
            return true;

        var text = string.Join(' ', raw.NoticeType, raw.Title, notice?.NoticeType, notice?.ProjectName);
        return ContainsAny(text, "Award", "Result", "Candidate", "Shortlist", "中标", "成交", "结果", "候选", "入围");
    }

    private static bool LooksLikeCandidateNotice(RawNotice raw, NoticeStaging? notice)
    {
        var text = string.Join(' ', raw.NoticeType, raw.Title, notice?.NoticeType, notice?.ProjectName);
        return ContainsAny(text, "Candidate", "Shortlist", "候选", "入围");
    }

    private static string FirstMeaningful(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!IsMissingValue(value))
                return value!.Trim();
        }

        return string.Empty;
    }

    private static string JsonElementToText(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.ToString()
        };
    }

    private static bool TryReadDecimal(string value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var chars = value
            .Where(x => char.IsDigit(x) || x is '.' or '-')
            .ToArray();
        if (chars.Length == 0)
            return false;

        return decimal.TryParse(new string(chars), out result);
    }

    private static bool MatchesRequirement(RequirementStaging requirement, string type, params string[] keywords)
    {
        if (string.Equals(requirement.RequirementType, type, StringComparison.OrdinalIgnoreCase))
            return true;

        var text = string.Join(
            ' ',
            requirement.RequirementType,
            requirement.OriginalText,
            requirement.RequiredEvidenceType,
            requirement.AiExplanation);
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatMissingRequirementName(string issueType)
    {
        return issueType switch
        {
            BidOpsReviewQualityIssueTypes.MissingQualificationRequirement => "资质要求",
            BidOpsReviewQualityIssueTypes.MissingPerformanceRequirement => "业绩要求",
            BidOpsReviewQualityIssueTypes.MissingPersonnelRequirement => "人员要求",
            _ => "要求项"
        };
    }

    private static bool LooksLikeMoneyHeader(string value)
    {
        if (LooksLikeRateHeader(value))
            return false;

        return ContainsAny(value, "金额", "限价", "预算", "估算", "报价", "保证金", "价格", "采购金额");
    }

    private static bool LooksLikeRateHeader(string value)
    {
        return ContainsAny(value, "%", "％", "折扣", "折扣率", "费率", "税率", "比例", "权重", "评分", "分权重", "价格参数");
    }

    private static bool ContainsTenThousandUnit(string value)
    {
        var normalized = NormalizeText(value).Replace(" ", string.Empty, StringComparison.Ordinal);
        return normalized.Contains("万元", StringComparison.Ordinal) ||
               normalized.Contains("万人民币", StringComparison.Ordinal) ||
               normalized.Contains("金额(万", StringComparison.Ordinal) ||
               normalized.Contains("金额（万", StringComparison.Ordinal) ||
               normalized.Contains("限价(万", StringComparison.Ordinal) ||
               normalized.Contains("限价（万", StringComparison.Ordinal);
    }

    private static bool ContainsAny(string value, params string[] keywords)
    {
        return keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool AmountClose(decimal left, decimal right)
    {
        return Math.Abs(left - right) <= 0.01m;
    }

    private static bool IsMissingValue(string? value)
    {
        var normalized = NormalizeKey(value);
        return string.IsNullOrWhiteSpace(normalized) ||
               normalized is "UNSPECIFIED" or "UNKNOWN" or "N/A" or "NA" or "NULL" or "-" or "/" or "未识别" or "无";
    }

    private static string NormalizeKey(string? value)
    {
        return NormalizeText(value)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("　", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private sealed record AmountCell(
        string Header,
        string Value,
        decimal NumericValue,
        bool HasTenThousandUnit,
        bool IsRateLike);

    private sealed record EvidenceNumber(
        string Token,
        decimal Value,
        bool HasTenThousandUnit,
        bool IsRateLike);
}

public sealed record ReviewQualityEvaluation(
    int QualityScore,
    ReviewQualityRiskLevel RiskLevel,
    int QualityIssueCount,
    int HighRiskIssueCount,
    ReviewRecommendation ReviewRecommendation,
    IReadOnlyList<ReviewQualityIssueDraft> Issues);

public sealed record ReviewQualityIssueDraft(
    string IssueType,
    ReviewQualityRiskLevel Severity,
    string FieldName,
    string Message,
    long? PackageStagingId = null,
    long? OutcomeSupplierRecordId = null,
    long? ProcurementDetailStagingId = null,
    IReadOnlyDictionary<string, object?>? Evidence = null);
