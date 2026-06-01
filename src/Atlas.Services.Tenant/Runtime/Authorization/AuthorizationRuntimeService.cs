using Atlas.Core.Authorization;
using Atlas.Core.Entities.Global;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Data.Abstractions;
using Atlas.Data.Global;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Services.Tenant.Runtime.Authorization;

public sealed class AuthorizationRuntimeService :
    IAtlasAuthorizationContextService,
    IAtlasAuthorizationManagementService
{
    private readonly IAtlasAuthorizationCatalog _authorizationCatalog;
    private readonly IAtlasAuthorizationConditionEvaluator _conditionEvaluator;
    private readonly IEntitlementService _entitlementService;
    private readonly AtlasGlobalDbContext _globalDbContext;
    private readonly IRepository<User> _users;
    private readonly IRepository<UserRole> _userRoles;
    private readonly IRepository<Role> _roles;
    private readonly IRepository<Permission> _permissions;
    private readonly IRepository<RolePermission> _rolePermissions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdGenerator _idGenerator;
    private readonly IAtlasPermissionCacheInvalidator _permissionCacheInvalidator;

    public AuthorizationRuntimeService(
        IAtlasAuthorizationCatalog authorizationCatalog,
        IAtlasAuthorizationConditionEvaluator conditionEvaluator,
        IEntitlementService entitlementService,
        AtlasGlobalDbContext globalDbContext,
        IRepository<User> users,
        IRepository<UserRole> userRoles,
        IRepository<Role> roles,
        IRepository<Permission> permissions,
        IRepository<RolePermission> rolePermissions,
        IUnitOfWork unitOfWork,
        IIdGenerator idGenerator,
        IAtlasPermissionCacheInvalidator permissionCacheInvalidator)
    {
        _authorizationCatalog = authorizationCatalog ?? throw new ArgumentNullException(nameof(authorizationCatalog));
        _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
        _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
        _globalDbContext = globalDbContext ?? throw new ArgumentNullException(nameof(globalDbContext));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _userRoles = userRoles ?? throw new ArgumentNullException(nameof(userRoles));
        _roles = roles ?? throw new ArgumentNullException(nameof(roles));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _rolePermissions = rolePermissions ?? throw new ArgumentNullException(nameof(rolePermissions));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _permissionCacheInvalidator = permissionCacheInvalidator ?? throw new ArgumentNullException(nameof(permissionCacheInvalidator));
    }

    public async Task<AtlasAuthorizationContextSnapshot> GetContextAsync(
        AtlasAuthorizationRuntimeContext context,
        CancellationToken ct = default)
    {
        ValidateRuntimeContext(context);

        var availableCapabilities = await _entitlementService.GetAvailableCapabilitiesAsync(
            new EntitlementCheckContext(context.TenantId, context.StoreId),
            ct);
        var availablePermissions = await _entitlementService.GetAvailablePermissionsAsync(
            new EntitlementCheckContext(context.TenantId, context.StoreId),
            ct);
        var user = await GetActiveUserAsync(context, ct);
        var featureFlags = Array.Empty<string>();

        if (user == null)
        {
            return new AtlasAuthorizationContextSnapshot(
                context.TenantId,
                context.UserId,
                context.StoreId,
                Array.Empty<string>(),
                availableCapabilities.OrderBy(x => x).ToArray(),
                featureFlags,
                Array.Empty<AtlasPermissionScopeGrant>());
        }

        if (user.Type is UserType.SystemAdmin or UserType.TenantAdmin)
        {
            var adminDataScopes = availablePermissions
                .OrderBy(x => x)
                .Select(permission => new AtlasPermissionScopeGrant(permission, AtlasDataScopeType.AllTenant, null))
                .ToArray();

            return new AtlasAuthorizationContextSnapshot(
                context.TenantId,
                context.UserId,
                context.StoreId,
                availablePermissions.OrderBy(x => x).ToArray(),
                availableCapabilities.OrderBy(x => x).ToArray(),
                featureFlags,
                adminDataScopes);
        }

        var rows = await GetEffectiveRolePermissionRowsAsync(context, availablePermissions, ct);
        var effectivePermissionSet = rows
            .Select(x => x.PermissionCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (availablePermissions.Contains("identity.self"))
            effectivePermissionSet.Add("identity.self");

        var dataScopeList = rows
            .Select(x => new AtlasPermissionScopeGrant(x.PermissionCode, x.DataScopeType, x.DataScopeJson))
            .Distinct()
            .ToList();
        if (availablePermissions.Contains("identity.self"))
            dataScopeList.Add(new AtlasPermissionScopeGrant("identity.self", AtlasDataScopeType.AllTenant, null));

        return new AtlasAuthorizationContextSnapshot(
            context.TenantId,
            context.UserId,
            context.StoreId,
            effectivePermissionSet.OrderBy(x => x).ToArray(),
            availableCapabilities.OrderBy(x => x).ToArray(),
            featureFlags,
            dataScopeList
                .OrderBy(x => x.PermissionCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.ScopeType)
                .ToArray());
    }

    public async Task<IReadOnlyCollection<AtlasMenuItemNode>> GetMenusAsync(
        AtlasAuthorizationRuntimeContext context,
        CancellationToken ct = default)
    {
        var snapshot = await GetContextAsync(context, ct);
        var permissionSet = snapshot.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var capabilitySet = snapshot.Capabilities.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var featureFlagSet = snapshot.FeatureFlags.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var visibleItems = _authorizationCatalog.MenuItems.Values
            .Where(item => item.IsEnabled &&
                           _conditionEvaluator.IsSatisfied(item.VisibleWhen, permissionSet, capabilitySet, featureFlagSet))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var childrenByParent = visibleItems
            .GroupBy(item => item.ParentCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        return BuildMenuNodes(string.Empty, childrenByParent);
    }

    public async Task<AtlasPermissionExplanation> ExplainPermissionAsync(
        AtlasAuthorizationRuntimeContext context,
        string permissionCode,
        CancellationToken ct = default)
    {
        ValidateRuntimeContext(context);
        var normalizedPermission = NormalizeCode(permissionCode);
        var steps = new List<AtlasPermissionExplainStep>();

        var permissionExists = _authorizationCatalog.Permissions.TryGetValue(normalizedPermission, out var definition) &&
                               definition.IsEnabled;
        steps.Add(new AtlasPermissionExplainStep(
            "catalog",
            permissionExists,
            permissionExists
                ? $"Permission '{normalizedPermission}' is declared and enabled."
                : $"Permission '{normalizedPermission}' is not declared or is disabled."));

        var availableCapabilities = await _entitlementService.GetAvailableCapabilitiesAsync(
            new EntitlementCheckContext(context.TenantId, context.StoreId),
            ct);
        var availablePermissions = await _entitlementService.GetAvailablePermissionsAsync(
            new EntitlementCheckContext(context.TenantId, context.StoreId),
            ct);
        var entitled = availablePermissions.Contains(normalizedPermission);
        steps.Add(new AtlasPermissionExplainStep(
            "entitlement",
            entitled,
            entitled
                ? "Tenant/store entitlement includes this permission."
                : "Tenant/store entitlement does not include this permission."));

        var user = await GetActiveUserAsync(context, ct);
        var userActive = user != null;
        steps.Add(new AtlasPermissionExplainStep(
            "user",
            userActive,
            userActive ? "User exists and is active." : "User is missing, deleted, or inactive."));

        IReadOnlyCollection<string> roles = Array.Empty<string>();
        IReadOnlyCollection<AtlasPermissionScopeGrant> dataScopes = Array.Empty<AtlasPermissionScopeGrant>();
        var rbacAllowed = false;

        if (userActive)
        {
            if (string.Equals(normalizedPermission, "identity.self", StringComparison.OrdinalIgnoreCase))
            {
                rbacAllowed = true;
                roles = new[] { "AuthenticatedUser" };
                dataScopes = new[]
                {
                    new AtlasPermissionScopeGrant(normalizedPermission, AtlasDataScopeType.AllTenant, null)
                };
                steps.Add(new AtlasPermissionExplainStep(
                    "rbac",
                    true,
                    "Active authenticated users receive identity.self."));
            }
            else if (user!.Type is UserType.SystemAdmin or UserType.TenantAdmin)
            {
                rbacAllowed = true;
                roles = new[] { user.Type.ToString() };
                dataScopes = new[]
                {
                    new AtlasPermissionScopeGrant(normalizedPermission, AtlasDataScopeType.AllTenant, null)
                };
                steps.Add(new AtlasPermissionExplainStep(
                    "rbac",
                    true,
                    $"{user.Type} receives all entitled permissions."));
            }
            else
            {
                var allRows = await GetRolePermissionRowsAsync(context, ct);
                var deniedRows = allRows
                    .Where(x => string.Equals(x.PermissionCode, normalizedPermission, StringComparison.OrdinalIgnoreCase) &&
                                x.Effect == RolePermissionEffect.Deny)
                    .ToArray();
                var allowedRows = allRows
                    .Where(x => string.Equals(x.PermissionCode, normalizedPermission, StringComparison.OrdinalIgnoreCase) &&
                                x.Effect == RolePermissionEffect.Allow)
                    .ToArray();

                rbacAllowed = allowedRows.Length > 0 && deniedRows.Length == 0;
                roles = (deniedRows.Length > 0 ? deniedRows : allowedRows)
                    .Select(x => x.RoleName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToArray();
                dataScopes = allowedRows
                    .Select(x => new AtlasPermissionScopeGrant(x.PermissionCode, x.DataScopeType, x.DataScopeJson))
                    .Distinct()
                    .ToArray();

                steps.Add(new AtlasPermissionExplainStep(
                    "rbac",
                    rbacAllowed,
                    deniedRows.Length > 0
                        ? "A role explicitly denies this permission."
                        : allowedRows.Length > 0
                            ? "At least one role allows this permission."
                            : "No enabled role allows this permission."));
            }
        }

        var allowed = permissionExists && entitled && userActive && rbacAllowed;
        steps.Add(new AtlasPermissionExplainStep(
            "effective",
            allowed,
            allowed
                ? "Effective permission is granted."
                : "Effective permission is denied."));

        return new AtlasPermissionExplanation(
            context.TenantId,
            context.UserId,
            context.StoreId,
            normalizedPermission,
            allowed,
            availableCapabilities.OrderBy(x => x).ToArray(),
            roles,
            dataScopes,
            steps);
    }

    public Task<AtlasAuthorizationCatalogSnapshot> GetCatalogAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new AtlasAuthorizationCatalogSnapshot(
            _authorizationCatalog.Capabilities.Values.OrderBy(x => x.Code).ToArray(),
            _authorizationCatalog.Permissions.Values.OrderBy(x => x.Code).ToArray(),
            _authorizationCatalog.Packages.Values.OrderBy(x => x.Code).ToArray(),
            _authorizationCatalog.PackageCapabilities
                .OrderBy(x => x.PackageCode)
                .ThenBy(x => x.CapabilityCode)
                .ToArray(),
            _authorizationCatalog.MenuItems.Values.OrderBy(x => x.Code).ToArray(),
            _authorizationCatalog.DataResources.Values.OrderBy(x => x.Code).ToArray()));
    }

    public async Task<IReadOnlyCollection<AtlasTenantEntitlementInfo>> GetTenantEntitlementsAsync(
        long tenantId,
        CancellationToken ct = default)
    {
        if (tenantId <= 0)
            throw new ArgumentOutOfRangeException(nameof(tenantId));

        var entities = await _globalDbContext.TenantEntitlements
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(ToEntitlementInfo).ToArray();
    }

    public async Task<AtlasTenantEntitlementInfo> GrantEntitlementAsync(
        AtlasGrantEntitlementCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TenantId <= 0)
            throw new ArgumentOutOfRangeException(nameof(command.TenantId));

        var packageCode = string.IsNullOrWhiteSpace(command.PackageCode) ? null : NormalizeCode(command.PackageCode);
        var capabilityCode = string.IsNullOrWhiteSpace(command.CapabilityCode) ? null : NormalizeCode(command.CapabilityCode);
        if ((packageCode == null) == (capabilityCode == null))
            throw new ArgumentException("Exactly one package or capability must be granted.", nameof(command));

        if (packageCode != null && !_authorizationCatalog.Packages.ContainsKey(packageCode))
            throw new InvalidOperationException($"Package '{packageCode}' is not declared.");

        if (capabilityCode != null && !_authorizationCatalog.Capabilities.ContainsKey(capabilityCode))
            throw new InvalidOperationException($"Capability '{capabilityCode}' is not declared.");

        var subjectId = command.SubjectId > 0
            ? command.SubjectId
            : command.SubjectType == AtlasEntitlementSubjectType.Tenant
                ? command.TenantId
                : throw new ArgumentException("SubjectId is required for non-tenant entitlement subjects.", nameof(command));
        var entity = new TenantEntitlement
        {
            TenantId = command.TenantId,
            SubjectType = command.SubjectType,
            SubjectId = subjectId,
            PackageCode = packageCode,
            CapabilityCode = capabilityCode,
            Source = command.Source,
            StartAtUtc = command.StartAtUtc ?? DateTime.UtcNow,
            EndAtUtc = command.EndAtUtc,
            Status = AtlasEntitlementStatus.Active,
            OptionOverrideJson = command.OptionOverrideJson,
            CreatedAt = DateTime.UtcNow
        };

        await _globalDbContext.TenantEntitlements.AddAsync(entity, ct);
        await _globalDbContext.SaveChangesAsync(ct);
        await InvalidateEntitlementCacheAsync(entity, ct);
        return ToEntitlementInfo(entity);
    }

    public async Task<AtlasTenantEntitlementInfo?> SetEntitlementStatusAsync(
        AtlasSetEntitlementStatusCommand command,
        CancellationToken ct = default)
    {
        var entity = await _globalDbContext.TenantEntitlements
            .FirstOrDefaultAsync(x => x.Id == command.EntitlementId, ct);
        if (entity == null)
            return null;

        entity.Status = command.Status;
        entity.UpdatedAt = DateTime.UtcNow;
        await _globalDbContext.SaveChangesAsync(ct);
        await InvalidateEntitlementCacheAsync(entity, ct);
        return ToEntitlementInfo(entity);
    }

    public async Task<IReadOnlyCollection<AtlasRolePermissionInfo>> GetRolePermissionsAsync(
        long tenantId,
        long roleId,
        CancellationToken ct = default)
    {
        if (tenantId <= 0 || roleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(roleId));

        return await GetRolePermissionInfosAsync(tenantId, roleId, ct);
    }

    public async Task<IReadOnlyCollection<AtlasRolePermissionInfo>> SetRolePermissionsAsync(
        long tenantId,
        long roleId,
        IReadOnlyCollection<AtlasSetRolePermissionCommand> permissions,
        CancellationToken ct = default)
    {
        if (tenantId <= 0 || roleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(roleId));
        ArgumentNullException.ThrowIfNull(permissions);

        var roleQuery = await _roles.QueryAsync(tenantId, ct);
        var role = await roleQuery
            .Where(x => x.TenantId == tenantId && x.Id == roleId)
            .FirstOrDefaultAsync(ct);
        if (role == null)
            throw new InvalidOperationException($"Role '{roleId}' does not exist.");

        var requested = permissions
            .GroupBy(x => NormalizeCode(x.PermissionCode), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();
        var requestedCodes = requested
            .Select(x => NormalizeCode(x.PermissionCode))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var permissionQuery = await _permissions.QueryAsync(tenantId, ct);
        var tenantPermissions = await permissionQuery
            .Where(x => x.TenantId == tenantId && x.IsEnabled && requestedCodes.Contains(x.Code))
            .ToListAsync(ct);
        var permissionsByCode = tenantPermissions.ToDictionary(x => NormalizeCode(x.Code), StringComparer.OrdinalIgnoreCase);
        var missingCodes = requestedCodes
            .Where(code => !permissionsByCode.ContainsKey(code))
            .ToArray();
        if (missingCodes.Length > 0)
            throw new InvalidOperationException($"Permissions are not declared for tenant {tenantId}: {string.Join(", ", missingCodes)}.");

        var existingQuery = await _rolePermissions.QueryTrackingAsync(tenantId, ct);
        var existing = await existingQuery
            .Where(x => x.TenantId == tenantId && x.RoleId == roleId)
            .ToListAsync(ct);
        if (existing.Count > 0)
            await _rolePermissions.RemoveRangeAsync(existing, tenantId, ct);

        foreach (var item in requested)
        {
            var permission = permissionsByCode[NormalizeCode(item.PermissionCode)];
            await _rolePermissions.AddAsync(new RolePermission
            {
                Id = _idGenerator.NextId(),
                TenantId = tenantId,
                RoleId = roleId,
                PermissionId = permission.Id,
                Effect = item.Effect,
                DataScopeType = item.DataScopeType,
                DataScopeJson = item.DataScopeJson,
                GrantedAt = DateTime.UtcNow
            }, tenantId, ct);
        }

        await _unitOfWork.SaveChangesAsync(tenantId, ct);
        await InvalidateRoleUsersAsync(tenantId, roleId, ct);
        return await GetRolePermissionInfosAsync(tenantId, roleId, ct);
    }

    private async Task<User?> GetActiveUserAsync(
        AtlasAuthorizationRuntimeContext context,
        CancellationToken ct)
    {
        var userQuery = await _users.QueryAsync(context.TenantId, ct);
        return await userQuery
            .Where(x => x.Id == context.UserId &&
                        x.TenantId == context.TenantId &&
                        !x.IsDeleted &&
                        x.Status == UserStatus.Active)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<IReadOnlyCollection<RolePermissionRuntimeRow>> GetEffectiveRolePermissionRowsAsync(
        AtlasAuthorizationRuntimeContext context,
        IReadOnlySet<string> availablePermissions,
        CancellationToken ct)
    {
        var allRows = await GetRolePermissionRowsAsync(context, ct);
        var deniedPermissionIds = allRows
            .Where(x => x.Effect == RolePermissionEffect.Deny)
            .Select(x => x.PermissionId)
            .ToHashSet();

        return allRows
            .Where(x => x.Effect == RolePermissionEffect.Allow &&
                        !deniedPermissionIds.Contains(x.PermissionId) &&
                        availablePermissions.Contains(x.PermissionCode))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<RolePermissionRuntimeRow>> GetRolePermissionRowsAsync(
        AtlasAuthorizationRuntimeContext context,
        CancellationToken ct)
    {
        var userRoleQuery = await _userRoles.QueryAsync(context.TenantId, ct);
        var userRoles = await userRoleQuery
            .Where(x => x.TenantId == context.TenantId &&
                        x.UserId == context.UserId &&
                        (x.StoreId == 0 || (context.StoreId.HasValue && x.StoreId == context.StoreId.Value)))
            .ToListAsync(ct);
        var roleIds = userRoles.Select(x => x.RoleId).Distinct().ToArray();
        if (roleIds.Length == 0)
            return Array.Empty<RolePermissionRuntimeRow>();

        var roleQuery = await _roles.QueryAsync(context.TenantId, ct);
        var roles = await roleQuery
            .Where(x => x.TenantId == context.TenantId &&
                        roleIds.Contains(x.Id) &&
                        x.IsEnabled &&
                        (x.StoreId == null || (context.StoreId.HasValue && x.StoreId == context.StoreId.Value)))
            .ToListAsync(ct);
        var rolesById = roles.ToDictionary(x => x.Id);
        var enabledRoleIds = rolesById.Keys.ToArray();
        if (enabledRoleIds.Length == 0)
            return Array.Empty<RolePermissionRuntimeRow>();

        var rolePermissionQuery = await _rolePermissions.QueryAsync(context.TenantId, ct);
        var rolePermissions = await rolePermissionQuery
            .Where(x => x.TenantId == context.TenantId && enabledRoleIds.Contains(x.RoleId))
            .ToListAsync(ct);
        var permissionIds = rolePermissions.Select(x => x.PermissionId).Distinct().ToArray();
        if (permissionIds.Length == 0)
            return Array.Empty<RolePermissionRuntimeRow>();

        var permissionQuery = await _permissions.QueryAsync(context.TenantId, ct);
        var permissions = await permissionQuery
            .Where(x => x.TenantId == context.TenantId &&
                        permissionIds.Contains(x.Id) &&
                        x.IsEnabled)
            .ToListAsync(ct);
        var permissionsById = permissions.ToDictionary(x => x.Id);

        return rolePermissions
            .Where(x => permissionsById.ContainsKey(x.PermissionId) && rolesById.ContainsKey(x.RoleId))
            .Select(x =>
            {
                var permission = permissionsById[x.PermissionId];
                var role = rolesById[x.RoleId];
                return new RolePermissionRuntimeRow(
                    x.Id,
                    x.RoleId,
                    role.Name,
                    x.PermissionId,
                    NormalizeCode(permission.Code),
                    permission.Name,
                    x.Effect,
                    x.DataScopeType,
                    x.DataScopeJson);
            })
            .ToArray();
    }

    private async Task<IReadOnlyCollection<AtlasRolePermissionInfo>> GetRolePermissionInfosAsync(
        long tenantId,
        long roleId,
        CancellationToken ct)
    {
        var rolePermissionQuery = await _rolePermissions.QueryAsync(tenantId, ct);
        var rolePermissions = await rolePermissionQuery
            .Where(x => x.TenantId == tenantId && x.RoleId == roleId)
            .ToListAsync(ct);
        if (rolePermissions.Count == 0)
            return Array.Empty<AtlasRolePermissionInfo>();

        var permissionIds = rolePermissions.Select(x => x.PermissionId).Distinct().ToArray();
        var permissionQuery = await _permissions.QueryAsync(tenantId, ct);
        var permissions = await permissionQuery
            .Where(x => x.TenantId == tenantId && permissionIds.Contains(x.Id))
            .ToListAsync(ct);
        var permissionsById = permissions.ToDictionary(x => x.Id);

        return rolePermissions
            .Where(x => permissionsById.ContainsKey(x.PermissionId))
            .Select(x =>
            {
                var permission = permissionsById[x.PermissionId];
                return new AtlasRolePermissionInfo(
                    x.Id,
                    x.RoleId,
                    x.PermissionId,
                    NormalizeCode(permission.Code),
                    permission.Name,
                    x.Effect,
                    x.DataScopeType,
                    x.DataScopeJson);
            })
            .OrderBy(x => x.PermissionCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task InvalidateRoleUsersAsync(long tenantId, long roleId, CancellationToken ct)
    {
        var userRoleQuery = await _userRoles.QueryAsync(tenantId, ct);
        var grants = await userRoleQuery
            .Where(x => x.TenantId == tenantId && x.RoleId == roleId)
            .ToListAsync(ct);

        foreach (var grant in grants)
        {
            await _permissionCacheInvalidator.InvalidateUserPermissionsAsync(
                tenantId,
                grant.UserId,
                grant.StoreId == 0 ? null : grant.StoreId,
                ct);
        }
    }

    private async Task InvalidateEntitlementCacheAsync(TenantEntitlement entitlement, CancellationToken ct)
    {
        var storeId = entitlement.SubjectType == AtlasEntitlementSubjectType.Store
            ? entitlement.SubjectId
            : (long?)null;

        await _entitlementService.InvalidateEntitlementsAsync(entitlement.TenantId, storeId, ct);
    }

    private static IReadOnlyCollection<AtlasMenuItemNode> BuildMenuNodes(
        string parentCode,
        IReadOnlyDictionary<string, AtlasMenuItemDefinition[]> childrenByParent)
    {
        if (!childrenByParent.TryGetValue(parentCode, out var children))
            return Array.Empty<AtlasMenuItemNode>();

        return children
            .Select(item => new AtlasMenuItemNode(
                item.Code,
                item.Name,
                item.Route,
                item.Icon,
                item.SortOrder,
                BuildMenuNodes(item.Code, childrenByParent)))
            .ToArray();
    }

    private static AtlasTenantEntitlementInfo ToEntitlementInfo(TenantEntitlement entity)
    {
        return new AtlasTenantEntitlementInfo(
            entity.Id,
            entity.TenantId,
            entity.SubjectType,
            entity.SubjectId,
            entity.PackageCode,
            entity.CapabilityCode,
            entity.Source,
            entity.StartAtUtc,
            entity.EndAtUtc,
            entity.Status,
            entity.OptionOverrideJson);
    }

    private static void ValidateRuntimeContext(AtlasAuthorizationRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TenantId <= 0)
            throw new ArgumentOutOfRangeException(nameof(context.TenantId));
        if (context.UserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(context.UserId));
    }

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        return code.Trim().ToLowerInvariant();
    }

    private sealed record RolePermissionRuntimeRow(
        long RolePermissionId,
        long RoleId,
        string RoleName,
        long PermissionId,
        string PermissionCode,
        string PermissionName,
        RolePermissionEffect Effect,
        AtlasDataScopeType DataScopeType,
        string? DataScopeJson);
}
