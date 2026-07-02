using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Buyers;

/// <summary>
/// 采购人/招标人主数据。
/// </summary>
public sealed class Buyer : BidOpsTenantEntity
{
    /// <summary>
    /// 采购人编号。
    /// </summary>
    public string BuyerNo { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 用于去重和匹配的归一化名称。
    /// </summary>
    public string NameNormalized { get; set; } = string.Empty;

    /// <summary>
    /// 统一社会信用代码。
    /// </summary>
    public string UnifiedSocialCreditCode { get; set; } = string.Empty;

    /// <summary>
    /// 地区或属地信息。
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// 来源公告或来源页面地址。
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// 最近一次关联公告的项目/采购/招标编号。
    /// </summary>
    public string LastProjectCode { get; set; } = string.Empty;

    /// <summary>
    /// 最近一次关联公告的项目名称。
    /// </summary>
    public string LastProjectName { get; set; } = string.Empty;

    /// <summary>
    /// 最近一次关联公告的标题。
    /// </summary>
    public string LastNoticeTitle { get; set; } = string.Empty;

    /// <summary>
    /// 最近一次在来源公告中出现的时间（UTC）。
    /// </summary>
    public DateTime? LastSeenAtUtc { get; set; }

    /// <summary>
    /// 记录状态。
    /// </summary>
    public string Status { get; set; } = BidOpsBuyerStatuses.Active;

    /// <summary>
    /// 人工备注。
    /// </summary>
    public string Remark { get; set; } = string.Empty;
}

/// <summary>
/// 采购人状态枚举值。
/// </summary>
public static class BidOpsBuyerStatuses
{
    /// <summary>
    /// 启用或活跃状态。
    /// </summary>
    public const string Active = "Active";
    /// <summary>
    /// 停用状态。
    /// </summary>
    public const string Inactive = "Inactive";
}
