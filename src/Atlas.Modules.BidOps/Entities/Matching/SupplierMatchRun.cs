using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Matching;

/// <summary>
/// 供应商匹配运行记录。
/// </summary>
public sealed class SupplierMatchRun : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的包件主键。
    /// </summary>
    public long PackageId { get; set; }

    /// <summary>
    /// 关联的 Atlas 后台任务主键。
    /// </summary>
    public long? BackgroundJobId { get; set; }

    /// <summary>
    /// 供应商匹配运行编号。
    /// </summary>
    public string RunNo { get; set; } = string.Empty;

    /// <summary>
    /// 记录状态。
    /// </summary>
    public string Status { get; set; } = BidOpsSupplierMatchRunStatuses.Queued;

    /// <summary>
    /// 发起匹配的用户主键。
    /// </summary>
    public long RequestedByUserId { get; set; }

    /// <summary>
    /// 发起匹配的用户名。
    /// </summary>
    public string RequestedByUserName { get; set; } = string.Empty;

    /// <summary>
    /// 匹配条件摘要。
    /// </summary>
    public string CriteriaSummary { get; set; } = string.Empty;

    /// <summary>
    /// 本次匹配最多纳入的供应商数量。
    /// </summary>
    public int MaxSuppliers { get; set; } = 100;

    /// <summary>
    /// 参与匹配的供应商数量。
    /// </summary>
    public int SupplierCount { get; set; }

    /// <summary>
    /// 匹配成功的供应商数量。
    /// </summary>
    public int MatchedCount { get; set; }

    /// <summary>
    /// 缺失证明材料数量。
    /// </summary>
    public int MissingEvidenceCount { get; set; }

    /// <summary>
    /// 开始时间（UTC）。
    /// </summary>
    public DateTime? StartedAtUtc { get; set; }

    /// <summary>
    /// 完成时间（UTC）。
    /// </summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// 错误信息。
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// 供应商匹配运行状态枚举值。
/// </summary>
public static class BidOpsSupplierMatchRunStatuses
{
    /// <summary>
    /// 已排队。
    /// </summary>
    public const string Queued = "Queued";
    /// <summary>
    /// 运行中。
    /// </summary>
    public const string Running = "Running";
    /// <summary>
    /// 成功。
    /// </summary>
    public const string Succeeded = "Succeeded";
    /// <summary>
    /// 失败。
    /// </summary>
    public const string Failed = "Failed";
}
