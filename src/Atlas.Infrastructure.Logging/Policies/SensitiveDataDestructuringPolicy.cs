using Serilog.Core;
using Serilog.Events;
using System.Reflection;

namespace Atlas.Infrastructure.Logging.Policies
{
    /// <summary>
    /// 敏感数据脱敏策略 - 用于对象属性脱敏
    /// </summary>
    /// <remarks>
    /// 该策略在 Serilog 解构对象时运行，适合处理 {@Object} 这类结构化日志。
    /// 普通消息模板中的敏感片段应继续依赖调用方避免记录，或使用过滤器补充检测。
    /// </remarks>
    public class SensitiveDataDestructuringPolicy : IDestructuringPolicy
    {
        private readonly HashSet<string> _sensitiveFields;
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

            // 只处理自定义对象，避免拦截 string、DateTime、集合等 Serilog 已有的标准解构路径。
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
                            new ScalarValue(SensitiveDataMasker.MaskByField(propName, propValue))));
                    }
                    else if (propValue is string strValue)
                    {
                        properties.Add(new LogEventProperty(
                            propName,
                            new ScalarValue(SensitiveDataMasker.MaskText(strValue))));
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
                    // 属性 getter 可能包含业务逻辑或抛异常，日志脱敏不应影响主流程。
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
    }
}
