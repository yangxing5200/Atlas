// Monitoring/CacheMonitor.cs
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Monitoring.Abstractions;

namespace Atlas.Infrastructure.Caching.Monitoring
{
    public class CacheMonitor : ICacheMonitor
    {
        private long _totalHits;
        private long _totalMisses;
        private long _totalSets;
        private long _totalRemoves;
        private long _totalInvalidations;
        private readonly ConcurrentDictionary<string, long> _keySizes = new();
        private DateTime _lastResetAt = DateTime.UtcNow;

        public void RecordHit(string key)
        {
            Interlocked.Increment(ref _totalHits);
        }

        public void RecordMiss(string key)
        {
            Interlocked.Increment(ref _totalMisses);
        }

        public void RecordSet(string key, long size)
        {
            Interlocked.Increment(ref _totalSets);
            _keySizes.AddOrUpdate(key, size, (_, __) => size);
        }

        public void RecordRemove(string key)
        {
            Interlocked.Increment(ref _totalRemoves);
            _keySizes.TryRemove(key, out _);
        }

        public void RecordInvalidation(string tag)
        {
            Interlocked.Increment(ref _totalInvalidations);
        }

        public Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var stats = new CacheStatistics
            {
                TotalHits = _totalHits,
                TotalMisses = _totalMisses,
                TotalGets = _totalHits + _totalMisses,
                TotalSets = _totalSets,
                TotalInvalidations = _totalInvalidations,
                TotalKeys = _keySizes.Count,
                LastResetAt = _lastResetAt
            };

            return Task.FromResult(stats);
        }

        public Task ResetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Exchange(ref _totalHits, 0);
            Interlocked.Exchange(ref _totalMisses, 0);
            Interlocked.Exchange(ref _totalSets, 0);
            Interlocked.Exchange(ref _totalRemoves, 0);
            Interlocked.Exchange(ref _totalInvalidations, 0);
            _keySizes.Clear();
            _lastResetAt = DateTime.UtcNow;

            return Task.CompletedTask;
        }
    }
}