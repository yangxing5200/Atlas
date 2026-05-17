namespace Atlas.Messaging.RabbitMQ;

/// <summary>
/// RabbitMQ 消息总线配置。
/// </summary>
/// <remarks>
/// Uri 优先级高于 Host/Port/VirtualHost；PrefetchCount 和 Retry* 影响消费者吞吐和失败重试节奏。
/// </remarks>
public sealed class RabbitMqMessagingOptions
{
    public string Host { get; set; } = "localhost";
    public ushort Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string? Uri { get; set; }
    public ushort PrefetchCount { get; set; } = 16;
    public int RetryLimit { get; set; } = 5;
    public int RetryIntervalSeconds { get; set; } = 5;
}
