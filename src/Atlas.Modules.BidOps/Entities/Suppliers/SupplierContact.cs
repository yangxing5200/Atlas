using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Suppliers;

public sealed class SupplierContact : BidOpsTenantEntity
{
    public long SupplierId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public string Remark { get; set; } = string.Empty;
}
