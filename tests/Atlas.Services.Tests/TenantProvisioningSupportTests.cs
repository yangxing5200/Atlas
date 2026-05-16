using Atlas.Infrastructure.Common.Tenants;
using Atlas.Messaging.Abstractions;
using Atlas.Services.Tenant;

namespace Atlas.Services.Tests;

public class TenantProvisioningSupportTests
{
    [Fact]
    public void NormalizeDomain_ShouldCreateStableSlug()
    {
        var generator = new TenantCodeGenerator();

        var result = generator.NormalizeDomain("  Demo Brand / Shanghai  ");

        Assert.Equal("demo-brand-shanghai", result);
    }

    [Fact]
    public void GenerateStoreCode_ShouldUseTenantAndSuffix()
    {
        var generator = new TenantCodeGenerator();

        var result = generator.GenerateStoreCode("demo-brand-shanghai", "headquarters");

        Assert.Equal("DEMOBRANDSHA-HEADQUAR", result);
    }

    [Fact]
    public async Task NoOpPublisher_ShouldAcceptTenantProvisionedEvent()
    {
        IDomainEventPublisher publisher = new NoOpDomainEventPublisher();
        var domainEvent = new TenantProvisionedEvent
        {
            TenantId = 1001,
            TenantName = "Demo Tenant",
            Domain = "demo",
            HeadquartersStoreId = 2001
        };

        await publisher.PublishAsync(domainEvent);

        Assert.Equal("tenant.provisioned", domainEvent.EventName);
        Assert.Equal(1001, domainEvent.TenantId);
    }
}
