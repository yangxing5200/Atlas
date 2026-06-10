using Atlas.Sample.WebApi.Services.PaymentDemo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Sample.WebApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payment-demo")]
public sealed class OrderPaymentDemoController : ControllerBase
{
    private readonly IOrderPaymentDemoService _paymentService;

    public OrderPaymentDemoController(IOrderPaymentDemoService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("orders/{orderId:long}/pay")]
    [ProducesResponseType(typeof(OrderPaymentDemoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OrderPaymentDemoErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(OrderPaymentDemoErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Pay(
        long orderId,
        [FromBody] PayOrderDemoRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _paymentService.PayAsync(orderId, request, cancellationToken);

        return result.Status switch
        {
            OrderPaymentDemoStatus.Success => Ok(result.Payment),
            OrderPaymentDemoStatus.LocalOrderNotFound => NotFound(result.Error),
            OrderPaymentDemoStatus.PaymentProviderUnavailable => StatusCode(StatusCodes.Status502BadGateway, result.Error),
            _ => StatusCode(StatusCodes.Status500InternalServerError, result.Error)
        };
    }
}
