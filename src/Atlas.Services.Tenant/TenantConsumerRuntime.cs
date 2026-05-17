using Atlas.Core.Entities.Tenant;
using Atlas.Data.Tenant.Context;
using Atlas.Messaging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Services.Tenant;

public sealed class TenantConsumerRuntime : ITenantConsumerRuntime
{
    private readonly ITenantDbContextFactory _dbContextFactory;
    private readonly ILogger<TenantConsumerRuntime> _logger;

    public TenantConsumerRuntime(
        ITenantDbContextFactory dbContextFactory,
        ILogger<TenantConsumerRuntime> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConsumeAsync<TEvent>(
        TEvent message,
        Guid messageId,
        string consumerName,
        Func<long, Guid, CancellationToken, Task> consume,
        CancellationToken ct)
        where TEvent : class, IDomainEvent
    {
        if (!message.TenantId.HasValue)
            throw new InvalidOperationException($"{typeof(TEvent).Name} must include tenant id.");

        var tenantId = message.TenantId.Value;
        var db = await _dbContextFactory.GetDbContextAsync(tenantId, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var alreadyConsumed = await db.Set<TenantInboxMessage>()
            .AnyAsync(
                x => x.TenantId == tenantId &&
                     x.MessageId == messageId &&
                     x.ConsumerName == consumerName,
                ct);

        if (alreadyConsumed)
        {
            _logger.LogInformation(
                "Skipped duplicate event {EventId} for consumer {ConsumerName}.",
                messageId,
                consumerName);
            return;
        }

        db.Set<TenantInboxMessage>().Add(new TenantInboxMessage
        {
            TenantId = tenantId,
            MessageId = messageId,
            ConsumerName = consumerName,
            ReceivedAtUtc = DateTime.UtcNow
        });

        await consume(tenantId, messageId, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
