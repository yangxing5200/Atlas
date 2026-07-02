using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Suppliers;

/// <summary>
/// 供应商证明材料。
/// </summary>
public sealed class SupplierEvidenceDocument : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的供应商主键。
    /// </summary>
    public long SupplierId { get; set; }

    /// <summary>
    /// 证明材料名称。
    /// </summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// 证明材料类型。
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// 证书或证明材料编号。
    /// </summary>
    public string EvidenceNo { get; set; } = string.Empty;

    /// <summary>
    /// 签发机构。
    /// </summary>
    public string IssuedBy { get; set; } = string.Empty;

    /// <summary>
    /// 有效期开始时间。
    /// </summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>
    /// 有效期结束时间。
    /// </summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>
    /// 附件或文件名。
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 附件原始下载地址。
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// 文件存储提供方标识。
    /// </summary>
    public string StorageProvider { get; set; } = string.Empty;

    /// <summary>
    /// 文件在对象存储或本地文件存储中的键。
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>
    /// 记录状态。
    /// </summary>
    public string Status { get; set; } = BidOpsSupplierEvidenceStatuses.Valid;

    /// <summary>
    /// 人工备注。
    /// </summary>
    public string Remark { get; set; } = string.Empty;
}

/// <summary>
/// 供应商证明材料状态枚举值。
/// </summary>
public static class BidOpsSupplierEvidenceStatuses
{
    /// <summary>
    /// 有效。
    /// </summary>
    public const string Valid = "Valid";
    /// <summary>
    /// 证明材料即将过期。
    /// </summary>
    public const string ExpiringSoon = "ExpiringSoon";
    /// <summary>
    /// 证明材料已过期。
    /// </summary>
    public const string Expired = "Expired";
    /// <summary>
    /// 已归档。
    /// </summary>
    public const string Archived = "Archived";
}
