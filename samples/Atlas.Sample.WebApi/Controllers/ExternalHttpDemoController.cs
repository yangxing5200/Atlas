using Atlas.Sample.WebApi.Services.ExternalHttpDemo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Sample.WebApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/external-http-demo")]
public sealed class ExternalHttpDemoController : ControllerBase
{
    private readonly IProductSourcingQueryService _sourcingQuery;

    public ExternalHttpDemoController(IProductSourcingQueryService sourcingQuery)
    {
        _sourcingQuery = sourcingQuery;
    }

    [HttpGet("products/{sku}/sourcing-summary")]
    [ProducesResponseType(typeof(ProductSourcingSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProductSourcingErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProductSourcingErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetSourcingSummary(
        string sku,
        [FromQuery] bool unstableSupplier = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _sourcingQuery.GetSourcingSummaryAsync(
            sku,
            unstableSupplier,
            cancellationToken);

        return result.Status switch
        {
            ProductSourcingQueryStatus.Success => Ok(result.Summary),
            ProductSourcingQueryStatus.LocalProductNotFound => NotFound(result.Error),
            ProductSourcingQueryStatus.SupplierProductNotFound => NotFound(result.Error),
            ProductSourcingQueryStatus.SupplierUnavailable => StatusCode(StatusCodes.Status502BadGateway, result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError, result.Error)
        };
    }
}
