using System.Reflection;
using System.Text.Json;
using Atlas.Core.Services;
using Atlas.Core.Entities.Tenant;
using Atlas.Messaging.Abstractions;
using Atlas.Services.Tenant;

namespace Atlas.Services.Tenant.Runtime.Messaging;

public sealed class TenantDomainEventOutbox : ITenantDomainEventOutbox
{
    private readonly ITenantOutboxStore _outboxStore;
    private readonly ICurrentIdentity _currentIdentity;

    public TenantDomainEventOutbox(
        ITenantOutboxStore outboxStore,
        ICurrentIdentity currentIdentity)
    {
        _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
    }

    public async Task EnqueueAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!domainEvent.TenantId.HasValue)
            throw new InvalidOperationException("Tenant-scoped domain events must include tenant id.");

        var eventTenantId = domainEvent.TenantId.Value;
        var currentTenantId = _currentIdentity.TenantId;
        if (currentTenantId.HasValue && currentTenantId.Value != eventTenantId)
        {
            throw new InvalidOperationException(
                $"Domain event tenant id '{eventTenantId}' does not match current tenant id '{currentTenantId.Value}'.");
        }

        var eventType = domainEvent.GetType();
        var messageType = eventType.AssemblyQualifiedName
            ?? throw new InvalidOperationException($"Cannot resolve message type for event '{eventType.FullName}'.");

        var outboxMessage = new TenantOutboxMessage
        {
            TenantId = eventTenantId,
            StoreId = TryGetLongProperty(domainEvent, "StoreId"),
            EventId = domainEvent.EventId,
            EventName = domainEvent.EventName,
            MessageType = messageType,
            Payload = JsonSerializer.Serialize(domainEvent, eventType, DomainEventJson.Options),
            OccurredAtUtc = domainEvent.OccurredAt.UtcDateTime,
            AvailableAtUtc = DateTime.UtcNow
        };

        await _outboxStore.AddAsync(outboxMessage, eventTenantId, ct);
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
