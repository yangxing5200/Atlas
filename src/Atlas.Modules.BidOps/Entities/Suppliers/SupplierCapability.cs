using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Suppliers;

/// <summary>
/// 供应商能力范围。
/// </summary>
public sealed class SupplierCapability : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的供应商主键。
    /// </summary>
    public long SupplierId { get; set; }

    /// <summary>
    /// 品类或业务类别。
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 产品线。
    /// </summary>
    public string ProductLine { get; set; } = string.Empty;

    /// <summary>
    /// 能力标签集合。
    /// </summary>
    public string CapabilityTags { get; set; } = string.Empty;

    /// <summary>
    /// 可服务区域范围。
    /// </summary>
    public string RegionScope { get; set; } = string.Empty;

    /// <summary>
    /// 资质等级。
    /// </summary>
    public string QualificationLevel { get; set; } = string.Empty;

    /// <summary>
    /// 人工备注。
    /// </summary>
    public string Remark { get; set; } = string.Empty;
}
