using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Providers.Redis
{
    /// <summary>
    /// 基于 Redis Pub/Sub 的分布式缓存失效总线
    /// </summary>
    public class RedisInvalidationBus : ICacheInvalidationBus
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisInvalidationBus>? _logger;
        private readonly List<Action<string>> _handlers = new();
        private const string ChannelName = "atlas.cache.invalidation";
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
        /// 发布缓存失效通知到所有订阅的服务器
        /// </summary>
        public async Task PublishInvalidationAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            try
            {
                var sub = _redis.GetSubscriber();
                var recipients = await sub.PublishAsync(ChannelName, key);

                _logger?.LogDebug(
                    "Published cache invalidation for key '{Key}' to {Recipients} subscribers",
                    key,
                    recipients);
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常，避免影响主流程
                _logger?.LogError(
                    ex,
                    "Failed to publish cache invalidation for key '{Key}'",
                    key);
            }
        }

        /// <summary>
        /// 订阅缓存失效通知
        /// </summary>
        public void Subscribe(Action<string> onInvalidate)
        {
            if (onInvalidate == null)
                throw new ArgumentNullException(nameof(onInvalidate));

            lock (_subscribeLock)
            {
                _handlers.Add(onInvalidate);

                // 只订阅一次 Redis 频道
                if (!_isSubscribed)
                {
                    try
                    {
                        var sub = _redis.GetSubscriber();
                        sub.Subscribe(ChannelName, (channel, message) =>
                        {
                            if (message.IsNullOrEmpty)
                                return;

                            var key = message.ToString();

                            _logger?.LogDebug(
                                "Received cache invalidation notification for key '{Key}'",
                                key);

                            // 调用所有已注册的处理器
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
        /// 批量发布缓存失效通知（性能优化）
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
                    tasks.Add(sub.PublishAsync(ChannelName, key));
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