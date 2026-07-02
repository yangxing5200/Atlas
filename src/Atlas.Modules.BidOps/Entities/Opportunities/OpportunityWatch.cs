using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Opportunities;

/// <summary>
/// 商机关注关系。
/// </summary>
public sealed class OpportunityWatch : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的商机主键。
    /// </summary>
    public long OpportunityId { get; set; }

    /// <summary>
    /// 关注该商机的用户主键。
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 人工备注。
    /// </summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; } = true;
}
