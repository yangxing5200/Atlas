namespace Atlas.BackgroundTasks.Operations;

public static class BackgroundJobDisplayNames
{
    private static readonly IReadOnlyDictionary<string, string> JobTypeNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tenant.cache-warmup"] = "租户缓存预热",
            ["export.generate"] = "导出文件生成",
            ["bidops.raw.manual-url-import"] = "BidOps 手动导入公告",
            ["bidops.raw.attachment-backfill"] = "BidOps 历史附件补齐",
            ["bidops.crawl.mock-scan"] = "BidOps 模拟采集扫描",
            ["bidops.crawl.state-grid-ecp-scan"] = "BidOps 国家电网采集扫描",
            ["bidops.document.attachment-process"] = "BidOps 附件下载与文本提取",
            ["bidops.ai.structured-parse"] = "BidOps 公告结构化解析",
            ["bidops.ai.mock-parse"] = "BidOps 模拟 AI 解析",
            ["bidops.opportunity.value-assessment"] = "BidOps 商机价值评估",
            ["bidops.opportunity.deadline-reminder"] = "BidOps 商机截止提醒",
            ["bidops.opportunity.watch-reminder"] = "BidOps 关注商机提醒",
            ["bidops.opportunity.stale-state-scan"] = "BidOps 商机状态巡检",
            ["bidops.supplier.evidence-expiry-scan"] = "BidOps 厂家资质到期扫描",
            ["bidops.matching.supplier-match-run"] = "BidOps 厂家匹配运行",
            ["bidops.outcome.supplier-extract"] = "BidOps 中标/候选厂家提取"
        };

    public static string ForJobType(string? jobType)
    {
        if (string.IsNullOrWhiteSpace(jobType))
            return string.Empty;

        var normalized = jobType.Trim();
        if (JobTypeNames.TryGetValue(normalized, out var displayName))
            return displayName;

        return normalized.StartsWith("bidops.", StringComparison.OrdinalIgnoreCase)
            ? "BidOps 后台任务"
            : normalized;
    }
}
