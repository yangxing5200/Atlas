using Atlas.Core.Services;
using Atlas.Data.Tenant.Context;
using Atlas.Services.Tenant;

namespace Atlas.Services.Tests;

public class TenantDomainEventOutboxTests
{
    [Fact]
    public async Task EnqueueAsync_WithMismatchedCurrentTenant_ShouldThrow()
    {
        var outbox = new TenantDomainEventOutbox(
            new ThrowingTenantDbContextFactory(),
            new FixedIdentity(tenantId: 100));

        var domainEvent = new OrderPlacedEvent
        {
            TenantId = 200,
            StoreId = 10,
            OrderId = 1,
            OrderNo = "ORD1",
            MemberId = 1,
            TotalAmount = 10m,
            ItemCount = 1
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => outbox.EnqueueAsync(domainEvent));
    }

    private sealed class FixedIdentity : ICurrentIdentity
    {
        public FixedIdentity(long? tenantId)
        {
            TenantId = tenantId;
        }

        public long? UserId => 1;
        public string UserName => "test";
        public long? StoreId => 10;
        public long? TenantId { get; }
        public string? SessionId => "session";
        public bool IsAuthenticated => true;
    }

    private sealed class ThrowingTenantDbContextFactory : ITenantDbContextFactory
    {
        public Task<AtlasTenantDbContext> GetDbContextAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("DbContext should not be requested.");
        }

        public Task<AtlasTenantDbContext> GetReadonlyDbContextAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("DbContext should not be requested.");
        }

        public Task<AtlasTenantDbContext> GetReportDbContextAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("DbContext should not be requested.");
        }

        public Task<AtlasTenantDbContext> GetDbContextAsync(long tenantId, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("DbContext should not be requested.");
        }

        public Task<AtlasTenantDbContext> GetReadonlyDbContextAsync(long tenantId, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("DbContext should not be requested.");
        }

        public Task<AtlasTenantDbContext> GetReportDbContextAsync(long tenantId, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("DbContext should not be requested.");
        }
    }
}
