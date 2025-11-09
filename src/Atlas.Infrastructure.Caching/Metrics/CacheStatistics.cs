namespace Atlas.Infrastructure.Caching.Metrics;

/// <summary>
/// 缓存统计信息
/// </summary>
public class CacheStatistics
{
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public double HitRate => TotalHits + TotalMisses > 0
        ? (double)TotalHits / (TotalHits + TotalMisses)
        : 0;

    public long TotalSets { get; set; }
    public long TotalErrors { get; set; }

    public double AverageGetLatencyMs { get; set; }
    public double AverageSetLatencyMs { get; set; }

    public Dictionary<string, long> KeyHits { get; set; } = new();
    public Dictionary<string, long> KeyMisses { get; set; } = new();
}