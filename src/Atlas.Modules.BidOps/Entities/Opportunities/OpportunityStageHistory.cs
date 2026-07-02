using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Opportunities;

/// <summary>
/// 商机阶段变更历史。
/// </summary>
public sealed class OpportunityStageHistory : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的商机主键。
    /// </summary>
    public long OpportunityId { get; set; }

    /// <summary>
    /// 变更前商机阶段。
    /// </summary>
    public string FromStage { get; set; } = string.Empty;

    /// <summary>
    /// 变更后商机阶段。
    /// </summary>
    public string ToStage { get; set; } = string.Empty;

    /// <summary>
    /// 操作或决策原因。
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 执行阶段变更的用户主键。
    /// </summary>
    public long? OperatorUserId { get; set; }

    /// <summary>
    /// 事件发生时间（UTC）。
    /// </summary>
    public DateTime OccurredAtUtc { get; set; }
}
