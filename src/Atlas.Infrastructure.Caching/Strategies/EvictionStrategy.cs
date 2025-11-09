namespace Atlas.Infrastructure.Caching.Strategies;

/// <summary>
/// 驱逐策略
/// </summary>
public enum EvictionPolicy
{
    /// <summary>
    /// 最近最少使用
    /// </summary>
    LRU,

    /// <summary>
    /// 最不常用
    /// </summary>
    LFU,

    /// <summary>
    /// 先进先出
    /// </summary>
    FIFO
}