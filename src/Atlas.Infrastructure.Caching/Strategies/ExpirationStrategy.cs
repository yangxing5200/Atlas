namespace Atlas.Infrastructure.Caching.Strategies;

/// <summary>
/// 过期策略
/// </summary>
public enum ExpirationType
{
    /// <summary>
    /// 滑动过期：每次访问重置过期时间
    /// </summary>
    Sliding,

    /// <summary>
    /// 绝对过期：固定时间点过期
    /// </summary>
    Absolute,

    /// <summary>
    /// 混合策略：滑动+绝对
    /// </summary>
    Hybrid
}

public class ExpirationStrategy
{
    public ExpirationType Type { get; set; }
    public TimeSpan SlidingExpiration { get; set; }
    public TimeSpan? AbsoluteExpiration { get; set; }
}