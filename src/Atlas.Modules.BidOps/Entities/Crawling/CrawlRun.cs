using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

/// <summary>
/// 一次采集运行记录。
/// </summary>
public sealed class CrawlRun : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的采集来源主键。
    /// </summary>
    public long SourceId { get; set; }

    /// <summary>
    /// 关联的采集栏目主键。
    /// </summary>
    public long ChannelId { get; set; }

    /// <summary>
    /// 关联的采集断点主键。
    /// </summary>
    public long? CheckpointId { get; set; }

    /// <summary>
    /// 关联的 Atlas 后台任务主键。
    /// </summary>
    public long? BackgroundJobId { get; set; }

    /// <summary>
    /// 运行模式。
    /// </summary>
    public string Mode { get; set; } = BidOpsCrawlModes.Incremental;

    /// <summary>
    /// 记录状态。
    /// </summary>
    public string Status { get; set; } = BidOpsCrawlRunStatuses.Running;

    /// <summary>
    /// 本次运行开始游标。
    /// </summary>
    public string StartCursor { get; set; } = string.Empty;

    /// <summary>
    /// 本次运行结束游标。
    /// </summary>
    public string EndCursor { get; set; } = string.Empty;

    /// <summary>
    /// 本次采集请求页大小。
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 本次采集扫描页数。
    /// </summary>
    public int PageCount { get; set; }

    /// <summary>
    /// 本次采集发现的列表项数量。
    /// </summary>
    public int DiscoveredCount { get; set; }

    /// <summary>
    /// 本次采集新建记录数量。
    /// </summary>
    public int CreatedCount { get; set; }

    /// <summary>
    /// 本次采集识别为内容变化的记录数量。
    /// </summary>
    public int ChangedCount { get; set; }

    /// <summary>
    /// 本次采集识别为重复的记录数量。
    /// </summary>
    public int DuplicateCount { get; set; }

    /// <summary>
    /// 本次采集单项失败数量。
    /// </summary>
    public int FailedItemCount { get; set; }

    /// <summary>
    /// 来源站点返回的总记录数。
    /// </summary>
    public int? TotalRemoteCount { get; set; }

    /// <summary>
    /// 预计剩余待扫描数量。
    /// </summary>
    public int? RemainingEstimate { get; set; }

    /// <summary>
    /// 开始时间。
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// 完成时间。
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 状态消息或日志内容。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
