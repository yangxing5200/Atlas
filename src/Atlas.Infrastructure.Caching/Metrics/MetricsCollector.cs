using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Metrics;

/// <summary>
/// 指标收集器
/// </summary>
public class MetricsCollector
{
    private readonly ILogger<MetricsCollector> _logger;
    private readonly ConcurrentDictionary<string, long> _hits = new();
    private readonly ConcurrentDictionary<string, long> _misses = new();
    private readonly ConcurrentDictionary<string, long> _errors = new();
    
    private long _totalHits;
    private long _totalMisses;
    private long _totalSets;
    private long _totalErrors;
    
    private readonly List<double> _getLatencies = new();
    private readonly List<double> _setLatencies = new();
    private readonly object _latencyLock = new();

    public MetricsCollector(ILogger<MetricsCollector> logger)
    {
        _logger = logger;
    }

    public void RecordGet(string key, bool hit, long latencyMs)
    {
        if (hit)
        {
            Interlocked.Increment(ref _totalHits);
            _hits.AddOrUpdate(key, 1, (_, count) => count + 1);
        }
        else
        {
            Interlocked.Increment(ref _totalMisses);
            _misses.AddOrUpdate(key, 1, (_, count) => count + 1);
        }

        lock (_latencyLock)
        {
            _getLatencies.Add(latencyMs);
            if (_getLatencies.Count > 1000)
                _getLatencies.RemoveAt(0);
        }
    }

    public void RecordSet(string key, long latencyMs)
    {
        Interlocked.Increment(ref _totalSets);

        lock (_latencyLock)
        {
            _setLatencies.Add(latencyMs);
            if (_setLatencies.Count > 1000)
                _setLatencies.RemoveAt(0);
        }
    }

    public void RecordBulkGet(string keyName, int count, long latencyMs)
    {
        _logger.LogDebug("Bulk get for {KeyName}: {Count} items in {Latency}ms",
            keyName, count, latencyMs);
    }

    public void RecordError(string key, string operation)
    {
        Interlocked.Increment(ref _totalErrors);
        _errors.AddOrUpdate(key, 1, (_, count) => count + 1);
        
        _logger.LogWarning("Cache error for {Key} during {Operation}", key, operation);
    }

    public Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new CacheStatistics
        {
            TotalHits = _totalHits,
            TotalMisses = _totalMisses,
            TotalSets = _totalSets,
            TotalErrors = _totalErrors,
            KeyHits = new Dictionary<string, long>(_hits),
            KeyMisses = new Dictionary<string, long>(_misses)
        };

        lock (_latencyLock)
        {
            stats.AverageGetLatencyMs = _getLatencies.Count > 0
                ? _getLatencies.Average()
                : 0;

            stats.AverageSetLatencyMs = _setLatencies.Count > 0
                ? _setLatencies.Average()
                : 0;
        }

        return Task.FromResult(stats);
    }
}