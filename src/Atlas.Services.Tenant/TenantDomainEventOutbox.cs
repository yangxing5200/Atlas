using System.Reflection;
using System.Text.Json;
using Atlas.Core.Entities.Tenant;
using Atlas.Data.Tenant.Context;
using Atlas.Messaging.Abstractions;

namespace Atlas.Services.Tenant;

public interface ITenantDomainEventOutbox
{
    Task EnqueueAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent;
}

public sealed class TenantDomainEventOutbox : ITenantDomainEventOutbox
{
    private readonly ITenantDbContextFactory _dbContextFactory;

    public TenantDomainEventOutbox(ITenantDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public async Task EnqueueAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!domainEvent.TenantId.HasValue)
            throw new InvalidOperationException("Tenant-scoped domain events must include tenant id.");

        var eventType = domainEvent.GetType();
        var messageType = eventType.AssemblyQualifiedName
            ?? throw new InvalidOperationException($"Cannot resolve message type for event '{eventType.FullName}'.");

        var outboxMessage = new TenantOutboxMessage
        {
            TenantId = domainEvent.TenantId.Value,
            StoreId = TryGetLongProperty(domainEvent, "StoreId"),
            EventId = domainEvent.EventId,
            EventName = domainEvent.EventName,
            MessageType = messageType,
            Payload = JsonSerializer.Serialize(domainEvent, eventType, DomainEventJson.Options),
            OccurredAtUtc = domainEvent.OccurredAt.UtcDateTime,
            AvailableAtUtc = DateTime.UtcNow
        };

        var db = await _dbContextFactory.GetDbContextAsync(ct);
        await db.Set<TenantOutboxMessage>().AddAsync(outboxMessage, ct);
    }

    private static long? TryGetLongProperty(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        var propertyValue = property?.GetValue(value);

        return propertyValue switch
        {
            long longValue => longValue,
            int intValue => intValue,
            _ => null
        };
    }
}
