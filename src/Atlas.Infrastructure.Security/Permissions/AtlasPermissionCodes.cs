using Atlas.Core.Enums;

namespace Atlas.Infrastructure.Security.Permissions;

public static class AtlasPermissionCodes
{
    public const string TenantAdmin = "tenant.admin";
    public const string IdentitySelf = "identity.self";
    public const string UsersRead = "users.read";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string StoresRead = "stores.read";
    public const string StoresManage = "stores.manage";
    public const string TenantProvisioning = "tenant.provisioning";
    public const string AuditRead = "audit.read";
    public const string AuthorizationRead = "authorization.read";
    public const string AuthorizationManage = "authorization.manage";

    public static IReadOnlyList<BuiltInPermissionDefinition> All { get; } =
    [
        new(TenantAdmin, "Tenant administration", "Tenant", PermissionScope.Tenant),
        new(IdentitySelf, "Manage own identity session", "Security", PermissionScope.Tenant),
        new(UsersRead, "Read users", "User", PermissionScope.Tenant),
        new(UsersManage, "Manage users", "User", PermissionScope.Tenant),
        new(RolesManage, "Manage roles and permissions", "Security", PermissionScope.Tenant),
        new(StoresRead, "Read stores", "Store", PermissionScope.Store),
        new(StoresManage, "Manage stores", "Store", PermissionScope.Store),
        new(TenantProvisioning, "Provision tenants", "Tenant", PermissionScope.Platform),
        new(AuditRead, "Read audit events", "Audit", PermissionScope.Tenant),
        new(AuthorizationRead, "Read authorization catalog and diagnostics", "Security", PermissionScope.Tenant),
        new(AuthorizationManage, "Manage entitlements and role permissions", "Security", PermissionScope.Tenant)
    ];
}

public sealed record BuiltInPermissionDefinition(
    string Code,
    string Name,
    string Module,
    PermissionScope Scope,
    string? Description = null);
