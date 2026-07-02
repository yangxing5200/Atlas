using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

/// <summary>
/// 采集断点和进度状态。
/// </summary>
public sealed class CrawlCheckpoint : BidOpsTenantEntity
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
    /// 运行模式。
    /// </summary>
    public string Mode { get; set; } = BidOpsCrawlModes.Incremental;

    /// <summary>
    /// 记录状态。
    /// </summary>
    public string Status { get; set; } = BidOpsCrawlCheckpointStatuses.Idle;

    /// <summary>
    /// 游标类型。
    /// </summary>
    public string CursorKind { get; set; } = BidOpsCrawlCursorKinds.PageIndex;

    /// <summary>
    /// 下一次采集起始游标。
    /// </summary>
    public string NextCursor { get; set; } = "1";

    /// <summary>
    /// 最近一次成功处理的游标。
    /// </summary>
    public string LastSuccessfulCursor { get; set; } = string.Empty;

    /// <summary>
    /// 回填范围开始发布时间。
    /// </summary>
    public DateTime? RangeStartPublishTime { get; set; }

    /// <summary>
    /// 回填范围结束发布时间。
    /// </summary>
    public DateTime? RangeEndPublishTime { get; set; }

    /// <summary>
    /// 增量采集高水位发布时间。
    /// </summary>
    public DateTime? HighWatermarkPublishTime { get; set; }

    /// <summary>
    /// 回填采集低水位发布时间。
    /// </summary>
    public DateTime? LowWatermarkPublishTime { get; set; }

    /// <summary>
    /// 来源站点返回的总记录数。
    /// </summary>
    public int? TotalRemoteCount { get; set; }

    /// <summary>
    /// 累计扫描条目数量。
    /// </summary>
    public int ScannedItemCount { get; set; }

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
    /// 预计剩余待扫描数量。
    /// </summary>
    public int? RemainingEstimate { get; set; }

    /// <summary>
    /// 开始时间。
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 最近一次运行时间。
    /// </summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// 完成时间。
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 暂停时间。
    /// </summary>
    public DateTime? PausedAt { get; set; }

    /// <summary>
    /// 暂停原因。
    /// </summary>
    public string PauseReason { get; set; } = string.Empty;

    /// <summary>
    /// 最近一次错误信息。
    /// </summary>
    public string LastError { get; set; } = string.Empty;
}
