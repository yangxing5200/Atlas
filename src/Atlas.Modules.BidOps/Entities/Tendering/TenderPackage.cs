using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Tendering;

public sealed class TenderPackage : BidOpsTenantEntity
{
    public long NoticeId { get; set; }

    public long? PackageStagingId { get; set; }

    public string LotNo { get; set; } = string.Empty;

    public string LotName { get; set; } = string.Empty;

    public string PackageNo { get; set; } = string.Empty;

    public string PackageName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal? Quantity { get; set; }

    public string Unit { get; set; } = string.Empty;

    public decimal? BudgetAmount { get; set; }

    public decimal? MaxPrice { get; set; }

    public string DeliveryPlace { get; set; } = string.Empty;

    public string DeliveryPeriod { get; set; } = string.Empty;

    public string Status { get; set; } = "New";
}
