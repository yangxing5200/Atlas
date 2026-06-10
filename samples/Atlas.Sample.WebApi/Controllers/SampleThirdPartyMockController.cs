using Atlas.Sample.WebApi.Integrations.SampleThirdParty;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Sample.WebApi.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/mock-third-party/supplier-catalog")]
public sealed class SampleThirdPartyMockController : ControllerBase
{
    private static int _unstableCounter;

    [HttpGet("products/{sku}")]
    public IActionResult GetSupplierProduct(
        string sku,
        [FromQuery] bool unstable = false)
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKey) ||
            apiKey != "sample-demo-key")
        {
            return Unauthorized(new
            {
                code = "invalid_api_key",
                message = "The supplier API key is missing or invalid."
            });
        }

        if (unstable && Interlocked.Increment(ref _unstableCounter) % 2 == 1)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "temporary_unavailable",
                message = "Supplier catalog is temporarily unavailable."
            });
        }

        if (sku.Equals("SKU-MISSING-SUPPLIER", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new
            {
                code = "supplier_product_not_found",
                message = $"Supplier catalog cannot find product '{sku}'."
            });
        }

        return Ok(BuildSupplierProduct(sku));
    }

    private static SampleThirdPartyProductDto BuildSupplierProduct(string sku)
    {
        var lowStock = sku.Equals("SKU-LOWSTOCK", StringComparison.OrdinalIgnoreCase);
        var delayed = sku.Equals("SKU-RETRY", StringComparison.OrdinalIgnoreCase);

        return new SampleThirdPartyProductDto
        {
            Sku = sku,
            SupplierSku = $"SUP-{sku}",
            ProductName = $"Supplier catalog item {sku}",
            UnitPrice = lowStock ? 89.50m : 142.30m,
            Currency = "CNY",
            AvailableQuantity = lowStock ? 3 : 250,
            LeadTimeDays = delayed ? 3 : 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
