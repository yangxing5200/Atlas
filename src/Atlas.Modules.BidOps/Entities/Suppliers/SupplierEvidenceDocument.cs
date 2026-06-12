using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Suppliers;

public sealed class SupplierEvidenceDocument : BidOpsTenantEntity
{
    public long SupplierId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string EvidenceNo { get; set; } = string.Empty;

    public string IssuedBy { get; set; } = string.Empty;

    public DateTime? ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileUrl { get; set; } = string.Empty;

    public string StorageProvider { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public string Status { get; set; } = BidOpsSupplierEvidenceStatuses.Valid;

    public string Remark { get; set; } = string.Empty;
}

public static class BidOpsSupplierEvidenceStatuses
{
    public const string Valid = "Valid";
    public const string ExpiringSoon = "ExpiringSoon";
    public const string Expired = "Expired";
    public const string Archived = "Archived";
}
