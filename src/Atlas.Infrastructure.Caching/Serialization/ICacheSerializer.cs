namespace Atlas.Infrastructure.Caching.Serialization;

/// <summary>
/// 缓存序列化器接口
/// </summary>
public interface ICacheSerializer
{
    byte[] Serialize<T>(T value);
    T? Deserialize<T>(byte[] data);
}