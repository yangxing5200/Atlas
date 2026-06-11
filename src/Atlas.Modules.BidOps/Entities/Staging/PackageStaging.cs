using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Staging;

public sealed class PackageStaging : BidOpsTenantEntity
{
    public long NoticeStagingId { get; set; }

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

    public decimal AiConfidence { get; set; }

    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;
}
