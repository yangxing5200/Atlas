using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Pursuits;

/// <summary>
/// 投标作业任务。
/// </summary>
public sealed class PursuitTask : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的投标作业主键。
    /// </summary>
    public long PursuitId { get; set; }

    /// <summary>
    /// 业务标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 任务类型。
    /// </summary>
    public string TaskType { get; set; } = BidOpsPursuitTaskTypes.Other;

    /// <summary>
    /// 记录状态。
    /// </summary>
    public string Status { get; set; } = BidOpsPursuitTaskStatuses.Todo;

    /// <summary>
    /// 任务优先级。
    /// </summary>
    public int Priority { get; set; } = 3;

    /// <summary>
    /// 负责人用户主键。
    /// </summary>
    public long? OwnerUserId { get; set; }

    /// <summary>
    /// 任务截止时间（UTC）。
    /// </summary>
    public DateTime? DueAtUtc { get; set; }

    /// <summary>
    /// 完成时间（UTC）。
    /// </summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// 任务描述。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 任务处理结果说明。
    /// </summary>
    public string ResultNote { get; set; } = string.Empty;
}

/// <summary>
/// 投标作业任务类型枚举值。
/// </summary>
public static class BidOpsPursuitTaskTypes
{
    /// <summary>
    /// 资格文件任务。
    /// </summary>
    public const string Qualification = "Qualification";
    /// <summary>
    /// 技术文件任务。
    /// </summary>
    public const string Technical = "Technical";
    /// <summary>
    /// 商务文件任务。
    /// </summary>
    public const string Commercial = "Commercial";
    /// <summary>
    /// 报价任务。
    /// </summary>
    public const string Pricing = "Pricing";
    /// <summary>
    /// 审核中心功能域。
    /// </summary>
    public const string Review = "Review";
    /// <summary>
    /// 投标文件提交任务。
    /// </summary>
    public const string Submission = "Submission";
    /// <summary>
    /// 其他类型。
    /// </summary>
    public const string Other = "Other";
}

/// <summary>
/// 投标作业任务状态枚举值。
/// </summary>
public static class BidOpsPursuitTaskStatuses
{
    /// <summary>
    /// 待处理。
    /// </summary>
    public const string Todo = "Todo";
    /// <summary>
    /// 处理中。
    /// </summary>
    public const string InProgress = "InProgress";
    /// <summary>
    /// 已完成。
    /// </summary>
    public const string Done = "Done";
    /// <summary>
    /// 禁用或拉黑状态。
    /// </summary>
    public const string Blocked = "Blocked";
    /// <summary>
    /// 已取消。
    /// </summary>
    public const string Canceled = "Canceled";
    /// <summary>
    /// 已逾期。
    /// </summary>
    public const string Overdue = "Overdue";
}
