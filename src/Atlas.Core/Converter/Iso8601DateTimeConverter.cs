using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Core.Converter
{
    /// <summary>
    /// DateTime 类型转 ISO 8601 格式的 JSON 转换器
    /// 确保所有 DateTime 序列化时统一使用 UTC 时间和 ISO 8601 格式
    /// </summary>
    public class Iso8601DateTimeConverter : JsonConverter<DateTime>
    {
        private const string Iso8601Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var dateString = reader.GetString();
                if (dateString != null && DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
                {
                    return result;
                }
            }

            throw new JsonException("Unable to convert to DateTime");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime().ToString(Iso8601Format, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Nullable DateTime 类型转 ISO 8601 格式的 JSON 转换器
    /// </summary>
    public class NullableIso8601DateTimeConverter : JsonConverter<DateTime?>
    {
        private const string Iso8601Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var dateString = reader.GetString();
                if (dateString != null && DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
                {
                    return result;
                }
            }

            throw new JsonException("Unable to convert to DateTime?");
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToUniversalTime().ToString(Iso8601Format, CultureInfo.InvariantCulture));
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
