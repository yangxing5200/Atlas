using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Infrastructure.Caching.Serialization;

/// <summary>
/// JSON序列化器
/// </summary>
public class JsonCacheSerializer : ICacheSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonCacheSerializer()
    {
        _options = new JsonSerializerOptions
        {
            // 启用循环引用处理
            ReferenceHandler = ReferenceHandler.Preserve,

            // 其他常用配置
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public byte[] Serialize<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, _options);
        return Encoding.UTF8.GetBytes(json);
    }

    public T? Deserialize<T>(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<T>(json, _options);
    }
}