namespace Atlas.Sample.WebApi.Integrations.SampleThirdParty;

public sealed class SampleThirdPartyProductDto
{
    public string Sku { get; set; } = string.Empty;

    public string SupplierSku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public string Currency { get; set; } = "CNY";

    public int AvailableQuantity { get; set; }

    public int LeadTimeDays { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
