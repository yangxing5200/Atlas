using Atlas.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Security
{
    public class TokenInfo
    {
        // 使用不可见字符作为分隔符，避免与用户数据冲突
        private const char DELIMITER = '\x1F'; // ASCII Unit Separator (31)

        // 字段顺序固定，用于Parse时按索引访问（性能优化）
        private const int FIELD_COUNT = 6;
        private const int IDX_USERID = 0;
        private const int IDX_USERNAME = 1;
        private const int IDX_STOREID = 2;
        private const int IDX_TENANTID = 3;
        private const int IDX_EXPIRESAT = 4;
        private const int IDX_EXTRA = 5;
        public long UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public long StoreId { get; set; }
        public long TenantId { get; set; }
        public long ExpiresAt { get; set; } // Unix时间戳
        public string? Extra { get; set; } // 额外信息（可选）

        /// <summary>
        /// 检查Token是否已过期（缓存当前时间戳避免重复计算）
        /// </summary>
        public bool IsExpired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() > ExpiresAt;

        /// <summary>
        /// 获取过期时间的DateTime格式
        /// </summary>
        public DateTime GetExpiryDateTime() => DateTimeOffset.FromUnixTimeSeconds(ExpiresAt).UtcDateTime;

        /// <summary>
        /// 高性能序列化为字符串
        /// </summary>
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

            if (!string.IsNullOrEmpty(Extra))
            {
                sb.Append(DELIMITER);
                sb.Append(Extra);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 高性能反序列化（避免异常处理开销）
        /// </summary>
        public static TokenInfo? Parse(ReadOnlySpan<char> data)
        {
            if (data.IsEmpty)
                return null;

            try
            {
                // 使用Span切割避免创建多个字符串对象
                var tokenInfo = new TokenInfo();
                var fieldIndex = 0;
                var startIndex = 0;

                for (int i = 0; i <= data.Length; i++)
                {
                    // 到达末尾或找到分隔符
                    if (i == data.Length || data[i] == DELIMITER)
                    {
                        var fieldValue = data.Slice(startIndex, i - startIndex);

                        switch (fieldIndex)
                        {
                            case IDX_USERID:
                                if (!long.TryParse(fieldValue, out var userId))
                                    return null;
                                tokenInfo.UserId = userId;
                                break;

                            case IDX_USERNAME:
                                tokenInfo.UserName = fieldValue.ToString();
                                break;

                            case IDX_STOREID:
                                if (!long.TryParse(fieldValue, out var storeId))
                                    return null;
                                tokenInfo.StoreId = storeId;
                                break;

                            case IDX_TENANTID:
                                if (!long.TryParse(fieldValue, out var tenantId))
                                    return null;
                                tokenInfo.TenantId = tenantId;
                                break;

                            case IDX_EXPIRESAT:
                                if (!long.TryParse(fieldValue, out var expiresAt))
                                    return null;
                                tokenInfo.ExpiresAt = expiresAt;
                                break;

                            case IDX_EXTRA:
                                tokenInfo.Extra = fieldValue.ToString();
                                break;
                        }

                        fieldIndex++;
                        startIndex = i + 1;

                        // 如果已经读取所有必需字段，可以提前返回
                        if (fieldIndex >= FIELD_COUNT && i < data.Length - 1)
                            break;
                    }
                }

                // 验证是否有足够的必需字段
                return fieldIndex >= 5 ? tokenInfo : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 工厂方法：创建新的IdentityInfo
        /// </summary>
        public static TokenInfo Create(ICurrentIdentity user, int expirationMinutes = 60, string? extra = null)
        {
            return new TokenInfo
            {
                UserId = user.UserId ?? 0,
                UserName = user.UserName ?? "",
                StoreId = user.StoreId ?? 0,
                TenantId = user.TenantId ?? 0,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes).ToUnixTimeSeconds(),
                Extra = extra
            };
        }
    }
}
