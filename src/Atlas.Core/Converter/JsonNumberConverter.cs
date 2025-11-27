using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace Atlas.Core.Converter
{

    /// <summary>
    /// Long 类型转 String 的 JSON 转换器（解决雪花算法ID精度丢失问题）
    /// </summary>
    public class JsonNumberConverter : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // 支持从字符串或数字读取
            if (reader.TokenType == JsonTokenType.String)
            {
                if (long.TryParse(reader.GetString(), out long value))
                {
                    return value;
                }
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetInt64();
            }

            throw new JsonException($"Unable to convert to long");
        }

        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        {
            // 写入时转为字符串
            writer.WriteStringValue(value.ToString());
        }
    }
}
