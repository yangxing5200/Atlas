namespace Atlas.Modules.BidOps.Ai;

public sealed class MockBidOpsAiExtractionService : IBidOpsAiExtractionService
{
    public Task<BidOpsNoticeExtract> ExtractAsync(
        string title,
        string text,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var projectName = string.IsNullOrWhiteSpace(title)
            ? "公开标讯样例项目"
            : title.Trim();

        var package = new BidOpsPackageExtract(
            LotNo: "LOT-1",
            LotName: "默认标段",
            PackageNo: "PKG-1",
            PackageName: projectName,
            Category: DetectCategory(text),
            Quantity: null,
            Unit: string.Empty,
            BudgetAmount: 100000m,
            MaxPrice: 100000m,
            DeliveryPlace: "以公告为准",
            DeliveryPeriod: "以公告为准",
            Confidence: 0.78m,
            Requirements:
            [
                new BidOpsRequirementExtract(
                    "Qualification",
                    "供应商应具备履行合同所必需的设备和专业技术能力。",
                    null,
                    true,
                    false,
                    "QualificationDocument",
                    "Medium",
                    "Mock rule detected a common qualification requirement.",
                    0.74m),
                new BidOpsRequirementExtract(
                    "Deadline",
                    "投标文件递交截止时间以公告载明时间为准。",
                    null,
                    true,
                    true,
                    "BidDocument",
                    "High",
                    "Deadline requirements can become rejection risks if missed.",
                    0.71m)
            ]);

        var extract = new BidOpsNoticeExtract(
            NoticeType: "TenderAnnouncement",
            ProjectName: projectName,
            ProjectCode: $"MOCK-{Math.Abs(projectName.GetHashCode()):000000}",
            BuyerName: "示例采购单位",
            AgencyName: "示例代理机构",
            Region: "公开来源",
            BudgetAmount: 100000m,
            PublishTime: now,
            SignupDeadline: now.AddDays(5),
            BidDeadline: now.AddDays(10),
            OpenBidTime: now.AddDays(10).AddHours(2),
            Confidence: 0.8m,
            Packages: [package]);

        return Task.FromResult(extract);
    }

    private static string DetectCategory(string text)
    {
        if (text.Contains("服务", StringComparison.OrdinalIgnoreCase))
            return "Service";

        if (text.Contains("施工", StringComparison.OrdinalIgnoreCase))
            return "Construction";

        return "Goods";
    }
}
