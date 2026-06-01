using Serilog.Core;
using Serilog.Events;
using Atlas.Infrastructure.Logging.Policies;

namespace Atlas.Infrastructure.Logging.Filters
{
    /// <summary>
    /// 敏感数据过滤器
    /// </summary>
    /// <remarks>
    /// 过滤器在日志事件写出前运行，可标记或替换敏感属性；它不应阻止日志写出，否则会影响故障排查完整性。
    /// </remarks>
    public class SensitiveDataFilter : ILogEventFilter
    {
        private readonly HashSet<string> _sensitiveFields;
        public SensitiveDataFilter(IEnumerable<string> sensitiveFields)
        {
            _sensitiveFields = new HashSet<string>(
                sensitiveFields,
                StringComparer.OrdinalIgnoreCase);
        }

        public bool IsEnabled(LogEvent logEvent)
        {
            foreach (var property in logEvent.Properties.ToList())
            {
                var maskedValue = SensitiveDataMasker.MaskValue(property.Key, property.Value, _sensitiveFields);
                if (!ReferenceEquals(maskedValue, property.Value))
                {
                    logEvent.RemovePropertyIfPresent(property.Key);
                    logEvent.AddOrUpdateProperty(
                        new LogEventProperty(property.Key,
                        maskedValue));
                }
            }

            var message = logEvent.RenderMessage();
            var maskedMessage = SensitiveDataMasker.MaskText(message);
            if (!string.Equals(message, maskedMessage, StringComparison.Ordinal))
            {
                logEvent.AddOrUpdateProperty(
                    new LogEventProperty("_SensitiveDataDetected",
                    new ScalarValue(true)));
                logEvent.AddOrUpdateProperty(
                    new LogEventProperty("_SanitizedMessage",
                    new ScalarValue(maskedMessage)));
            }

            return true;
        }
    }
}
