using Atlas.Infrastructure.Security;
using Atlas.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Extensions.DependencyInjection.Controllers;

[ApiController]
[Route("api/tenant-provisioning")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.RequireTenantProvisioning)]
public sealed class AtlasTenantProvisioningController : ControllerBase
{
    private readonly ITenantProvisioningService _provisioningService;

    public AtlasTenantProvisioningController(ITenantProvisioningService provisioningService)
    {
        _provisioningService = provisioningService ?? throw new ArgumentNullException(nameof(provisioningService));
    }

    [HttpPost]
    [ProducesResponseType(typeof(TenantProvisioningResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TenantProvisioningResult>> ProvisionAsync(
        [FromBody] TenantProvisioningRequest request,
        CancellationToken ct)
    {
        var result = await _provisioningService.ProvisionAsync(request, ct);
        return Created($"/api/tenant-provisioning/{result.TenantId}", result);
    }
}
