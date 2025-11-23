using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace Atlas.Infrastructure.Logging.Enrichers
{
    /// <summary>
    /// 性能增强器 - 为日志添加性能相关信息
    /// </summary>
    public class PerformanceEnricher : ILogEventEnricher
    {
        private static readonly AsyncLocal<PerformanceContext> _context = new();

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var context = _context.Value;
            if (context != null)
            {
                // 添加操作持续时间
                if (context.Stopwatch.IsRunning)
                {
                    var elapsed = context.Stopwatch.ElapsedMilliseconds;
                    var property = propertyFactory.CreateProperty("ElapsedMs", elapsed);
                    logEvent.AddPropertyIfAbsent(property);

                    // 如果是慢操作，添加标记
                    if (elapsed > context.SlowThreshold)
                    {
                        var slowProperty = propertyFactory.CreateProperty("IsSlowOperation", true);
                        logEvent.AddPropertyIfAbsent(slowProperty);
                    }
                }

                // 添加操作名称
                if (!string.IsNullOrEmpty(context.OperationName))
                {
                    var opProperty = propertyFactory.CreateProperty("OperationName", context.OperationName);
                    logEvent.AddPropertyIfAbsent(opProperty);
                }
            }

            // 添加内存使用情况（可选）
            var gcMemory = GC.GetTotalMemory(false);
            var memoryProperty = propertyFactory.CreateProperty("GCMemoryBytes", gcMemory);
            logEvent.AddPropertyIfAbsent(memoryProperty);
        }

        /// <summary>
        /// 开始性能跟踪
        /// </summary>
        public static IDisposable BeginOperation(string operationName, int slowThresholdMs = 1000)
        {
            var context = new PerformanceContext
            {
                OperationName = operationName,
                SlowThreshold = slowThresholdMs,
                Stopwatch = Stopwatch.StartNew()
            };

            _context.Value = context;

            return new PerformanceScope(context);
        }

        private class PerformanceContext
        {
            public string OperationName { get; set; } = string.Empty;
            public int SlowThreshold { get; set; }
            public Stopwatch Stopwatch { get; set; } = new();
        }

        private class PerformanceScope : IDisposable
        {
            private readonly PerformanceContext _context;
            private bool _disposed;

            public PerformanceScope(PerformanceContext context)
            {
                _context = context;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _context.Stopwatch.Stop();
                    _disposed = true;
                }
            }
        }
    }
}