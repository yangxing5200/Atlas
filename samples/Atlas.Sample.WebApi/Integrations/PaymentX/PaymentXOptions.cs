namespace Atlas.Sample.WebApi.Integrations.PaymentX;

public sealed class PaymentXOptions
{
    public const string SectionName = "Integrations:PaymentX";

    public string ApiPrefix { get; set; } = "/api/v1";

    public string MerchantId { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;
}
