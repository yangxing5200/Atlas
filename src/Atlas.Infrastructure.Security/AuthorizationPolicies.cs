using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Data.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Security;

public static class AuthorizationPolicies
{
    public const string RequireTenantAdmin = nameof(RequireTenantAdmin);
}

public sealed class TenantAdminRequirement : IAuthorizationRequirement
{
}

public sealed class TenantAdminAuthorizationHandler : AuthorizationHandler<TenantAdminRequirement>
{
    private readonly IRepository<User> _users;
    private readonly ILogger<TenantAdminAuthorizationHandler> _logger;

    public TenantAdminAuthorizationHandler(
        IRepository<User> users,
        ILogger<TenantAdminAuthorizationHandler> logger)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantAdminRequirement requirement)
    {
        if (!TryGetClaimValue(context, "uid", out var userId) ||
            !TryGetClaimValue(context, "tid", out var tenantId))
        {
            return;
        }

        try
        {
            var users = await _users.QueryAsync(tenantId);
            var isAdmin = await users.Where(user =>
                    user.Id == userId &&
                    !user.IsDeleted &&
                    user.Status == UserStatus.Active &&
                    (user.Type == UserType.TenantAdmin || user.Type == UserType.SystemAdmin))
                .AnyAsync();

            if (isAdmin)
                context.Succeed(requirement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tenant admin authorization failed for user {UserId}", userId);
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
