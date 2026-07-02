using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Suppliers;

/// <summary>
/// 供应商联系人。
/// </summary>
public sealed class SupplierContact : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的供应商主键。
    /// </summary>
    public long SupplierId { get; set; }

    /// <summary>
    /// 显示名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 联系人角色。
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 联系电话。
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// 联系邮箱。
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 是否主联系人。
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// 人工备注。
    /// </summary>
    public string Remark { get; set; } = string.Empty;
}
