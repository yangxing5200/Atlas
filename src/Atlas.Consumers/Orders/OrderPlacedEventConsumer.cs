using System.Text.Json;
using Atlas.Consumers;
using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Messaging.Abstractions;
using Atlas.Services.Tenant;
using Atlas.Services.Tenant.Runtime.Messaging;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Atlas.Consumers.Orders;

public sealed class OrderPlacedEventConsumer : TenantConsumerBase<OrderPlacedEvent>
{
    private readonly IRepository<OperationLog> _operationLogs;
    private readonly ILogger<OrderPlacedEventConsumer> _logger;

    public OrderPlacedEventConsumer(
        ITenantConsumerRuntime runtime,
        IRepository<OperationLog> operationLogs,
        ILogger<OrderPlacedEventConsumer> logger)
        : base(runtime)
    {
        _operationLogs = operationLogs ?? throw new ArgumentNullException(nameof(operationLogs));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override string ConsumerName => nameof(OrderPlacedEventConsumer);

    protected override async Task ConsumeTenantMessageAsync(
        ConsumeContext<OrderPlacedEvent> context,
        long tenantId,
        Guid messageId,
        CancellationToken ct)
    {
        var message = context.Message;
        var operationLog = new OperationLog
        {
            TenantId = tenantId,
            StoreId = message.StoreId,
            Module = "Order",
            OperationType = "Placed",
            Description = $"Order {message.OrderNo} was placed.",
            EntityId = message.OrderId,
            Changes = JsonSerializer.Serialize(message, DomainEventJson.Options),
            IsSuccess = true
        };

        await _operationLogs.AddAsync(operationLog, tenantId, ct);

        _logger.LogInformation(
            "Consumed order placed event {EventId}: tenant={TenantId}, store={StoreId}, order={OrderId}",
            message.EventId,
            tenantId,
            message.StoreId,
            message.OrderId);
    }
}
