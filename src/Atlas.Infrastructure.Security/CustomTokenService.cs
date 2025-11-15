using Atlas.Core.Security;
using Atlas.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Security
{
    public  class CustomTokenService : ITokenService
    {
        private readonly ICryptoService _cryptoService;
        private readonly IMemoryCache _cache;
        private readonly byte[] _secretKeyBytes;
        private readonly int _expirationMinutes;
        private readonly TokenOptions _options;
        // Token格式常量
        private const char TOKEN_SEPARATOR = '.';
        private const string TOKEN_VERSION = "1";
        private const int EXPECTED_TOKEN_PARTS = 4;

        // 性能优化：预编译的HMAC实例
        private readonly ThreadLocal<HMACSHA256> _hmac;

        public CustomTokenService(
            ICryptoService cryptoService,
            IMemoryCache cache,
            IOptions<TokenOptions> options)
        {
            _cryptoService = cryptoService;
            _cache = cache;
            _options = options.Value;
            _secretKeyBytes = Encoding.UTF8.GetBytes(_options.SecretKey);
            _expirationMinutes = 1440;

            // ThreadLocal确保每个线程有自己的HMAC实例（线程安全+高性能）
            _hmac = new ThreadLocal<HMACSHA256>(() => new HMACSHA256(_secretKeyBytes));
        }

        /// <summary>
        /// 生成自定义格式的Token
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GenerateToken(ICurrentIdentity user, string? extra = null)
        {
            // 1. 创建token数据
            var tokenInfo = TokenInfo.Create(user, _expirationMinutes, extra);
            var serializedData = tokenInfo.ToString();

            // 2. 加密数据
            var encryptedData = _cryptoService.Encrypt(serializedData);

            // 3. 生成时间戳（秒级精度足够）
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            // 4. 构建待签名数据（使用StringBuilder避免字符串连接）
            var dataToSign = new StringBuilder(256);
            dataToSign.Append(TOKEN_VERSION);
            dataToSign.Append(TOKEN_SEPARATOR);
            dataToSign.Append(timestamp);
            dataToSign.Append(TOKEN_SEPARATOR);
            dataToSign.Append(encryptedData);

            // 5. 生成签名
            var signature = GenerateSignature(dataToSign.ToString());

            // 6. 组合最终token
            dataToSign.Append(TOKEN_SEPARATOR);
            dataToSign.Append(signature);

            var token = dataToSign.ToString();

            return token;
        }

        /// <summary>
        /// 异步验证Token（用于高并发场景）
        /// </summary>
        public async Task<TokenInfo?> ValidateTokenAsync(string token)
        {
            // 先检查缓存
            var cacheKey = $"token_valid_{token.GetHashCode()}";
            if (_cache.TryGetValue<TokenInfo>(cacheKey, out var cachedInfo))
            {
                // 再次检查是否过期
                if (!cachedInfo.IsExpired)
                    return cachedInfo;

                // 过期则移除缓存
                _cache.Remove(cacheKey);
            }

            // 异步验证
            var result = await Task.Run(() => ValidateTokenCore(token));

            // 缓存有效的token信息（5分钟）
            if (result != null)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            }

            return result;
        }

        /// <summary>
        /// 同步验证Token
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TokenInfo? ValidateToken(string token)
        {
            // 先检查缓存
            var cacheKey = $"token_valid_{token.GetHashCode()}";
            if (_cache.TryGetValue<TokenInfo>(cacheKey, out var cachedInfo))
            {
                if (!cachedInfo.IsExpired)
                    return cachedInfo;

                _cache.Remove(cacheKey);
            }

            var result = ValidateTokenCore(token);

            // 缓存有效的token信息
            if (result != null)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            }

            return result;
        }

        /// <summary>
        /// Token验证核心逻辑
        /// </summary>
        private TokenInfo? ValidateTokenCore(string token)
        {
            try
            {
                // 1. 快速检查token格式
                if (string.IsNullOrEmpty(token))
                    return null;

                // 使用Span避免字符串分割的内存分配
                var tokenSpan = token.AsSpan();
                var separatorCount = 0;
                var lastSeparatorIndex = -1;
                var indices = new int[3]; // 存储分隔符位置

                for (int i = 0; i < tokenSpan.Length; i++)
                {
                    if (tokenSpan[i] == TOKEN_SEPARATOR)
                    {
                        if (separatorCount < 3)
                            indices[separatorCount] = i;
                        separatorCount++;
                        lastSeparatorIndex = i;
                    }
                }

                if (separatorCount != 3)
                {
                    return null;
                }

                // 2. 提取token部分（使用Span切片）
                var version = tokenSpan.Slice(0, indices[0]);
                var timestamp = tokenSpan.Slice(indices[0] + 1, indices[1] - indices[0] - 1);
                var encryptedData = tokenSpan.Slice(indices[1] + 1, indices[2] - indices[1] - 1);
                var signature = tokenSpan.Slice(indices[2] + 1);

                // 3. 验证版本
                if (!version.SequenceEqual(TOKEN_VERSION.AsSpan()))
                {
                    return null;
                }

                // 4. 验证时间戳（防重放攻击）
                if (!long.TryParse(timestamp, out var tokenTimestamp))
                    return null;

                var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var tokenAge = currentTimestamp - tokenTimestamp;

                // Token生成超过24小时则拒绝
                if (tokenAge > 86400 || tokenAge < -60) // 允许60秒时钟偏差
                {
                    return null;
                }

                // 5. 验证签名
                var dataToVerify = tokenSpan.Slice(0, indices[2]).ToString();
                var expectedSignature = GenerateSignature(dataToVerify);
                if (!signature.SequenceEqual(expectedSignature.AsSpan()))
                {
                    return null;
                }

                // 6. 解密数据
                var decryptedData = _cryptoService.Decrypt(encryptedData.ToString());
                if (string.IsNullOrEmpty(decryptedData))
                {
                    return null;
                }

                // 7. 解析UserTokenInfo
                var tokenInfo = TokenInfo.Parse(decryptedData);
                if (tokenInfo == null)
                {
                    return null;
                }

                // 8. 检查过期
                if (tokenInfo.IsExpired)
                {
                    return null;
                }

                return tokenInfo;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 生成HMAC签名（优化版本）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GenerateSignature(string data)
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var hash = _hmac.Value!.ComputeHash(dataBytes);

            // 只使用前16字节，Base64编码
            return Convert.ToBase64String(hash, 0, 16)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }

    public class TokenOptions
    {
        public string SecretKey { get; set; } = string.Empty;
        public int ExpirationMinutes { get; set; } = 1440;
    }
}
