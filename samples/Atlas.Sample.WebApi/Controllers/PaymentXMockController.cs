using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Sample.WebApi.Integrations.PaymentX;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Atlas.Sample.WebApi.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/mock-paymentx/v1")]
public sealed class PaymentXMockController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static int _unstableCounter;

    private readonly IOptionsMonitor<PaymentXOptions> _options;

    public PaymentXMockController(IOptionsMonitor<PaymentXOptions> options)
    {
        _options = options;
    }

    [HttpPost("payments")]
    public async Task<IActionResult> CreatePayment(
        [FromQuery] bool unstable = false,
        CancellationToken cancellationToken = default)
    {
        var body = await ReadRawBodyAsync(cancellationToken);
        if (!IsSignatureValid(body))
        {
            return Unauthorized(new
            {
                code = "invalid_signature",
                message = "PaymentX signature headers are missing or invalid."
            });
        }

        var request = JsonSerializer.Deserialize<PaymentXCreatePaymentRequest>(body, JsonOptions);
        if (request is null)
        {
            return BadRequest(new
            {
                code = "invalid_request_body",
                message = "PaymentX request body is required."
            });
        }

        if (!Request.Headers.ContainsKey("Idempotency-Key"))
        {
            return BadRequest(new
            {
                code = "missing_idempotency_key",
                message = "Idempotency-Key is required for payment creation."
            });
        }

        if (unstable && Interlocked.Increment(ref _unstableCounter) % 2 == 1)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "temporary_unavailable",
                message = "PaymentX sandbox is temporarily unavailable."
            });
        }

        return Ok(new PaymentXCreatePaymentResponse
        {
            PaymentId = $"payx_{Guid.NewGuid():N}",
            MerchantOrderNo = request.MerchantOrderNo,
            Status = "Pending",
            Amount = request.Amount,
            Currency = request.Currency,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task<string> ReadRawBodyAsync(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        Request.Body.Position = 0;

        using var reader = new StreamReader(
            Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);

        Request.Body.Position = 0;
        return body;
    }

    private bool IsSignatureValid(string body)
    {
        var options = _options.CurrentValue;

        if (!HeaderEquals(PaymentXSignatureHandler.MerchantIdHeader, options.MerchantId) ||
            !HeaderEquals(PaymentXSignatureHandler.AccessKeyHeader, options.AccessKey))
        {
            return false;
        }

        var timestamp = Request.Headers[PaymentXSignatureHandler.TimestampHeader].ToString();
        var nonce = Request.Headers[PaymentXSignatureHandler.NonceHeader].ToString();
        var signature = Request.Headers[PaymentXSignatureHandler.SignatureHeader].ToString();

        if (string.IsNullOrWhiteSpace(timestamp) ||
            string.IsNullOrWhiteSpace(nonce) ||
            string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var payload = PaymentXSignatureHandler.BuildPayload(
            Request.Method,
            Request.Path + Request.QueryString,
            timestamp,
            nonce,
            body);
        var expected = PaymentXSignatureHandler.Sign(payload, options.SecretKey);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature),
            Encoding.UTF8.GetBytes(expected));
    }

    private bool HeaderEquals(string headerName, string expected)
    {
        return Request.Headers.TryGetValue(headerName, out var actual) &&
            string.Equals(actual.ToString(), expected, StringComparison.Ordinal);
    }
}
