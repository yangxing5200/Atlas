using Serilog.Core;
using Serilog.Events;

namespace Atlas.Infrastructure.Logging.Enrichers
{
    /// <summary>
    /// 关联 ID 增强器 - 为日志添加 CorrelationId
    /// 注意：实际的 CorrelationId 已经在 LogContextMiddleware 中通过 LogContext.PushProperty 设置
    /// 这个 Enricher 可以作为备用方案或用于非 HTTP 上下文场景
    /// </summary>
    public class CorrelationIdEnricher : ILogEventEnricher
    {
        private const string CorrelationIdPropertyName = "CorrelationId";

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // 如果已经有 CorrelationId，则不处理
            if (logEvent.Properties.ContainsKey(CorrelationIdPropertyName))
            {
                return;
            }

            // 尝试从 AsyncLocal 或其他上下文获取
            var correlationId = GetCorrelationId();

            if (!string.IsNullOrEmpty(correlationId))
            {
                var property = propertyFactory.CreateProperty(
                    CorrelationIdPropertyName,
                    correlationId);
                logEvent.AddPropertyIfAbsent(property);
            }
        }

        private string? GetCorrelationId()
        {
            // 这里可以从 AsyncLocal 或其他上下文存储中获取
            // 对于 HTTP 请求，LogContextMiddleware 已经设置
            // 对于后台任务等场景，可以在这里生成新的 ID
            return System.Diagnostics.Activity.Current?.Id;
        }
    }
}