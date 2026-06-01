using Atlas.Core.Enums;
using Atlas.Messaging.Abstractions;

namespace Atlas.Services.Tenant;

public sealed class TenantProvisioningRequest
{
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string? Address { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhoneNumber { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? Province { get; set; }
    public string City { get; set; } = string.Empty;
    public string? Category { get; set; }
    public TenantType TenantType { get; set; } = TenantType.Enterprise;
    public BusinessType BusinessType { get; set; } = BusinessType.Chain;
    public long DatabaseInstanceId { get; set; }
    public int OfficeCount { get; set; } = 1;
    public string? HeadquartersName { get; set; }
    public string? HeadquartersCode { get; set; }
}

public sealed class TenantProvisioningResult
{
    public long TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public long DatabaseInstanceId { get; set; }
    public long HeadquartersStoreId { get; set; }
    public string HeadquartersStoreCode { get; set; } = string.Empty;
    public string HeadquartersStoreName { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public bool EventPublished { get; set; }
}

public sealed class TenantProvisionedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventName => "tenant.provisioned";
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public long? TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public long HeadquartersStoreId { get; init; }
}
