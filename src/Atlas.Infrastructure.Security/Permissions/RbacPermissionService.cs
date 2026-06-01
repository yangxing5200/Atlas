using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Data.Abstractions;
using Atlas.Infrastructure.Caching.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Security.Permissions;

public sealed class RbacPermissionService : IPermissionChecker, IRbacSeedService
{
    private const string TenantAdminRoleCode = "tenant-admin";
    private readonly IRepository<User> _users;
    private readonly IRepository<UserRole> _userRoles;
    private readonly IRepository<Role> _roles;
    private readonly IRepository<Permission> _permissions;
    private readonly IRepository<RolePermission> _rolePermissions;
    private readonly ICacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdGenerator _idGenerator;
    private readonly ILogger<RbacPermissionService> _logger;

    public RbacPermissionService(
        IRepository<User> users,
        IRepository<UserRole> userRoles,
        IRepository<Role> roles,
        IRepository<Permission> permissions,
        IRepository<RolePermission> rolePermissions,
        ICacheService cache,
        IUnitOfWork unitOfWork,
        IIdGenerator idGenerator,
        ILogger<RbacPermissionService> logger)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _userRoles = userRoles ?? throw new ArgumentNullException(nameof(userRoles));
        _roles = roles ?? throw new ArgumentNullException(nameof(roles));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _rolePermissions = rolePermissions ?? throw new ArgumentNullException(nameof(rolePermissions));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> HasPermissionAsync(PermissionCheckContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TenantId <= 0 || context.UserId <= 0 || string.IsNullOrWhiteSpace(context.PermissionCode))
            return false;

        var normalizedPermission = NormalizeCode(context.PermissionCode);
        var userQuery = await _users.QueryAsync(context.TenantId, ct);
        var user = await userQuery
            .Where(x => x.Id == context.UserId &&
                        x.TenantId == context.TenantId &&
                        !x.IsDeleted &&
                        x.Status == UserStatus.Active)
            .FirstOrDefaultAsync(ct);

        if (user == null)
            return false;

        // Compatibility path: existing TenantAdmin/SystemAdmin users keep access while teams migrate to explicit RBAC rows.
        if (user.Type is UserType.SystemAdmin or UserType.TenantAdmin)
            return true;

        var permissionCodes = await GetUserPermissionCodesAsync(
            context.TenantId,
            context.UserId,
            context.StoreId,
            ct);

