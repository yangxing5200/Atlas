using Atlas.Core.Authorization;
using Atlas.Core.Entities.Base;

namespace Atlas.Core.Entities.Global;

public sealed class Capability : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string SourceModule { get; set; } = string.Empty;
}

public sealed class FeaturePackage : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AtlasPackageType Type { get; set; } = AtlasPackageType.Edition;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string SourceModule { get; set; } = string.Empty;
}

public sealed class PackageCapability : AuditableEntity
{
    public string PackageCode { get; set; } = string.Empty;
    public string CapabilityCode { get; set; } = string.Empty;
    public string? LimitJson { get; set; }
    public string? OptionJson { get; set; }
    public string SourceModule { get; set; } = string.Empty;
}

public sealed class TenantEntitlement : AuditableEntity
{
    public long TenantId { get; set; }
    public AtlasEntitlementSubjectType SubjectType { get; set; } = AtlasEntitlementSubjectType.Tenant;
    public long SubjectId { get; set; }
    public string? PackageCode { get; set; }
    public string? CapabilityCode { get; set; }
    public AtlasEntitlementSource Source { get; set; } = AtlasEntitlementSource.Manual;
    public DateTime StartAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndAtUtc { get; set; }
    public AtlasEntitlementStatus Status { get; set; } = AtlasEntitlementStatus.Active;
    public string? OptionOverrideJson { get; set; }

    public bool IsActive(DateTime utcNow)
    {
        return Status == AtlasEntitlementStatus.Active &&
               StartAtUtc <= utcNow &&
               (!EndAtUtc.HasValue || EndAtUtc.Value > utcNow);
    }
}

public sealed class MenuItem : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string? ParentCode { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public string? VisibleWhenJson { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string SourceModule { get; set; } = string.Empty;
}
