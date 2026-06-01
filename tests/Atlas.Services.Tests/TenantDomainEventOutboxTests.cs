using Atlas.Core.Services;
using Atlas.Core.Entities.Tenant;
using Atlas.Services.Tenant;
using Atlas.Services.Tenant.Runtime.Messaging;

namespace Atlas.Services.Tests;

public class TenantDomainEventOutboxTests
{
    [Fact]
    public async Task EnqueueAsync_WithMismatchedCurrentTenant_ShouldThrow()
    {
        var outbox = new TenantDomainEventOutbox(
            new ThrowingTenantOutboxStore(),
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

    private sealed class ThrowingTenantOutboxStore : ITenantOutboxStore
    {
        public Task AddAsync(TenantOutboxMessage message, long tenantId, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Outbox store should not be requested.");
        }

        public Task<List<TenantOutboxMessage>> ListDueAsync(
            long tenantId,
            DateTime now,
            DateTime staleProcessingBefore,
            int maxAttempts,
            int batchSize,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryClaimAsync(
            long tenantId,
            long messageId,
            string workerId,
            DateTime now,
            DateTime staleProcessingBefore,
            int maxAttempts,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task ReloadAsync(TenantOutboxMessage message, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task MarkProcessedAsync(TenantOutboxMessage message, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task MarkFailedAsync(
            TenantOutboxMessage message,
            string lastError,
            DateTime? nextAttemptAtUtc,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> DeleteProcessedBeforeAsync(
            long tenantId,
            DateTime cutoffUtc,
            int batchSize,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }
    }
}
