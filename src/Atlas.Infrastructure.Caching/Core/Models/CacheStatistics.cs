// Core/Models/CacheStatistics.cs
using System;

namespace Atlas.Infrastructure.Caching.Core.Models
{
    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public class CacheStatistics
    {
        public long TotalGets { get; set; }
        public long TotalSets { get; set; }
        public long TotalHits { get; set; }
        public long TotalMisses { get; set; }
        public long TotalInvalidations { get; set; }
        public double HitRate => TotalGets > 0 ? (double)TotalHits / TotalGets : 0;
        public long TotalKeys { get; set; }
        public long TotalTags { get; set; }
        public DateTime LastResetAt { get; set; } = DateTime.UtcNow;
    }
}