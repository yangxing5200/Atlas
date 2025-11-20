using Serilog.Core;
using Serilog.Events;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Logging.Filters
{
    /// <summary>
    /// 敏感数据过滤器
    /// </summary>
    public class SensitiveDataFilter : ILogEventFilter
    {
        private readonly HashSet<string> _sensitiveFields;
        private readonly string _maskValue = "***";

        public SensitiveDataFilter(IEnumerable<string> sensitiveFields)
        {
            _sensitiveFields = new HashSet<string>(
                sensitiveFields,
                StringComparer.OrdinalIgnoreCase);
        }

        public bool IsEnabled(LogEvent logEvent)
        {
            // 过滤敏感属性
            foreach (var property in logEvent.Properties.ToList())
            {
                if (IsSensitiveField(property.Key))
                {
                    logEvent.RemovePropertyIfPresent(property.Key);
                    logEvent.AddOrUpdateProperty(
                        new LogEventProperty(property.Key,
                        new ScalarValue(_maskValue)));
                }
                else if (property.Value is StructureValue structureValue)
                {
                    MaskSensitiveData(structureValue);
                }
            }

            // 过滤消息模板中的敏感信息
            var message = logEvent.RenderMessage();
            if (ContainsSensitivePattern(message))
            {
                // 记录警告但不阻止日志输出
                logEvent.AddOrUpdateProperty(
                    new LogEventProperty("_SensitiveDataDetected",
                    new ScalarValue(true)));
            }

            return true;
        }

        private bool IsSensitiveField(string fieldName)
        {
            return _sensitiveFields.Contains(fieldName);
        }

        private void MaskSensitiveData(StructureValue structure)
        {
            foreach (var property in structure.Properties)
            {
                if (IsSensitiveField(property.Name))
                {
                    // 替换敏感值
                    var maskedProperty = new LogEventProperty(
                        property.Name,
                        new ScalarValue(_maskValue));
                }
            }
        }
        private static readonly Regex[] _cachedPatterns = new[]
        {
            new Regex(@"\b\d{15,19}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\b\d{17}[\dXx]\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\b1[3-9]\d{9}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"password\s*[:=]\s*\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"token\s*[:=]\s*\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };
        private bool ContainsSensitivePattern(string message)
        {
            if (string.IsNullOrEmpty(message) || message.Length < 8) return false;
            return _cachedPatterns.Any(pattern => pattern.IsMatch(message));
        }
    }
}