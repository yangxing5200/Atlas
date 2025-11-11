using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Infrastructure.Caching.Abstractions;

namespace Atlas.Infrastructure.Caching.Serialization
{
    public class JsonCacheSerializer : ICacheSerializer
    {
        private readonly JsonSerializerOptions _options;

        public JsonCacheSerializer(JsonSerializerOptions? options = null)
        {
            _options = options ?? new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false,
                // 关键配置：处理循环引用
                ReferenceHandler = ReferenceHandler.Preserve,

                // 配置默认忽略策略
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

                // 处理只读属性
                IgnoreReadOnlyProperties = false,

                // 包含字段
                IncludeFields = true
            };
        }

        public string SerializerName => "Json";

        public byte[] Serialize<T>(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                var json = JsonSerializer.Serialize(value, _options);
                return Encoding.UTF8.GetBytes(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to serialize object of type {typeof(T).Name}. " +
                    $"Error: {ex.Message}", ex);
            }
        }

        public T? Deserialize<T>(byte[] data)
        {
            if (data == null || data.Length == 0)
                return default;

            try
            {
                var json = Encoding.UTF8.GetString(data);
                return JsonSerializer.Deserialize<T>(json, _options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize object of type {typeof(T).Name}. " +
                    $"Error: {ex.Message}", ex);
            }
        }
    }
}