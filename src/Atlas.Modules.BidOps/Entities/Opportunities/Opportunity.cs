using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Opportunities;

/// <summary>
/// 包件级商机。
/// </summary>
public sealed class Opportunity : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的正式公告主键。
    /// </summary>
    public long NoticeId { get; set; }

    /// <summary>
    /// 关联的包件主键。
    /// </summary>
    public long PackageId { get; set; }

    /// <summary>
    /// 商机编号。
    /// </summary>
    public string OpportunityNo { get; set; } = string.Empty;

    /// <summary>
    /// 业务标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 业务推进阶段。
    /// </summary>
    public string Stage { get; set; } = BidOpsOpportunityStages.New;

    /// <summary>
    /// 记录状态。
    /// </summary>
    public string Status { get; set; } = BidOpsOpportunityStatuses.Active;

    /// <summary>
    /// 唯一活跃标记，用于数据库约束同一对象只能有一个活跃记录。
    /// </summary>
    public string? ActiveMarker { get; set; } = BidOpsOpportunityActiveMarkers.Active;

    /// <summary>
    /// 优先级，数值越小通常越靠前处理。
    /// </summary>
    public int Priority { get; set; } = 3;

    /// <summary>
    /// 估算金额，按人民币元存储。
    /// </summary>
    public decimal? EstimatedAmount { get; set; }

    /// <summary>
    /// 商机价值评分。
    /// </summary>
    public decimal? ValueScore { get; set; }

    /// <summary>
    /// 商机价值等级。
    /// </summary>
    public string ValueLevel { get; set; } = BidOpsOpportunityValueLevels.Unknown;

    /// <summary>
    /// 当前商机的 Go/No-Go 决策。
    /// </summary>
    public string Decision { get; set; } = BidOpsOpportunityDecisions.Undecided;

    /// <summary>
    /// 负责人用户主键。
    /// </summary>
    public long? OwnerUserId { get; set; }

    /// <summary>
    /// 下一步跟进时间（UTC）。
    /// </summary>
    public DateTime? NextActionAtUtc { get; set; }

    /// <summary>
    /// 最近一次阶段变更时间（UTC）。
    /// </summary>
    public DateTime LastStageChangedAtUtc { get; set; }

    /// <summary>
    /// 商机评估摘要。
    /// </summary>
    public string AssessmentSummary { get; set; } = string.Empty;

    /// <summary>
    /// 人工备注。
    /// </summary>
    public string Remark { get; set; } = string.Empty;
}

/// <summary>
/// 商机生命周期状态枚举值。
/// </summary>
public static class BidOpsOpportunityStatuses
{
    /// <summary>
    /// 启用或活跃状态。
    /// </summary>
    public const string Active = "Active";
    /// <summary>
    /// 已关闭。
    /// </summary>
    public const string Closed = "Closed";
    /// <summary>
    /// 已归档。
    /// </summary>
    public const string Archived = "Archived";
}

/// <summary>
/// 商机推进阶段枚举值。
/// </summary>
public static class BidOpsOpportunityStages
{
    /// <summary>
    /// 新建阶段。
    /// </summary>
    public const string New = "New";
    /// <summary>
    /// 关注观察阶段。
    /// </summary>
    public const string Watching = "Watching";
    /// <summary>
    /// 评估阶段。
    /// </summary>
    public const string Assessing = "Assessing";
    /// <summary>
    /// 已完成决策阶段。
    /// </summary>
    public const string Decided = "Decided";
    /// <summary>
    /// 可进入投标作业阶段。
    /// </summary>
    public const string PursuitReady = "PursuitReady";
    /// <summary>
    /// 已关闭。
    /// </summary>
    public const string Closed = "Closed";
}

/// <summary>
/// 商机 Go/No-Go 决策枚举值。
/// </summary>
public static class BidOpsOpportunityDecisions
{
    /// <summary>
    /// 尚未决策。
    /// </summary>
    public const string Undecided = "Undecided";
    /// <summary>
    /// 建议继续投标或推进。
    /// </summary>
    public const string Go = "Go";
    /// <summary>
    /// 建议放弃投标或停止推进。
    /// </summary>
    public const string NoGo = "NoGo";
    /// <summary>
    /// 暂缓决策。
    /// </summary>
    public const string Hold = "Hold";
}

/// <summary>
/// 商机价值等级枚举值。
/// </summary>
public static class BidOpsOpportunityValueLevels
{
    /// <summary>
    /// 未知类型。
    /// </summary>
    public const string Unknown = "Unknown";
    /// <summary>
    /// 低等级。
    /// </summary>
    public const string Low = "Low";
    /// <summary>
    /// 中等级。
    /// </summary>
    public const string Medium = "Medium";
    /// <summary>
    /// 高等级。
    /// </summary>
    public const string High = "High";
}

/// <summary>
/// 商机唯一活跃标记枚举值。
/// </summary>
public static class BidOpsOpportunityActiveMarkers
{
    /// <summary>
    /// 启用或活跃状态。
    /// </summary>
    public const string Active = "active";
}
