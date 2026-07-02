using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

/// <summary>
/// 公开标讯采集来源配置。
/// </summary>
public sealed class CrawlSource : BidOpsTenantEntity
{
    /// <summary>
    /// 业务编码，通常用于租户内唯一识别。
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 采集来源类型。
    /// </summary>
    public string SourceType { get; set; } = "Mock";

    /// <summary>
    /// 采集来源基础地址。
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 优先级，数值越小通常越靠前处理。
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 每分钟最大请求数。
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 10;

    /// <summary>
    /// 采集间隔，单位分钟。
    /// </summary>
    public int CrawlIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 最大重试次数。
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// 是否需要浏览器渲染页面。
    /// </summary>
    public bool NeedJsRender { get; set; }

    /// <summary>
    /// 来源是否需要登录；MVP 不采集需要登录的来源。
    /// </summary>
    public bool NeedLogin { get; set; }

    /// <summary>
    /// 是否遵守来源站点 robots 策略。
    /// </summary>
    public bool RespectRobots { get; set; } = true;

    /// <summary>
    /// 采集请求使用的 User-Agent。
    /// </summary>
    public string UserAgent { get; set; } = "BidOpsCrawler/0.1 (+public tender notice monitor)";

    /// <summary>
    /// Robots 或来源访问策略说明。
    /// </summary>
    public string RobotsPolicyNote { get; set; } = string.Empty;

    /// <summary>
    /// 暂停原因。
    /// </summary>
    public string PauseReason { get; set; } = string.Empty;

    /// <summary>
    /// 暂停时间。
    /// </summary>
    public DateTime? PausedAt { get; set; }

    /// <summary>
    /// 人工备注。
    /// </summary>
    public string Remark { get; set; } = string.Empty;
}
