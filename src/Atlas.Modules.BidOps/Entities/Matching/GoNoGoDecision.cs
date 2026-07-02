using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Matching;

/// <summary>
/// 包件 Go/No-Go 决策记录。
/// </summary>
public sealed class GoNoGoDecision : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的包件主键。
    /// </summary>
    public long PackageId { get; set; }

    /// <summary>
    /// 关联的商机主键。
    /// </summary>
    public long? OpportunityId { get; set; }

    /// <summary>
    /// 关联的供应商匹配运行主键。
    /// </summary>
    public long? MatchRunId { get; set; }

    /// <summary>
    /// 关联的供应商匹配结果主键。
    /// </summary>
    public long? SupplierMatchResultId { get; set; }

    /// <summary>
    /// 关联的供应商主键。
    /// </summary>
    public long? SupplierId { get; set; }

    /// <summary>
    /// 最终 Go/No-Go 决策。
    /// </summary>
    public string Decision { get; set; } = BidOpsGoNoGoDecisions.Hold;

    /// <summary>
    /// 操作或决策原因。
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 风险摘要。
    /// </summary>
    public string RiskSummary { get; set; } = string.Empty;

    /// <summary>
    /// 做出决策的用户主键。
    /// </summary>
    public long DecidedByUserId { get; set; }

    /// <summary>
    /// 做出决策的用户名。
    /// </summary>
    public string DecidedByUserName { get; set; } = string.Empty;

    /// <summary>
    /// 决策时间（UTC）。
    /// </summary>
    public DateTime DecidedAtUtc { get; set; }
}

/// <summary>
/// Go/No-Go 决策枚举值。
/// </summary>
public static class BidOpsGoNoGoDecisions
{
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
