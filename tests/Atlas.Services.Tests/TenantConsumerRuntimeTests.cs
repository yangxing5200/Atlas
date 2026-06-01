using Atlas.Messaging.Abstractions;
using Atlas.Services.Tenant.Runtime.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Services.Tests;

public sealed class TenantConsumerRuntimeTests
{
    [Fact]
    public async Task ConsumeAsync_WhenMessageAlreadyConsumed_DoesNotInvokeHandler()
    {
        var inbox = new FakeInboxStore(consumed: false);
        var runtime = new TenantConsumerRuntime(
            inbox,
            NullLogger<TenantConsumerRuntime>.Instance);
        var invoked = false;

        await runtime.ConsumeAsync(
            new TestTenantEvent { TenantId = 10 },
            Guid.NewGuid(),
            "consumer",
            (_, _, _) =>
            {
                invoked = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.False(invoked);
        Assert.Equal(10, inbox.TenantId);
    }

    [Fact]
    public async Task ConsumeAsync_WhenMessageIsNew_InvokesHandlerInsideInboxStore()
    {
        var inbox = new FakeInboxStore(consumed: true);
        var runtime = new TenantConsumerRuntime(
            inbox,
            NullLogger<TenantConsumerRuntime>.Instance);
        var invoked = false;

        await runtime.ConsumeAsync(
            new TestTenantEvent { TenantId = 20 },
            Guid.NewGuid(),
            "consumer",
            (tenantId, _, _) =>
            {
                invoked = tenantId == 20;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(invoked);
        Assert.Equal(20, inbox.TenantId);
    }

    [Fact]
    public async Task ConsumeAsync_WithoutTenantId_ShouldThrow()
    {
        var runtime = new TenantConsumerRuntime(
            new FakeInboxStore(consumed: true),
            NullLogger<TenantConsumerRuntime>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runtime.ConsumeAsync(
                new TestTenantEvent(),
                Guid.NewGuid(),
                "consumer",
                (_, _, _) => Task.CompletedTask,
                CancellationToken.None));
    }

    private sealed class FakeInboxStore : ITenantInboxStore
    {
        private readonly bool _consumed;

        public FakeInboxStore(bool consumed)
        {
            _consumed = consumed;
        }

        public long TenantId { get; private set; }

        public async Task<bool> ExecuteOnceAsync(
            long tenantId,
            Guid messageId,
            string consumerName,
            Func<CancellationToken, Task> consume,
            CancellationToken ct = default)
        {
            TenantId = tenantId;
            if (!_consumed)
                return false;

            await consume(ct);
            return true;
        }

        public Task<int> DeleteReceivedBeforeAsync(
            long tenantId,
            DateTime cutoffUtc,
            int batchSize,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestTenantEvent : IDomainEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public string EventName => "test.tenant-event";
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
        public long? TenantId { get; init; }
    }
}
