using System.Text.Json;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Data.Global;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Sql;
using Atlas.Messaging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Services.Tenant;

public sealed class TenantOutboxDispatcherOptions
{
    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 5;
    public int TenantBatchSize { get; set; } = 100;
    public int MessageBatchSize { get; set; } = 50;
    public int MaxAttempts { get; set; } = 10;
    public int InitialRetryDelaySeconds { get; set; } = 10;
    public int MaxRetryDelaySeconds { get; set; } = 300;
    public int ProcessingTimeoutSeconds { get; set; } = 300;
}

public sealed class TenantOutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TenantOutboxDispatcher> _logger;
    private readonly TenantOutboxDispatcherOptions _options;
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public TenantOutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<TenantOutboxDispatcherOptions> options,
        ILogger<TenantOutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new TenantOutboxDispatcherOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Tenant outbox dispatcher is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var dispatched = await DispatchOnceAsync(stoppingToken);
                if (dispatched > 0)
                {
                    _logger.LogInformation("Dispatched {Count} tenant outbox messages.", dispatched);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tenant outbox dispatch cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)), stoppingToken);
        }
    }

    internal async Task<int> DispatchOnceAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var globalDbContext = scope.ServiceProvider.GetRequiredService<AtlasGlobalDbContext>();

        var dispatched = 0;
        var lastTenantId = 0L;
        var tenantBatchSize = Math.Max(1, _options.TenantBatchSize);

        while (!ct.IsCancellationRequested)
        {
            var tenantIds = await globalDbContext.Tenants
                .AsNoTracking()
                .Where(x =>
                    x.Id > lastTenantId &&
                    !x.IsDeleted &&
                    (x.Status == TenantStatus.Active || x.Status == TenantStatus.Trial))
                .OrderBy(x => x.Id)
                .Take(tenantBatchSize)
                .Select(x => x.Id)
                .ToListAsync(ct);

            if (tenantIds.Count == 0)
                break;

            foreach (var tenantId in tenantIds)
            {
                dispatched += await DispatchTenantAsync(scope.ServiceProvider, tenantId, ct);
                lastTenantId = tenantId;
            }
        }

        return dispatched;
    }

    private async Task<int> DispatchTenantAsync(
        IServiceProvider serviceProvider,
        long tenantId,
        CancellationToken ct)
    {
        var dbContextFactory = serviceProvider.GetRequiredService<ITenantDbContextFactory>();
        var sqlExecutor = serviceProvider.GetRequiredService<ITenantSqlExecutor>();
        var transport = serviceProvider.GetRequiredService<IDomainEventTransport>();
        var db = await dbContextFactory.GetDbContextAsync(tenantId, ct);

        var now = DateTime.UtcNow;
        var staleProcessingBefore = now.AddSeconds(-Math.Max(30, _options.ProcessingTimeoutSeconds));
        var maxAttempts = Math.Max(1, _options.MaxAttempts);

        var messages = await db.Set<TenantOutboxMessage>()
            .Where(x =>
                x.TenantId == tenantId &&
                x.ProcessedAtUtc == null &&
                x.AttemptCount < maxAttempts &&
                (x.AvailableAtUtc == null || x.AvailableAtUtc <= now) &&
                (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now) &&
                (x.ProcessingAtUtc == null || x.ProcessingAtUtc < staleProcessingBefore))
            .OrderBy(x => x.CreatedAt)
            .Take(Math.Max(1, _options.MessageBatchSize))
            .ToListAsync(ct);

        var dispatched = 0;
        foreach (var message in messages)
        {
            if (!await TryClaimAsync(sqlExecutor, db, tenantId, message.Id, now, staleProcessingBefore, maxAttempts, ct))
                continue;

            await db.Entry(message).ReloadAsync(ct);

            try
            {
                var domainEvent = Deserialize(message);
                if (domainEvent.TenantId != message.TenantId)
                    throw new InvalidOperationException(
                        $"Tenant outbox message {message.Id} has mismatched payload tenant id '{domainEvent.TenantId}'.");

                await transport.PublishAsync(domainEvent, ct);

                message.ProcessedAtUtc = DateTime.UtcNow;
                message.ProcessingAtUtc = null;
                message.ProcessingBy = null;
                message.LastError = null;
                await db.SaveChangesAsync(ct);
                dispatched++;
            }
            catch (Exception ex)
            {
                await MarkFailedAsync(db, message, ex, ct);
            }
        }

        return dispatched;
    }

    private async Task<bool> TryClaimAsync(
        ITenantSqlExecutor sqlExecutor,
        DbContext db,
        long tenantId,
        long messageId,
        DateTime now,
        DateTime staleProcessingBefore,
        int maxAttempts,
        CancellationToken ct)
    {
        var updated = await sqlExecutor.ExecuteTenantCommandAsync(
            db,
            tenantId,
            $"""
             UPDATE TenantOutboxMessages
              SET ProcessingAtUtc = {now}, ProcessingBy = {_workerId}, UpdatedAt = {now}
              WHERE Id = {messageId}
                AND TenantId = {tenantId}
                AND ProcessedAtUtc IS NULL
                AND AttemptCount < {maxAttempts}
               AND (AvailableAtUtc IS NULL OR AvailableAtUtc <= {now})
               AND (NextAttemptAtUtc IS NULL OR NextAttemptAtUtc <= {now})
               AND (ProcessingAtUtc IS NULL OR ProcessingAtUtc < {staleProcessingBefore})
             """,
            ct);

        return updated == 1;
    }

    private async Task MarkFailedAsync(
        DbContext db,
        TenantOutboxMessage message,
        Exception exception,
        CancellationToken ct)
    {
        message.AttemptCount++;
        message.ProcessingAtUtc = null;
        message.ProcessingBy = null;
        message.LastError = Truncate(exception.ToString(), 2000);

        if (message.AttemptCount < Math.Max(1, _options.MaxAttempts))
        {
            message.NextAttemptAtUtc = DateTime.UtcNow.Add(GetRetryDelay(message.AttemptCount));
        }

        await db.SaveChangesAsync(ct);

        _logger.LogError(
            exception,
            "Failed to dispatch tenant outbox message {MessageId} for tenant {TenantId}; attempt {AttemptCount}/{MaxAttempts}.",
            message.EventId,
            message.TenantId,
            message.AttemptCount,
            _options.MaxAttempts);
    }

    private TimeSpan GetRetryDelay(int attemptCount)
    {
        var initial = Math.Max(1, _options.InitialRetryDelaySeconds);
        var max = Math.Max(initial, _options.MaxRetryDelaySeconds);
        var seconds = Math.Min(max, initial * Math.Pow(2, Math.Max(0, attemptCount - 1)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static IDomainEvent Deserialize(TenantOutboxMessage message)
    {
        var messageType = Type.GetType(message.MessageType, throwOnError: false)
            ?? throw new InvalidOperationException($"Cannot resolve domain event type '{message.MessageType}'.");

        var value = JsonSerializer.Deserialize(message.Payload, messageType, DomainEventJson.Options);
        return value as IDomainEvent
            ?? throw new InvalidOperationException($"Message type '{message.MessageType}' does not implement IDomainEvent.");
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
