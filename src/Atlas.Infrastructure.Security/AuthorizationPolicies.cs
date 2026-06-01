using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Data.Abstractions;
using Atlas.Infrastructure.Security.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Security;

/// <summary>
/// Atlas 内置授权策略名称。
/// </summary>
public static class AuthorizationPolicies
{
    public const string RequireTenantAdmin = nameof(RequireTenantAdmin);
    public const string PermissionPrefix = "Permission:";
    public const string RequireIdentitySelf = PermissionPrefix + AtlasPermissionCodes.IdentitySelf;
    public const string RequireUsersRead = PermissionPrefix + AtlasPermissionCodes.UsersRead;
    public const string RequireUsersManage = PermissionPrefix + AtlasPermissionCodes.UsersManage;
    public const string RequireRolesManage = PermissionPrefix + AtlasPermissionCodes.RolesManage;
    public const string RequireStoresRead = PermissionPrefix + AtlasPermissionCodes.StoresRead;
    public const string RequireStoresManage = PermissionPrefix + AtlasPermissionCodes.StoresManage;
    public const string RequireTenantProvisioning = PermissionPrefix + AtlasPermissionCodes.TenantProvisioning;
    public const string RequireAuditRead = PermissionPrefix + AtlasPermissionCodes.AuditRead;
    public const string RequireAuthorizationRead = PermissionPrefix + AtlasPermissionCodes.AuthorizationRead;
    public const string RequireAuthorizationManage = PermissionPrefix + AtlasPermissionCodes.AuthorizationManage;
    public const string RequireUsersSensitiveReveal = PermissionPrefix + AtlasPermissionCodes.UsersSensitiveReveal;
    public const string RequireAuditSensitiveReveal = PermissionPrefix + AtlasPermissionCodes.AuditSensitiveReveal;

    public static string RequirePermission(string permissionCode)
    {
        return $"{PermissionPrefix}{permissionCode}";
    }

    public static bool TryParsePermissionPolicy(string policyName, out string permissionCode)
    {
        permissionCode = string.Empty;
        if (!policyName.StartsWith(PermissionPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        permissionCode = policyName[PermissionPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(permissionCode);
    }
}

/// <summary>
/// 要求当前用户是租户管理员或系统管理员。
/// </summary>
public sealed class TenantAdminRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// 基于租户库用户状态验证租户管理员权限。
/// </summary>
/// <remarks>
/// 不完全信任 Token 中的角色声明，而是回查当前租户库，确保禁用、删除或降权能及时生效。
/// </remarks>
public sealed class TenantAdminAuthorizationHandler : AuthorizationHandler<TenantAdminRequirement>
{
    private readonly IRepository<User> _users;
    private readonly IPermissionChecker _permissionChecker;
    private readonly ILogger<TenantAdminAuthorizationHandler> _logger;

    public TenantAdminAuthorizationHandler(
        IRepository<User> users,
        IPermissionChecker permissionChecker,
        ILogger<TenantAdminAuthorizationHandler> logger)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
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
            {
                context.Succeed(requirement);
                return;
            }

            var hasTenantAdminPermission = await _permissionChecker.HasPermissionAsync(
                new PermissionCheckContext(
                    tenantId,
                    userId,
                    null,
                    AtlasPermissionCodes.TenantAdmin));

            if (hasTenantAdminPermission)
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
