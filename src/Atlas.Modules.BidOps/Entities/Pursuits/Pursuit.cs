using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Pursuits;

/// <summary>
/// 投标作业舱主记录。
/// </summary>
public sealed class Pursuit : BidOpsTenantEntity
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
    /// 关联的商机主键。
    /// </summary>
    public long? OpportunityId { get; set; }

    /// <summary>
    /// 关联的 Go/No-Go 决策主键。
    /// </summary>
    public long? GoNoGoDecisionId { get; set; }

    /// <summary>
    /// 关联的供应商主键。
    /// </summary>
    public long? SupplierId { get; set; }

    /// <summary>
    /// 匹配时的供应商名称快照。
    /// </summary>
    public string SupplierNameSnapshot { get; set; } = string.Empty;

    /// <summary>
    /// 投标作业编号。
    /// </summary>
    public string PursuitNo { get; set; } = string.Empty;

    /// <summary>
    /// 业务标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 业务推进阶段。
    /// </summary>
    public string Stage { get; set; } = BidOpsPursuitStages.New;

    /// <summary>
    /// 记录状态。
    /// </summary>
    public string Status { get; set; } = BidOpsPursuitStatuses.Active;

    /// <summary>
    /// 唯一活跃标记，用于数据库约束同一对象只能有一个活跃记录。
    /// </summary>
    public string? ActiveMarker { get; set; } = BidOpsPursuitActiveMarkers.Active;

    /// <summary>
    /// 投标作业优先级。
    /// </summary>
    public int Priority { get; set; } = 3;

    /// <summary>
    /// 估算金额，按人民币元存储。
    /// </summary>
    public decimal? EstimatedAmount { get; set; }

    /// <summary>
    /// 投标截止时间（UTC）。
    /// </summary>
    public DateTime? BidDeadlineAtUtc { get; set; }

    /// <summary>
    /// 负责人用户主键。
    /// </summary>
    public long? OwnerUserId { get; set; }

    /// <summary>
    /// 投标作业进度百分比。
    /// </summary>
    public int ProgressPercent { get; set; }

    /// <summary>
    /// 风险等级。
    /// </summary>
    public string RiskLevel { get; set; } = BidOpsPursuitRiskLevels.None;

    /// <summary>
    /// 最近一次阶段变更时间（UTC）。
    /// </summary>
    public DateTime LastStageChangedAtUtc { get; set; }

    /// <summary>
    /// 人工备注。
    /// </summary>
    public string Remark { get; set; } = string.Empty;
}

/// <summary>
/// 投标作业阶段枚举值。
/// </summary>
public static class BidOpsPursuitStages
{
    /// <summary>
    /// 新建阶段。
    /// </summary>
    public const string New = "New";
    /// <summary>
    /// 投标准备阶段。
    /// </summary>
    public const string Preparing = "Preparing";
    /// <summary>
    /// 审核中心功能域。
    /// </summary>
    public const string Review = "Review";
    /// <summary>
    /// 已提交投标文件。
    /// </summary>
    public const string Submitted = "Submitted";
    /// <summary>
    /// 已中标或中选。
    /// </summary>
    public const string Awarded = "Awarded";
    /// <summary>
    /// 已关闭。
    /// </summary>
    public const string Closed = "Closed";
}

/// <summary>
/// 投标作业状态枚举值。
/// </summary>
public static class BidOpsPursuitStatuses
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
/// 投标作业风险等级枚举值。
/// </summary>
public static class BidOpsPursuitRiskLevels
{
    /// <summary>
    /// 无风险。
    /// </summary>
    public const string None = "None";
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
/// 投标作业唯一活跃标记枚举值。
/// </summary>
public static class BidOpsPursuitActiveMarkers
{
    /// <summary>
    /// 启用或活跃状态。
    /// </summary>
    public const string Active = "active";
}
