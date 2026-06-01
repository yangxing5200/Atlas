using Atlas.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Security
{
    /// <summary>
    /// Token数据结构（仅存储最小必要信息）
    /// </summary>
    public class TokenInfo : ICurrentIdentity
    {
        private const char DELIMITER = '\x1F';

        public long? UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public long? StoreId { get; set; }
        public long? TenantId { get; set; }
        public long ExpiresAt { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public int TokenVersion { get; set; } = 1;

        public bool IsExpired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() > ExpiresAt;

        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// 获取过期时间的DateTime格式
        /// </summary>
        public DateTime GetExpiryDateTime() => DateTimeOffset.FromUnixTimeSeconds(ExpiresAt).UtcDateTime;
        public override string ToString()
        {
            var sb = new StringBuilder(100);
            sb.Append(UserId);
            sb.Append(DELIMITER);
            sb.Append(UserName);
            sb.Append(DELIMITER);
            sb.Append(StoreId);
            sb.Append(DELIMITER);
            sb.Append(TenantId);
            sb.Append(DELIMITER);
            sb.Append(ExpiresAt);
            sb.Append(DELIMITER);
            sb.Append(SessionId);
            sb.Append(DELIMITER);
            sb.Append(TokenVersion);
            return sb.ToString();
        }

        public static TokenInfo? Parse(ReadOnlySpan<char> data)
        {
            if (data.IsEmpty) return null;

            try
            {
                var fields = new string[7];
                var fieldIndex = 0;
                var startIndex = 0;

                for (int i = 0; i <= data.Length; i++)
                {
                    if (i == data.Length || data[i] == DELIMITER)
                    {
                        if (fieldIndex >= 7) break;
                        fields[fieldIndex++] = data.Slice(startIndex, i - startIndex).ToString();
                        startIndex = i + 1;
                    }
                }

                if (fieldIndex < 5) return null;  // ✅ 最少5个字段即可（向后兼容）

                return new TokenInfo
                {
                    UserId = long.Parse(fields[0]),
                    UserName = fields[1] ?? string.Empty,
                    StoreId = long.Parse(fields[2]),
                    TenantId = long.Parse(fields[3]),
                    ExpiresAt = long.Parse(fields[4]),
                    SessionId = fieldIndex >= 6 && !string.IsNullOrEmpty(fields[5])
                        ? fields[5]
                        : GenerateSessionId(),
                    TokenVersion = fieldIndex >= 7 ? int.Parse(fields[6]) : 1
                };
            }
            catch
            {
                return null;
            }
        }

        public static TokenInfo Create(
            ICurrentIdentity user,
            int expirationMinutes = 60,
            int tokenVersion = 1)
        {
            return new TokenInfo
            {
                UserId = user.UserId ?? 0,
                UserName = user.UserName,
                StoreId = user.StoreId ?? 0,
                TenantId = user.TenantId ?? 0,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes).ToUnixTimeSeconds(),
                SessionId = GenerateSessionId(),
                TokenVersion = tokenVersion
            };
        }

        /// <summary>
        /// 生成短SessionId（16字符）
        /// </summary>
        private static string GenerateSessionId()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var random = Random.Shared.NextInt64();
            var machineId = Environment.MachineName.GetHashCode();

            var bytes = new byte[16];
            BitConverter.GetBytes(timestamp).CopyTo(bytes, 0);
            BitConverter.GetBytes(random).CopyTo(bytes, 8);
            BitConverter.GetBytes(machineId).CopyTo(bytes, 12);

            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=')
                .Substring(0, 16);
        }
    }
}
