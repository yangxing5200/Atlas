using Atlas.Core.Authorization;
using Atlas.Core.Services;
using Atlas.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Extensions.DependencyInjection.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[Authorize]
public sealed class AtlasAuthController : ControllerBase
{
    private readonly ICurrentIdentity _currentIdentity;
    private readonly IAtlasAuthorizationContextService _authorizationContext;

    public AtlasAuthController(
        ICurrentIdentity currentIdentity,
        IAtlasAuthorizationContextService authorizationContext)
    {
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
        _authorizationContext = authorizationContext ?? throw new ArgumentNullException(nameof(authorizationContext));
    }

    [HttpGet("context")]
    [ProducesResponseType(typeof(AtlasAuthorizationContextSnapshot), StatusCodes.Status200OK)]
    public async Task<ActionResult<AtlasAuthorizationContextSnapshot>> GetContext(CancellationToken ct)
    {
        var context = GetRuntimeContext();
        return context == null
            ? Unauthorized(new { message = "User, tenant, or authentication context is missing." })
            : Ok(await _authorizationContext.GetContextAsync(context, ct));
    }

    private AtlasAuthorizationRuntimeContext? GetRuntimeContext()
    {
        return !_currentIdentity.IsAuthenticated ||
               !_currentIdentity.UserId.HasValue ||
               !_currentIdentity.TenantId.HasValue
            ? null
            : new AtlasAuthorizationRuntimeContext(
                _currentIdentity.TenantId.Value,
                _currentIdentity.UserId.Value,
                _currentIdentity.StoreId);
    }
}

[ApiController]
[Route("api/menus")]
[Produces("application/json")]
[Authorize]
public sealed class AtlasMenusController : ControllerBase
{
    private readonly ICurrentIdentity _currentIdentity;
    private readonly IAtlasAuthorizationContextService _authorizationContext;

