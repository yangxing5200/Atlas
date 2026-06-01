using Atlas.Core.Authorization;
using Atlas.Core.Entities.Base;
using Atlas.Core.Entities.Interfaces;
using Atlas.Core.Enums;

namespace Atlas.Core.Entities.Tenant;

public sealed class Role : BaseEntity, ITenantEntity, ISnowflakeId
{
    public long TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PermissionScope Scope { get; set; } = PermissionScope.Tenant;
    public long? StoreId { get; set; }
    public bool IsSystem { get; set; }
    public bool IsEnabled { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public sealed class Permission : BaseEntity, ITenantEntity, ISnowflakeId
{
    public long TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CapabilityCode { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public PermissionScope Scope { get; set; } = PermissionScope.Tenant;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool IsAssignable { get; set; } = true;
    public bool IsSystem { get; set; }
    public AtlasPermissionRiskLevel RiskLevel { get; set; } = AtlasPermissionRiskLevel.Low;
    public bool IsBuiltIn { get; set; } = true;
    public bool IsEnabled { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public sealed class RolePermission : BaseEntity, ITenantEntity, ISnowflakeId
{
    public long TenantId { get; set; }
    public long RoleId { get; set; }
    public long PermissionId { get; set; }
    public RolePermissionEffect Effect { get; set; } = RolePermissionEffect.Allow;
    public AtlasDataScopeType DataScopeType { get; set; } = AtlasDataScopeType.CurrentStore;
    public string? DataScopeJson { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public long? GrantedBy { get; set; }

    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}

public sealed class RefreshToken : BaseEntity, ITenantEntity, ISnowflakeId
{
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public long? StoreId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }
    public long? ReplacedByTokenId { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAtUtc == null && ExpiresAtUtc > utcNow;
}

public sealed class AuditEvent : BaseEntity, ITenantEntity, ISnowflakeId
{
    public long TenantId { get; set; }
    public long? UserId { get; set; }
    public long? StoreId { get; set; }
    public string? SessionId { get; set; }
    public string? TraceId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public AuditEventOutcome Outcome { get; set; } = AuditEventOutcome.Succeeded;
    public string? EntityType { get; set; }
    public long? EntityId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Metadata { get; set; }
    public string? ErrorMessage { get; set; }
}
