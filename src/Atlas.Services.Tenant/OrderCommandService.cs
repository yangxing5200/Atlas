using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Microsoft.Extensions.Logging;

namespace Atlas.Services.Tenant;

public sealed class OrderCommandService : IOrderCommandService
{
    private readonly IRepository<Order> _orders;
    private readonly IUnitOfWork _tenantUnitOfWork;
    private readonly IIdGenerator _idGenerator;
    private readonly ICurrentIdentity _currentIdentity;
    private readonly ITenantDomainEventOutbox _tenantOutbox;
    private readonly ILogger<OrderCommandService> _logger;

    public OrderCommandService(
        IRepository<Order> orders,
        IUnitOfWork tenantUnitOfWork,
        IIdGenerator idGenerator,
        ICurrentIdentity currentIdentity,
        ITenantDomainEventOutbox tenantOutbox,
        ILogger<OrderCommandService> logger)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _tenantUnitOfWork = tenantUnitOfWork ?? throw new ArgumentNullException(nameof(tenantUnitOfWork));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
        _tenantOutbox = tenantOutbox ?? throw new ArgumentNullException(nameof(tenantOutbox));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PlaceOrderResult> PlaceAsync(
        PlaceOrderRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = _currentIdentity.TenantId
            ?? throw new InvalidOperationException("Tenant context is required to place an order.");
        var storeId = _currentIdentity.StoreId
            ?? throw new InvalidOperationException("Store context is required to place an order.");

        ValidateRequest(request);

        var orderId = _idGenerator.NextId();
        var order = new Order
        {
            Id = orderId,
            OrderNo = NormalizeOrderNo(request.OrderNo, orderId),
            MemberId = request.MemberId,
            TotalAmount = request.TotalAmount,
            Status = OrderStatus.Pending
        };

        var integrationEvent = new OrderPlacedEvent
        {
            TenantId = tenantId,
            StoreId = storeId,
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            MemberId = order.MemberId,
            TotalAmount = order.TotalAmount,
            ItemCount = request.ItemCount
        };

        await _tenantUnitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _orders.AddAsync(order, ct);
            await _tenantOutbox.EnqueueAsync(integrationEvent, ct);
            await _tenantUnitOfWork.SaveChangesAsync(ct);
            await _tenantUnitOfWork.CommitAsync(ct);
        }
        catch
        {
            if (_tenantUnitOfWork.HasActiveTransaction)
                await _tenantUnitOfWork.RollbackAsync(ct);
            throw;
        }

        _logger.LogInformation(
            "Placed order {OrderId} ({OrderNo}) for tenant {TenantId}, store {StoreId}; queued event {EventId}",
            order.Id,
            order.OrderNo,
            tenantId,
            storeId,
            integrationEvent.EventId);

        return new PlaceOrderResult
        {
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            TenantId = tenantId,
            StoreId = storeId,
            MemberId = order.MemberId,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            EventId = integrationEvent.EventId,
            EventQueued = true,
            EventPublished = false
        };
    }

    private static void ValidateRequest(PlaceOrderRequest request)
    {
        if (request.MemberId <= 0)
            throw new ArgumentException("Member id must be greater than zero.", nameof(request));

        if (request.TotalAmount <= 0)
            throw new ArgumentException("Total amount must be greater than zero.", nameof(request));

        if (request.ItemCount <= 0)
            throw new ArgumentException("Item count must be greater than zero.", nameof(request));

        if (request.OrderNo?.Trim().Length > 50)
            throw new ArgumentException("Order number cannot exceed 50 characters.", nameof(request));
    }

    private static string NormalizeOrderNo(string? orderNo, long orderId)
    {
        if (!string.IsNullOrWhiteSpace(orderNo))
            return orderNo.Trim();

        return $"ORD{orderId}";
    }
}