    public AtlasMenusController(
        ICurrentIdentity currentIdentity,
        IAtlasAuthorizationContextService authorizationContext)
    {
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
        _authorizationContext = authorizationContext ?? throw new ArgumentNullException(nameof(authorizationContext));
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(IReadOnlyCollection<AtlasMenuItemNode>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AtlasMenuItemNode>>> GetMyMenus(CancellationToken ct)
    {
        var context = GetRuntimeContext();
        return context == null
            ? Unauthorized(new { message = "User, tenant, or authentication context is missing." })
            : Ok(await _authorizationContext.GetMenusAsync(context, ct));
    }

    private AtlasAuthorizationRuntimeContext? GetRuntimeContext()
    {
        return !_currentIdentity.IsAuthenticated ||
               !_currentIdentity.UserId.HasValue ||
               !_currentIdentity.TenantId.HasValue
            ? null
            : new AtlasAuthorizationRuntimeContext(
                _currentIdentity.TenantId.Value,
                _currentIdentity.UserId.Value,
                _currentIdentity.StoreId);
    }
}

[ApiController]
[Route("api/admin/authorization")]
[Produces("application/json")]
[Authorize]
public sealed class AtlasAuthorizationAdminController : ControllerBase
{
    private readonly ICurrentIdentity _currentIdentity;
    private readonly IAtlasAuthorizationContextService _authorizationContext;
    private readonly IAtlasAuthorizationManagementService _management;

    public AtlasAuthorizationAdminController(
        ICurrentIdentity currentIdentity,
        IAtlasAuthorizationContextService authorizationContext,
        IAtlasAuthorizationManagementService management)
    {
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
        _authorizationContext = authorizationContext ?? throw new ArgumentNullException(nameof(authorizationContext));
        _management = management ?? throw new ArgumentNullException(nameof(management));
    }

    [HttpGet("catalog")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthorizationRead)]
    [ProducesResponseType(typeof(AtlasAuthorizationCatalogSnapshot), StatusCodes.Status200OK)]
    public Task<AtlasAuthorizationCatalogSnapshot> GetCatalog(CancellationToken ct)
    {
        return _management.GetCatalogAsync(ct);
    }

    [HttpGet("tenants/{tenantId:long}/entitlements")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthorizationRead)]
    [ProducesResponseType(typeof(IReadOnlyCollection<AtlasTenantEntitlementInfo>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AtlasTenantEntitlementInfo>>> GetTenantEntitlements(
        [FromRoute] long tenantId,
        CancellationToken ct)
    {
        if (!CanAccessTenant(tenantId))
            return Forbid();

        return Ok(await _management.GetTenantEntitlementsAsync(tenantId, ct));
    }

    [HttpPost("tenants/{tenantId:long}/entitlements")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthorizationManage)]
    [ProducesResponseType(typeof(AtlasTenantEntitlementInfo), StatusCodes.Status201Created)]
    public async Task<ActionResult<AtlasTenantEntitlementInfo>> GrantEntitlement(
        [FromRoute] long tenantId,
        [FromBody] GrantEntitlementRequest request,
        CancellationToken ct)
    {
        if (!CanAccessTenant(tenantId))
            return Forbid();

        var result = await _management.GrantEntitlementAsync(
            new AtlasGrantEntitlementCommand(
                tenantId,
                request.SubjectType,
                request.SubjectId,
                request.PackageCode,
                request.CapabilityCode,
                request.Source,
                request.StartAtUtc,
                request.EndAtUtc,
                request.OptionOverrideJson),
            ct);

        return CreatedAtAction(
            nameof(GetTenantEntitlements),
            new { tenantId },
            result);
    }

    [HttpPut("tenants/{tenantId:long}/entitlements/{entitlementId:long}/status")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthorizationManage)]
    [ProducesResponseType(typeof(AtlasTenantEntitlementInfo), StatusCodes.Status200OK)]
    public async Task<ActionResult<AtlasTenantEntitlementInfo>> SetEntitlementStatus(
        [FromRoute] long tenantId,
        [FromRoute] long entitlementId,
        [FromBody] SetEntitlementStatusRequest request,
        CancellationToken ct)
    {
        if (!CanAccessTenant(tenantId))
            return Forbid();

        var existing = await _management.GetTenantEntitlementsAsync(tenantId, ct);
        if (existing.All(x => x.Id != entitlementId))
            return NotFound(new { message = $"Entitlement '{entitlementId}' not found." });

        var result = await _management.SetEntitlementStatusAsync(
            new AtlasSetEntitlementStatusCommand(entitlementId, request.Status),
            ct);

        return result == null
            ? NotFound(new { message = $"Entitlement '{entitlementId}' not found." })
            : Ok(result);
    }

    [HttpGet("tenants/{tenantId:long}/roles/{roleId:long}/permissions")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthorizationRead)]
    [ProducesResponseType(typeof(IReadOnlyCollection<AtlasRolePermissionInfo>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AtlasRolePermissionInfo>>> GetRolePermissions(
        [FromRoute] long tenantId,
        [FromRoute] long roleId,
        CancellationToken ct)
    {
        if (!CanAccessTenant(tenantId))
            return Forbid();

        return Ok(await _management.GetRolePermissionsAsync(tenantId, roleId, ct));
    }

    [HttpPut("tenants/{tenantId:long}/roles/{roleId:long}/permissions")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthorizationManage)]
    [ProducesResponseType(typeof(IReadOnlyCollection<AtlasRolePermissionInfo>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AtlasRolePermissionInfo>>> SetRolePermissions(
        [FromRoute] long tenantId,
        [FromRoute] long roleId,
        [FromBody] IReadOnlyCollection<AtlasSetRolePermissionCommand> permissions,
        CancellationToken ct)
    {
        if (!CanAccessTenant(tenantId))
            return Forbid();

        return Ok(await _management.SetRolePermissionsAsync(tenantId, roleId, permissions, ct));
    }

    [HttpGet("diagnostics/users/{userId:long}/permissions/{permissionCode}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthorizationRead)]
    [ProducesResponseType(typeof(AtlasPermissionExplanation), StatusCodes.Status200OK)]
    public async Task<ActionResult<AtlasPermissionExplanation>> ExplainPermission(
        [FromRoute] long userId,
        [FromRoute] string permissionCode,
        [FromQuery] long? storeId,
        CancellationToken ct)
    {
        if (!_currentIdentity.TenantId.HasValue)
            return Unauthorized(new { message = "Tenant context is missing." });

        var context = new AtlasAuthorizationRuntimeContext(
            _currentIdentity.TenantId.Value,
            userId,
            storeId ?? _currentIdentity.StoreId);

        return Ok(await _authorizationContext.ExplainPermissionAsync(context, permissionCode, ct));
    }

    private bool CanAccessTenant(long tenantId)
    {
        return _currentIdentity.TenantId.HasValue &&
               _currentIdentity.TenantId.Value == tenantId;
    }
}

public sealed record GrantEntitlementRequest(
    AtlasEntitlementSubjectType SubjectType,
    long SubjectId,
    string? PackageCode,
    string? CapabilityCode,
    AtlasEntitlementSource Source = AtlasEntitlementSource.Manual,
    DateTime? StartAtUtc = null,
    DateTime? EndAtUtc = null,
    string? OptionOverrideJson = null);

public sealed record SetEntitlementStatusRequest(
    AtlasEntitlementStatus Status);
