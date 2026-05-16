using System.Text.Json;

namespace Atlas.Messaging.Abstractions;

public interface IDomainEvent
{
    Guid EventId { get; }
    string EventName { get; }
    DateTimeOffset OccurredAt { get; }
    long? TenantId { get; }
}

public sealed record DomainEventEnvelope<TEvent>
    where TEvent : IDomainEvent
{
    public DomainEventEnvelope(TEvent data)
    {
        EventId = data.EventId;
        EventName = data.EventName;
        TenantId = data.TenantId;
        OccurredAt = data.OccurredAt;
        Data = data;
    }

    public Guid EventId { get; init; }
    public string EventName { get; init; }
    public long? TenantId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public TEvent Data { get; init; }
}

public interface IDomainEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent;
}

public sealed class NoOpDomainEventPublisher : IDomainEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return Task.CompletedTask;
    }
}

public static class DomainEventJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
