using System.Diagnostics;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Telemetry;
using Atlas.Messaging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Atlas.Services.Tenant.Runtime.Messaging;

public sealed class TenantConsumerRuntime : ITenantConsumerRuntime
{
    private readonly ITenantInboxStore _inboxStore;
    private readonly ILogger<TenantConsumerRuntime> _logger;

    public TenantConsumerRuntime(
        ITenantInboxStore inboxStore,
        ILogger<TenantConsumerRuntime> logger)
    {
        _inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
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
        using var activity = AtlasTelemetry.ActivitySource.StartActivity(
            "atlas.tenant_event.consume",
            ActivityKind.Consumer);
        activity?.SetTag("atlas.tenant.id", tenantId);
        activity?.SetTag("messaging.message.id", messageId);
        activity?.SetTag("messaging.destination.name", consumerName);
        activity?.SetTag("atlas.event.name", message.EventName);

        var consumed = false;
        try
        {
            consumed = await _inboxStore.ExecuteOnceAsync(
                tenantId,
                messageId,
                consumerName,
                token => consume(tenantId, messageId, token),
                ct);
            activity?.SetTag("atlas.messaging.duplicate", !consumed);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }

        if (!consumed)
        {
            _logger.LogInformation(
                "Skipped duplicate event {EventId} for consumer {ConsumerName}.",
                messageId,
                consumerName);
        }
    }
}
