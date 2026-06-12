using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Suppliers;

public sealed class Supplier : BidOpsTenantEntity
{
    public string SupplierNo { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string UnifiedSocialCreditCode { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string ContactName { get; set; } = string.Empty;

    public string ContactPhone { get; set; } = string.Empty;

    public string ContactEmail { get; set; } = string.Empty;

    public string Status { get; set; } = BidOpsSupplierStatuses.Active;

    public decimal? QualityScore { get; set; }

    public string Remark { get; set; } = string.Empty;
}

public static class BidOpsSupplierStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Blocked = "Blocked";
}
