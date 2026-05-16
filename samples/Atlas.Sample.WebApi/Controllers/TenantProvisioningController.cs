using Atlas.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Sample.WebApi.Controllers;

[ApiController]
[Route("api/tenant-provisioning")]
[Produces("application/json")]
[Authorize]
public sealed class TenantProvisioningController : ControllerBase
{
    private readonly ITenantProvisioningService _provisioningService;
    private readonly ILogger<TenantProvisioningController> _logger;

    public TenantProvisioningController(
        ITenantProvisioningService provisioningService,
        ILogger<TenantProvisioningController> logger)
    {
        _provisioningService = provisioningService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(TenantProvisioningResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TenantProvisioningResult>> Provision(
        [FromBody] TenantProvisioningRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _provisioningService.ProvisionAsync(request, ct);
            return Created($"/api/tenant-provisioning/{result.TenantId}", result);
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
            _logger.LogError(ex, "Tenant provisioning failed for domain {Domain}", request.Domain);
            return StatusCode(500, new { message = "Tenant provisioning failed" });
        }
    }

}
