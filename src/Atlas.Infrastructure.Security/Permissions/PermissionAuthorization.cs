using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Security.Permissions;

public sealed record PermissionCheckContext(
    long TenantId,
    long UserId,
    long? StoreId,
    string PermissionCode);

public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(PermissionCheckContext context, CancellationToken ct = default);

    Task InvalidateUserPermissionsAsync(
        long tenantId,
        long userId,
        long? storeId = null,
        CancellationToken ct = default);
}

public interface IRbacSeedService
{
    Task SeedTenantAsync(long tenantId, CancellationToken ct = default);
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = string.IsNullOrWhiteSpace(permissionCode)
            ? throw new ArgumentException("Permission code is required.", nameof(permissionCode))
            : permissionCode.Trim();
    }

    public string PermissionCode { get; }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionChecker _permissionChecker;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IPermissionChecker permissionChecker,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!TryGetClaimValue(context, "uid", out var userId) ||
            !TryGetClaimValue(context, "tid", out var tenantId))
        {
            return;
        }

        TryGetClaimValue(context, "sid", out var storeId);

        try
        {
            var allowed = await _permissionChecker.HasPermissionAsync(
                new PermissionCheckContext(
                    tenantId,
                    userId,
                    storeId == 0 ? null : storeId,
                    requirement.PermissionCode));

            if (allowed)
                context.Succeed(requirement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Permission authorization failed for user {UserId}, tenant {TenantId}, permission {PermissionCode}",
                userId,
                tenantId,
                requirement.PermissionCode);
        }
    }

    private static bool TryGetClaimValue(
        AuthorizationHandlerContext context,
        string claimType,
        out long value)
    {
        value = 0;
        var rawValue = context.User.FindFirst(claimType)?.Value;
        return long.TryParse(rawValue, out value);
    }
}

public sealed class PermissionAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (AuthorizationPolicies.TryParsePermissionPolicy(policyName, out var permissionCode))
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permissionCode))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}
