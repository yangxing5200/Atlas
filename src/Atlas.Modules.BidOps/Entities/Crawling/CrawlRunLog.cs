using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

/// <summary>
/// 采集运行过程日志。
/// </summary>
public sealed class CrawlRunLog : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的采集来源主键。
    /// </summary>
    public long? SourceId { get; set; }

    /// <summary>
    /// 关联的采集栏目主键。
    /// </summary>
    public long? ChannelId { get; set; }

    /// <summary>
    /// 关联的 Atlas 后台任务主键。
    /// </summary>
    public long? BackgroundJobId { get; set; }

    /// <summary>
    /// 本条日志对应的采集操作。
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// 本条运行日志的状态。
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 状态消息或日志内容。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 本次操作耗时，单位毫秒。
    /// </summary>
    public int? DurationMs { get; set; }
}
