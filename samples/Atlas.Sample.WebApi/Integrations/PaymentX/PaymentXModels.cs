namespace Atlas.Sample.WebApi.Integrations.PaymentX;

public sealed class PaymentXCreatePaymentRequest
{
    public string MerchantOrderNo { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "CNY";

    public string Subject { get; set; } = string.Empty;

    public string NotifyUrl { get; set; } = string.Empty;
}

public sealed class PaymentXCreatePaymentResponse
{
    public string PaymentId { get; set; } = string.Empty;

    public string MerchantOrderNo { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "CNY";

    public DateTimeOffset CreatedAt { get; set; }
}
