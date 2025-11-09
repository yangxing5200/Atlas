using MessagePack;

namespace Atlas.Infrastructure.Caching.Serialization;

/// <summary>
/// MessagePack序列化器（高性能）
/// </summary>
public class MessagePackCacheSerializer : ICacheSerializer
{
    private readonly MessagePackSerializerOptions _options;

    public MessagePackCacheSerializer()
    {
        _options = MessagePackSerializerOptions.Standard;
    }

    public byte[] Serialize<T>(T value)
    {
        return MessagePackSerializer.Serialize(value, _options);
    }

    public T? Deserialize<T>(byte[] data)
    {
        return MessagePackSerializer.Deserialize<T>(data, _options);
    }
}