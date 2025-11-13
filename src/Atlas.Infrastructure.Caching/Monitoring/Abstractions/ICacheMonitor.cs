// Monitoring/Abstractions/ICacheMonitor.cs
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.Monitoring.Abstractions
{
    public interface ICacheMonitor
    {
        void RecordHit(string key);
        void RecordMiss(string key);
        void RecordSet(string key, long size);
        void RecordRemove(string key);
        void RecordInvalidation(string tag);
        Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
        Task ResetStatisticsAsync(CancellationToken cancellationToken = default);
    }
}