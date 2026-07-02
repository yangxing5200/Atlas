using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Suppliers;

/// <summary>
/// 供应商主数据。
/// </summary>
public sealed class Supplier : BidOpsTenantEntity
{
    /// <summary>
    /// 供应商编号。
    /// </summary>
    public string SupplierNo { get; set; } = string.Empty;

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
    /// 地址。
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// 默认联系人姓名。
    /// </summary>
    public string ContactName { get; set; } = string.Empty;

    /// <summary>
    /// 默认联系人电话。
    /// </summary>
    public string ContactPhone { get; set; } = string.Empty;

    /// <summary>
    /// 默认联系人邮箱。
    /// </summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>
    /// 记录状态。
    /// </summary>
    public string Status { get; set; } = BidOpsSupplierStatuses.Active;

    /// <summary>
    /// 质量评分。
    /// </summary>
    public decimal? QualityScore { get; set; }

    /// <summary>
    /// 人工备注。
    /// </summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    /// 创建供应商时引用的原始公告主键。
    /// </summary>
    public long? CreatedFromRawNoticeId { get; set; }

    /// <summary>
    /// 创建供应商时引用的正式公告主键。
    /// </summary>
    public long? CreatedFromNoticeId { get; set; }

    /// <summary>
    /// 创建供应商时引用的公告标题。
    /// </summary>
    public string CreatedFromNoticeTitle { get; set; } = string.Empty;

    /// <summary>
    /// 创建供应商时引用的公告来源地址。
    /// </summary>
    public string CreatedFromSourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// 最近一次结果公告原始记录主键。
    /// </summary>
    public long? LastOutcomeRawNoticeId { get; set; }

    /// <summary>
    /// 最近一次结果公告正式记录主键。
    /// </summary>
    public long? LastOutcomeNoticeId { get; set; }

    /// <summary>
    /// 最近一次结果公告标题。
    /// </summary>
    public string LastOutcomeNoticeTitle { get; set; } = string.Empty;

    /// <summary>
    /// 最近一次结果公告时间（UTC）。
    /// </summary>
    public DateTime? LastOutcomeAtUtc { get; set; }
}

/// <summary>
/// 供应商状态枚举值。
/// </summary>
public static class BidOpsSupplierStatuses
{
    /// <summary>
    /// 启用或活跃状态。
    /// </summary>
    public const string Active = "Active";
    /// <summary>
    /// 停用状态。
    /// </summary>
    public const string Inactive = "Inactive";
    /// <summary>
    /// 禁用或拉黑状态。
    /// </summary>
    public const string Blocked = "Blocked";
}
