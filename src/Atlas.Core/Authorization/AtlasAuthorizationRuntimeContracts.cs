using Atlas.Core.Enums;

namespace Atlas.Core.Authorization;

public sealed record AtlasAuthorizationRuntimeContext(
    long TenantId,
    long UserId,
    long? StoreId = null);

public sealed record AtlasPermissionScopeGrant(
    string PermissionCode,
    AtlasDataScopeType ScopeType,
    string? ScopeJson);

public sealed record AtlasAuthorizationContextSnapshot(
    long TenantId,
    long UserId,
    long? StoreId,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<string> Capabilities,
    IReadOnlyCollection<string> FeatureFlags,
    IReadOnlyCollection<AtlasPermissionScopeGrant> DataScopes);

public sealed record AtlasMenuItemNode(
    string Code,
    string Name,
    string Route,
    string? Icon,
    int SortOrder,
    IReadOnlyCollection<AtlasMenuItemNode> Children);

public sealed record AtlasPermissionExplainStep(
    string Key,
    bool Passed,
    string Message);

public sealed record AtlasPermissionExplanation(
    long TenantId,
    long UserId,
    long? StoreId,
    string PermissionCode,
    bool Allowed,
    IReadOnlyCollection<string> Capabilities,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<AtlasPermissionScopeGrant> DataScopes,
    IReadOnlyCollection<AtlasPermissionExplainStep> Steps);

public sealed record AtlasAuthorizationCatalogSnapshot(
    IReadOnlyCollection<AtlasCapabilityDefinition> Capabilities,
    IReadOnlyCollection<AtlasPermissionDefinition> Permissions,
    IReadOnlyCollection<AtlasPackageDefinition> Packages,
    IReadOnlyCollection<AtlasPackageCapabilityDefinition> PackageCapabilities,
    IReadOnlyCollection<AtlasMenuItemDefinition> MenuItems,
    IReadOnlyCollection<AtlasDataResourceDefinition> DataResources);

public sealed record AtlasTenantEntitlementInfo(
    long Id,
    long TenantId,
    AtlasEntitlementSubjectType SubjectType,
    long SubjectId,
    string? PackageCode,
    string? CapabilityCode,
    AtlasEntitlementSource Source,
    DateTime StartAtUtc,
    DateTime? EndAtUtc,
    AtlasEntitlementStatus Status,
    string? OptionOverrideJson);

public sealed record AtlasGrantEntitlementCommand(
    long TenantId,
    AtlasEntitlementSubjectType SubjectType,
    long SubjectId,
    string? PackageCode,
    string? CapabilityCode,
    AtlasEntitlementSource Source,
    DateTime? StartAtUtc,
    DateTime? EndAtUtc,
    string? OptionOverrideJson);

public sealed record AtlasSetEntitlementStatusCommand(
    long EntitlementId,
    AtlasEntitlementStatus Status);

public sealed record AtlasRolePermissionInfo(
    long RolePermissionId,
    long RoleId,
    long PermissionId,
    string PermissionCode,
    string PermissionName,
    RolePermissionEffect Effect,
    AtlasDataScopeType DataScopeType,
    string? DataScopeJson);

public sealed record AtlasSetRolePermissionCommand(
    string PermissionCode,
    RolePermissionEffect Effect,
    AtlasDataScopeType DataScopeType,
    string? DataScopeJson);

public interface IAtlasAuthorizationContextService
{
    Task<AtlasAuthorizationContextSnapshot> GetContextAsync(
        AtlasAuthorizationRuntimeContext context,
        CancellationToken ct = default);

    Task<IReadOnlyCollection<AtlasMenuItemNode>> GetMenusAsync(
        AtlasAuthorizationRuntimeContext context,
        CancellationToken ct = default);

    Task<AtlasPermissionExplanation> ExplainPermissionAsync(
        AtlasAuthorizationRuntimeContext context,
        string permissionCode,
        CancellationToken ct = default);
}

public interface IAtlasAuthorizationManagementService
{
    Task<AtlasAuthorizationCatalogSnapshot> GetCatalogAsync(CancellationToken ct = default);

    Task<IReadOnlyCollection<AtlasTenantEntitlementInfo>> GetTenantEntitlementsAsync(
        long tenantId,
        CancellationToken ct = default);

    Task<AtlasTenantEntitlementInfo> GrantEntitlementAsync(
        AtlasGrantEntitlementCommand command,
        CancellationToken ct = default);

    Task<AtlasTenantEntitlementInfo?> SetEntitlementStatusAsync(
        AtlasSetEntitlementStatusCommand command,
        CancellationToken ct = default);

    Task<IReadOnlyCollection<AtlasRolePermissionInfo>> GetRolePermissionsAsync(
        long tenantId,
        long roleId,
        CancellationToken ct = default);

    Task<IReadOnlyCollection<AtlasRolePermissionInfo>> SetRolePermissionsAsync(
        long tenantId,
        long roleId,
        IReadOnlyCollection<AtlasSetRolePermissionCommand> permissions,
        CancellationToken ct = default);
}

public interface IAtlasPermissionCacheInvalidator
{
    Task InvalidateUserPermissionsAsync(
        long tenantId,
        long userId,
        long? storeId = null,
        CancellationToken ct = default);
}
