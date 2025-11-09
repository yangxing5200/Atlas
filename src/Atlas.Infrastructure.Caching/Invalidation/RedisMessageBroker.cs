using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Atlas.Infrastructure.Caching.Invalidation;

/// <summary>
/// Redis Pub/Sub 消息代理
/// </summary>
public class RedisMessageBroker : IMessageBroker
{
    private const string Channel = "cache:invalidation";
    
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisMessageBroker> _logger;
    private readonly string _serverId;
    private ISubscriber? _subscriber;

    public RedisMessageBroker(
        IConnectionMultiplexer redis,
        ILogger<RedisMessageBroker> logger)
    {
        _redis = redis;
        _logger = logger;
        _serverId = Guid.NewGuid().ToString("N");
    }

    public async Task PublishAsync(InvalidationMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            message.SenderId = _serverId;
            var json = JsonSerializer.Serialize(message);
            
            _subscriber ??= _redis.GetSubscriber();
            await _subscriber.PublishAsync(Channel, json);
            
            _logger.LogDebug("Published invalidation message with {Count} keys", message.Keys.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish invalidation message");
        }
    }

    public async Task SubscribeAsync(Func<InvalidationMessage, Task> handler, CancellationToken cancellationToken = default)
    {
        try
        {
            _subscriber ??= _redis.GetSubscriber();
            
            await _subscriber.SubscribeAsync(Channel, async (channel, value) =>
            {
                try
                {
                    var message = JsonSerializer.Deserialize<InvalidationMessage>(value.ToString());
                    if (message == null)
                        return;

                    // 忽略自己发送的消息
                    if (message.SenderId == _serverId)
                        return;

                    await handler(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle invalidation message");
                }
            });

            _logger.LogInformation("Subscribed to invalidation channel");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to invalidation channel");
        }
    }

    public async Task UnsubscribeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_subscriber != null)
            {
                await _subscriber.UnsubscribeAsync(Channel);
                _logger.LogInformation("Unsubscribed from invalidation channel");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from invalidation channel");
        }
    }
}