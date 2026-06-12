using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Suppliers;

public sealed class SupplierCapability : BidOpsTenantEntity
{
    public long SupplierId { get; set; }

    public string Category { get; set; } = string.Empty;

    public string ProductLine { get; set; } = string.Empty;

    public string CapabilityTags { get; set; } = string.Empty;

    public string RegionScope { get; set; } = string.Empty;

    public string QualificationLevel { get; set; } = string.Empty;

    public string Remark { get; set; } = string.Empty;
}
