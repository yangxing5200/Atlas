using Atlas.Messaging.Abstractions;
using MassTransit;

namespace Atlas.Messaging.RabbitMQ;

public sealed class MassTransitDomainEventPublisher : IDomainEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitDomainEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
    }

    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        return PublishAsync((IDomainEvent)domainEvent, ct);
    }

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return _publishEndpoint.Publish(
            (object)domainEvent,
            Pipe.Execute<PublishContext>(context =>
            {
                context.MessageId = domainEvent.EventId;
                context.CorrelationId = domainEvent.EventId;
                context.Headers.Set("Atlas-EventName", domainEvent.EventName);
                context.Headers.Set("Atlas-OccurredAt", domainEvent.OccurredAt);

                if (domainEvent.TenantId.HasValue)
                    context.Headers.Set("Atlas-TenantId", domainEvent.TenantId.Value);
            }),
            ct);
    }
}

public sealed class MassTransitDomainEventTransport : IDomainEventTransport
{
    private readonly IBus _bus;

    public MassTransitDomainEventTransport(IBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return _bus.Publish(
            (object)domainEvent,
            Pipe.Execute<PublishContext>(context =>
            {
                context.MessageId = domainEvent.EventId;
                context.CorrelationId = domainEvent.EventId;
                context.Headers.Set("Atlas-EventName", domainEvent.EventName);
                context.Headers.Set("Atlas-OccurredAt", domainEvent.OccurredAt);

                if (domainEvent.TenantId.HasValue)
                    context.Headers.Set("Atlas-TenantId", domainEvent.TenantId.Value);
            }),
            ct);
    }
}
