namespace Atlas.Infrastructure.Caching.Invalidation;

/// <summary>
/// 失效消息
/// </summary>
public class InvalidationMessage
{
    /// <summary>
    /// 失效的键列表（支持通配符）
    /// </summary>
    public List<string> Keys { get; set; } = new();

    /// <summary>
    /// 失效的标签列表
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 失效类型
    /// </summary>
    public InvalidationType InvalidationType { get; set; } = InvalidationType.ExactKeys;

    /// <summary>
    /// 发送者标识
    /// </summary>
    public string SenderId { get; set; } = string.Empty;

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 失效原因（可选）
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// 失效类型枚举
/// </summary>
public enum InvalidationType
{
    /// <summary>
    /// 精确键失效
    /// </summary>
    ExactKeys,

    /// <summary>
    /// 模式匹配失效
    /// </summary>
    Pattern,

    /// <summary>
    /// 标签失效
    /// </summary>
    Tags
}
