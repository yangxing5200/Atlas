using System.Text.Json;
using Atlas.Messaging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Atlas.Messaging.Redis;

public sealed class RedisMessagingOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ChannelPrefix { get; set; } = "atlas:events";
}

public sealed class RedisDomainEventPublisher : IDomainEventPublisher
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisMessagingOptions _options;
    private readonly ILogger<RedisDomainEventPublisher> _logger;

    public RedisDomainEventPublisher(
        IConnectionMultiplexer redis,
        IOptions<RedisMessagingOptions> options,
        ILogger<RedisDomainEventPublisher> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ct.ThrowIfCancellationRequested();

        var envelope = new DomainEventEnvelope<TEvent>(domainEvent);
        var payload = JsonSerializer.Serialize(envelope, DomainEventJson.Options);
        var subscriber = _redis.GetSubscriber();

        var allChannel = $"{_options.ChannelPrefix}:all";
        var eventChannel = $"{_options.ChannelPrefix}:{domainEvent.EventName}";

        await subscriber.PublishAsync(RedisChannel.Literal(allChannel), payload);
        await subscriber.PublishAsync(RedisChannel.Literal(eventChannel), payload);

        _logger.LogInformation(
            "Published domain event {EventName} ({EventId}) to Redis",
            domainEvent.EventName,
            domainEvent.EventId);
    }
}

public static class RedisMessagingServiceCollectionExtensions
{
    public static IServiceCollection AddRedisDomainEvents(
        this IServiceCollection services,
        string connectionString,
        string? channelPrefix = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Redis connection string is required.", nameof(connectionString));

        services.Configure<RedisMessagingOptions>(options =>
        {
            options.ConnectionString = connectionString;
            if (!string.IsNullOrWhiteSpace(channelPrefix))
                options.ChannelPrefix = channelPrefix;
        });

        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.RemoveAll<IDomainEventPublisher>();
        services.AddSingleton<IDomainEventPublisher, RedisDomainEventPublisher>();

        return services;
    }
}
