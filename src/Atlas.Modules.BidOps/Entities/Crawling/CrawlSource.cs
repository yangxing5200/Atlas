using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

public sealed class CrawlSource : BidOpsTenantEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string SourceType { get; set; } = "Mock";

    public string BaseUrl { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public int Priority { get; set; }

    public int RateLimitPerMinute { get; set; } = 10;

    public int CrawlIntervalMinutes { get; set; } = 60;

    public int MaxRetryCount { get; set; } = 3;

    public bool NeedJsRender { get; set; }

    public bool NeedLogin { get; set; }

    public bool RespectRobots { get; set; } = true;

    public string UserAgent { get; set; } = "BidOpsCrawler/0.1 (+public tender notice monitor)";

    public string RobotsPolicyNote { get; set; } = string.Empty;

    public string PauseReason { get; set; } = string.Empty;

    public DateTime? PausedAt { get; set; }

    public string Remark { get; set; } = string.Empty;
}
