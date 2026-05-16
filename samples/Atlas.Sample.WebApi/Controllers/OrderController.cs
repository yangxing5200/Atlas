using Atlas.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Sample.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public sealed class OrderController : ControllerBase
{
    private readonly IOrderCommandService _orderCommandService;
    private readonly ILogger<OrderController> _logger;

    public OrderController(
        IOrderCommandService orderCommandService,
        ILogger<OrderController> logger)
    {
        _orderCommandService = orderCommandService ?? throw new ArgumentNullException(nameof(orderCommandService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    [ProducesResponseType(typeof(PlaceOrderResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlaceOrderResult>> Place(
        [FromBody] PlaceOrderRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _orderCommandService.PlaceAsync(request, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing order");
            return StatusCode(500, new { message = "An error occurred while placing the order" });
        }
    }

}
