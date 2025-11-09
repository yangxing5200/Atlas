namespace Atlas.Infrastructure.Caching.Core;

/// <summary>
/// 缓存全局配置
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Redis连接字符串
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// 默认过期时间（秒）
    /// </summary>
    public int DefaultExpirationSeconds { get; set; } = 3600;

    /// <summary>
    /// L1缓存大小限制（MB）
    /// </summary>
    public int L1CacheSizeLimitMB { get; set; } = 100;

    /// <summary>
    /// 是否启用分布式失效
    /// </summary>
    public bool EnableDistributedInvalidation { get; set; } = true;

    /// <summary>
    /// 序列化器类型（Json/MessagePack）
    /// </summary>
    public string SerializerType { get; set; } = "Json";

    /// <summary>
    /// 是否启用压缩（对象>10KB自动压缩）
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// 压缩阈值（字节）
    /// </summary>
    public int CompressionThresholdBytes { get; set; } = 10240;

    /// <summary>
    /// 最大随机过期时间偏移（秒）
    /// </summary>
    public int MaxRandomOffsetSeconds { get; set; } = 300;

    /// <summary>
    /// 是否启用指标收集
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// 操作超时时间（毫秒）
    /// </summary>
    public int OperationTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 熔断器错误率阈值
    /// </summary>
    public double CircuitBreakerErrorThreshold { get; set; } = 0.5;

    /// <summary>
    /// 键前缀
    /// </summary>
    public string KeyPrefix { get; set; } = "Atlas";
}