        return permissionCodes.Contains(normalizedPermission, StringComparer.OrdinalIgnoreCase);
    }

    public Task InvalidateUserPermissionsAsync(
        long tenantId,
        long userId,
        long? storeId = null,
        CancellationToken ct = default)
    {
        if (tenantId <= 0 || userId <= 0)
            return Task.CompletedTask;

        _cache.Remove(BuildPermissionCacheKey(tenantId, userId, null));
        if (storeId.HasValue)
            _cache.Remove(BuildPermissionCacheKey(tenantId, userId, storeId.Value));

        return Task.CompletedTask;
    }

    public async Task SeedTenantAsync(long tenantId, CancellationToken ct = default)
    {
        if (tenantId <= 0)
            throw new ArgumentOutOfRangeException(nameof(tenantId), "Tenant id must be greater than zero.");

        var existingPermissionQuery = await _permissions.QueryAsync(tenantId, ct);
        var existingPermissions = await existingPermissionQuery
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(ct);
        var existingCodes = existingPermissions
            .Select(x => NormalizeCode(x.Code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in AtlasPermissionCodes.All)
        {
            if (existingCodes.Contains(NormalizeCode(definition.Code)))
                continue;

            await _permissions.AddAsync(new Permission
            {
                Id = _idGenerator.NextId(),
                TenantId = tenantId,
                Code = NormalizeCode(definition.Code),
                Name = definition.Name,
                Description = definition.Description,
                Module = definition.Module,
                Scope = definition.Scope,
                IsBuiltIn = true,
                IsEnabled = true
            }, tenantId, ct);
        }

        await _unitOfWork.SaveChangesAsync(tenantId, ct);

        var roleQuery = await _roles.QueryAsync(tenantId, ct);
        var adminRole = await roleQuery
            .Where(x => x.TenantId == tenantId && x.Code == TenantAdminRoleCode)
            .FirstOrDefaultAsync(ct);

        if (adminRole == null)
        {
            adminRole = new Role
            {
                Id = _idGenerator.NextId(),
                TenantId = tenantId,
                Code = TenantAdminRoleCode,
                Name = "Tenant administrator",
                Description = "Built-in role with all default tenant permissions.",
                Scope = PermissionScope.Tenant,
                IsSystem = true,
                IsEnabled = true
            };
            await _roles.AddAsync(adminRole, tenantId, ct);
            await _unitOfWork.SaveChangesAsync(tenantId, ct);
        }

        var refreshedPermissionQuery = await _permissions.QueryAsync(tenantId, ct);
        var permissions = await refreshedPermissionQuery
            .Where(x => x.TenantId == tenantId && x.IsEnabled)
            .ToListAsync(ct);
        var rolePermissionQuery = await _rolePermissions.QueryAsync(tenantId, ct);
        var existingPermissionIds = await rolePermissionQuery
            .Where(x => x.TenantId == tenantId && x.RoleId == adminRole.Id)
            .Select(x => new RolePermissionIdProjection { PermissionId = x.PermissionId })
            .ToListAsync(ct);
        var granted = existingPermissionIds
            .Select(x => x.PermissionId)
            .ToHashSet();

        foreach (var permission in permissions)
        {
            if (granted.Contains(permission.Id))
                continue;

            await _rolePermissions.AddAsync(new RolePermission
            {
                Id = _idGenerator.NextId(),
                TenantId = tenantId,
                RoleId = adminRole.Id,
                PermissionId = permission.Id,
                GrantedAt = DateTime.UtcNow
            }, tenantId, ct);
        }

        await _unitOfWork.SaveChangesAsync(tenantId, ct);
        _logger.LogInformation("Seeded RBAC permissions for tenant {TenantId}", tenantId);
    }

    private async Task<IReadOnlyCollection<string>> GetUserPermissionCodesAsync(
        long tenantId,
        long userId,
        long? storeId,
        CancellationToken ct)
    {
        var cacheKey = BuildPermissionCacheKey(tenantId, userId, storeId);
        var cached = _cache.Get<string[]>(cacheKey);
        if (cached is { Length: > 0 })
            return cached;

        var userRoleQuery = await _userRoles.QueryAsync(tenantId, ct);
        var userRoles = await userRoleQuery
            .Where(x => x.TenantId == tenantId &&
                        x.UserId == userId &&
                        (x.StoreId == 0 || (storeId.HasValue && x.StoreId == storeId.Value)))
            .ToListAsync(ct);
        var roleIds = userRoles.Select(x => x.RoleId).Distinct().ToArray();
        if (roleIds.Length == 0)
        {
            _cache.Set(cacheKey, Array.Empty<string>(), TimeSpan.FromMinutes(5));
            return Array.Empty<string>();
        }

        var roleQuery = await _roles.QueryAsync(tenantId, ct);
        var roles = await roleQuery
            .Where(x => x.TenantId == tenantId &&
                        roleIds.Contains(x.Id) &&
                        x.IsEnabled &&
                        (x.StoreId == null || (storeId.HasValue && x.StoreId == storeId.Value)))
            .ToListAsync(ct);
        var enabledRoleIds = roles.Select(x => x.Id).Distinct().ToArray();
        if (enabledRoleIds.Length == 0)
        {
            _cache.Set(cacheKey, Array.Empty<string>(), TimeSpan.FromMinutes(5));
            return Array.Empty<string>();
        }

        var rolePermissionQuery = await _rolePermissions.QueryAsync(tenantId, ct);
        var rolePermissions = await rolePermissionQuery
            .Where(x => x.TenantId == tenantId && enabledRoleIds.Contains(x.RoleId))
            .ToListAsync(ct);
        var permissionIds = rolePermissions.Select(x => x.PermissionId).Distinct().ToArray();
        if (permissionIds.Length == 0)
        {
            _cache.Set(cacheKey, Array.Empty<string>(), TimeSpan.FromMinutes(5));
            return Array.Empty<string>();
        }

        var permissionQuery = await _permissions.QueryAsync(tenantId, ct);
        var permissionCodes = await permissionQuery
            .Where(x => x.TenantId == tenantId &&
                        permissionIds.Contains(x.Id) &&
                        x.IsEnabled)
            .Select(x => new PermissionCodeProjection { Code = x.Code })
            .ToListAsync(ct);

        var result = permissionCodes
            .Select(x => NormalizeCode(x.Code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    private static string BuildPermissionCacheKey(long tenantId, long userId, long? storeId)
    {
        return $"rbac:permissions:{tenantId}:{userId}:{storeId.GetValueOrDefault()}";
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().ToLowerInvariant();
    }

    private sealed class PermissionCodeProjection
    {
        public string Code { get; init; } = string.Empty;
    }

    private sealed class RolePermissionIdProjection
    {
        public long PermissionId { get; init; }
    }
}
