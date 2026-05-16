using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Providers.Redis
{
    /// <summary>
    /// Redis Pub/Sub based distributed cache invalidation bus.
    /// </summary>
    public class RedisInvalidationBus : ICacheInvalidationBus
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisInvalidationBus>? _logger;
        private readonly List<Action<string>> _handlers = new();
        private const string ChannelName = "atlas.cache.invalidation";
        private static readonly RedisChannel Channel = RedisChannel.Literal(ChannelName);
        private bool _isSubscribed = false;
        private readonly object _subscribeLock = new();

        public RedisInvalidationBus(
            IConnectionMultiplexer redis,
            ILogger<RedisInvalidationBus>? logger = null)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger;
        }

        /// <summary>
        /// Publishes a cache invalidation notification to all subscribed servers.
        /// </summary>
        public async Task PublishInvalidationAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            try
            {
                var sub = _redis.GetSubscriber();
                var recipients = await sub.PublishAsync(Channel, key);

                _logger?.LogDebug(
                    "Published cache invalidation for key '{Key}' to {Recipients} subscribers",
                    key,
                    recipients);
            }
            catch (Exception ex)
            {
                // Log failures without breaking the caller's main flow.
                _logger?.LogError(
                    ex,
                    "Failed to publish cache invalidation for key '{Key}'",
                    key);
            }
        }

        /// <summary>
        /// Subscribes to cache invalidation notifications.
        /// </summary>
        public void Subscribe(Action<string> onInvalidate)
        {
            if (onInvalidate == null)
                throw new ArgumentNullException(nameof(onInvalidate));

            lock (_subscribeLock)
            {
                _handlers.Add(onInvalidate);

                // Subscribe to the Redis channel only once.
                if (!_isSubscribed)
                {
                    try
                    {
                        var sub = _redis.GetSubscriber();
                        sub.Subscribe(Channel, (channel, message) =>
                        {
                            if (message.IsNullOrEmpty)
                                return;

                            var key = message.ToString();

                            _logger?.LogDebug(
                                "Received cache invalidation notification for key '{Key}'",
                                key);

                            // Invoke all registered handlers.
                            foreach (var handler in _handlers)
                            {
                                try
                                {
                                    handler(key);
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogError(
                                        ex,
                                        "Error executing cache invalidation handler for key '{Key}'",
                                        key);
                                }
                            }
                        });

                        _isSubscribed = true;
                        _logger?.LogInformation(
                            "Subscribed to cache invalidation channel '{Channel}'",
                            ChannelName);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(
                            ex,
                            "Failed to subscribe to cache invalidation channel '{Channel}'",
                            ChannelName);
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Publishes multiple cache invalidation notifications.
        /// </summary>
        public async Task PublishInvalidationsAsync(IEnumerable<string> keys)
        {
            if (keys == null)
                return;

            var keyList = new List<string>();
            foreach (var key in keys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                    keyList.Add(key);
            }

            if (keyList.Count == 0)
                return;

            try
            {
                var sub = _redis.GetSubscriber();
                var tasks = new List<Task>();

                foreach (var key in keyList)
                {
                    tasks.Add(sub.PublishAsync(Channel, key));
                }

                await Task.WhenAll(tasks);

                _logger?.LogDebug(
                    "Published {Count} cache invalidation notifications",
                    keyList.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Failed to publish batch cache invalidations");
            }
        }
    }
}
