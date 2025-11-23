using Serilog.Core;
using Serilog.Events;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Logging.Policies
{
    /// <summary>
    /// 敏感数据脱敏策略 - 用于对象属性脱敏
    /// </summary>
    public class SensitiveDataDestructuringPolicy : IDestructuringPolicy
    {
        private readonly HashSet<string> _sensitiveFields;
        private readonly string _maskValue = "***REDACTED***";

        private static readonly Regex[] SensitivePatterns = new[]
        {
            new Regex(@"\b\d{15,19}\b", RegexOptions.Compiled), // 银行卡
            new Regex(@"\b\d{17}[\dXx]\b", RegexOptions.Compiled), // 身份证
            new Regex(@"\b1[3-9]\d{9}\b", RegexOptions.Compiled), // 手机号
            new Regex(@"password\s*[:=]\s*\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"token\s*[:=]\s*\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        public SensitiveDataDestructuringPolicy(IEnumerable<string> sensitiveFields)
        {
            _sensitiveFields = new HashSet<string>(
                sensitiveFields,
                StringComparer.OrdinalIgnoreCase);
        }

        public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory,
            out LogEventPropertyValue result)
        {
            if (value == null)
            {
                result = null!;
                return false;
            }

            var type = value.GetType();

            // 只处理自定义对象，不处理基础类型和系统类型
            if (type.IsPrimitive || type == typeof(string) || type.Namespace?.StartsWith("System") == true)
            {
                result = null!;
                return false;
            }

            var properties = new List<LogEventProperty>();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead)
                    continue;

                try
                {
                    var propValue = prop.GetValue(value);
                    var propName = prop.Name;

                    // 检查是否为敏感字段
                    if (IsSensitiveField(propName))
                    {
                        properties.Add(new LogEventProperty(
                            propName,
                            new ScalarValue(_maskValue)));
                    }
                    else if (propValue != null && propValue is string strValue && ContainsSensitivePattern(strValue))
                    {
                        // 检查字符串值是否包含敏感模式
                        properties.Add(new LogEventProperty(
                            propName,
                            new ScalarValue(_maskValue)));
                    }
                    else
                    {
                        properties.Add(new LogEventProperty(
                            propName,
                            propertyValueFactory.CreatePropertyValue(propValue, true)));
                    }
                }
                catch
                {
                    // 如果无法读取属性，跳过
                    continue;
                }
            }

            result = new StructureValue(properties);
            return true;
        }

        private bool IsSensitiveField(string fieldName)
        {
            return _sensitiveFields.Contains(fieldName);
        }

        private bool ContainsSensitivePattern(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 8)
                return false;

            return SensitivePatterns.Any(pattern => pattern.IsMatch(value));
        }
    }
}