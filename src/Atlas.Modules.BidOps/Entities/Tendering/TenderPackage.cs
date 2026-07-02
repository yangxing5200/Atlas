using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Tendering;

/// <summary>
/// 人工审核后的正式包件。
/// </summary>
public sealed class TenderPackage : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的正式公告主键。
    /// </summary>
    public long NoticeId { get; set; }

    /// <summary>
    /// 关联的包件暂存记录主键。
    /// </summary>
    public long? PackageStagingId { get; set; }

    /// <summary>
    /// 分标、标段或分包编号。
    /// </summary>
    public string LotNo { get; set; } = string.Empty;

    /// <summary>
    /// 分标、标段或分包名称。
    /// </summary>
    public string LotName { get; set; } = string.Empty;

    /// <summary>
    /// 包号。
    /// </summary>
    public string PackageNo { get; set; } = string.Empty;

    /// <summary>
    /// 包件名称。
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// 品类或业务类别。
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 数量。
    /// </summary>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// 计量单位。
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// 预算金额，按人民币元存储。
    /// </summary>
    public decimal? BudgetAmount { get; set; }

    /// <summary>
    /// 最高限价，按人民币元存储。
    /// </summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>
    /// 交付地点。
    /// </summary>
    public string DeliveryPlace { get; set; } = string.Empty;

    /// <summary>
    /// 交付周期或工期文本。
    /// </summary>
    public string DeliveryPeriod { get; set; } = string.Empty;

    /// <summary>
    /// 记录状态。
    /// </summary>
    public string Status { get; set; } = "New";
}
