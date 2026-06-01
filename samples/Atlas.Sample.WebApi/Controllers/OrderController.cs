using Atlas.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Infrastructure.Security;
using Atlas.Sample.ECommerce;

namespace Atlas.Sample.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public sealed class OrderController : ControllerBase
{
    private readonly IOrderCommandService _orderCommandService;

    public OrderController(IOrderCommandService orderCommandService)
    {
        _orderCommandService = orderCommandService ?? throw new ArgumentNullException(nameof(orderCommandService));
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.OrdersPlace)]
    [ProducesResponseType(typeof(PlaceOrderResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlaceOrderResult>> Place(
        [FromBody] PlaceOrderRequest request,
        CancellationToken ct = default)
    {
        var result = await _orderCommandService.PlaceAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

}
