using Atlas.Core.Security;
using Atlas.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Security
{
    public class CustomTokenService : ITokenService, IDisposable
    {
        private readonly ICryptoService _cryptoService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CustomTokenService> _logger;
        private readonly byte[] _secretKeyBytes;
        private readonly int _expirationMinutes;
        private readonly TokenOptions _options;
        private bool _disposed;

        // Token格式常量
        private const char TOKEN_SEPARATOR = '.';
        private const string TOKEN_VERSION = "1";
        private const int EXPECTED_TOKEN_PARTS = 4;

        public CustomTokenService(
            ICryptoService cryptoService,
            IMemoryCache cache,
            ILogger<CustomTokenService> logger,
            IOptions<TokenOptions> options)
        {
            _cryptoService = cryptoService;
            _cache = cache;
            _logger = logger;
            _options = options.Value;

            if (string.IsNullOrEmpty(_options.SecretKey))
                throw new ArgumentException("Token secret key cannot be empty");

            _secretKeyBytes = Encoding.UTF8.GetBytes(_options.SecretKey);
            _expirationMinutes = _options.ExpirationMinutes > 0 ? _options.ExpirationMinutes : 1440;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GenerateToken(ICurrentIdentity user, string? extra = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // 1. 创建token数据
            var tokenInfo = TokenInfo.Create(user, _expirationMinutes, extra);
            var serializedData = tokenInfo.ToString();

            // 2. 加密数据
            var encryptedData = _cryptoService.Encrypt(serializedData);

            // 3. 生成时间戳（秒级精度）
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            // 4. 构建待签名数据
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

            return dataToSign.ToString();
        }

        public async Task<TokenInfo?> ValidateTokenAsync(string token)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // ✅ 使用SHA256作为缓存键，避免HashCode碰撞
            var cacheKey = GetSecureCacheKey(token);

            if (_cache.TryGetValue<TokenInfo>(cacheKey, out var cachedInfo))
            {
                if (!cachedInfo.IsExpired)
                    return cachedInfo;

                _cache.Remove(cacheKey);
            }

            var result = await Task.Run(() => ValidateTokenCore(token));

            // ✅ 缓存时间缩短到30秒，减少已注销token仍有效的风险
            if (result != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
                    Size = 1 // 每个缓存条目占用1个单位
                };
                _cache.Set(cacheKey, result, cacheOptions);
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TokenInfo? ValidateToken(string token)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var cacheKey = GetSecureCacheKey(token);

            if (_cache.TryGetValue<TokenInfo>(cacheKey, out var cachedInfo))
            {
                if (!cachedInfo.IsExpired)
                    return cachedInfo;

                _cache.Remove(cacheKey);
            }

            var result = ValidateTokenCore(token);

            if (result != null)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromSeconds(30));
            }

            return result;
        }

        /// <summary>
        /// ✅ 使用SHA256生成安全的缓存键，避免HashCode碰撞
        /// </summary>
        private static string GetSecureCacheKey(string token)
        {
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
            // 只取前16字节转Base64，减少内存占用
            return $"token_{Convert.ToBase64String(hashBytes, 0, 16)}";
        }

        private TokenInfo? ValidateTokenCore(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                    return null;

                // 使用Span避免字符串分割的内存分配
                var tokenSpan = token.AsSpan();
                var separatorCount = 0;
                var indices = new int[3];

                for (int i = 0; i < tokenSpan.Length; i++)
                {
                    if (tokenSpan[i] == TOKEN_SEPARATOR)
                    {
                        if (separatorCount < 3)
                            indices[separatorCount] = i;
                        separatorCount++;
                    }
                }

                if (separatorCount != 3)
                {
                    _logger.LogWarning("Invalid token format: incorrect separator count");
                    return null;
                }

                // 提取token部分
                var version = tokenSpan.Slice(0, indices[0]);
                var timestamp = tokenSpan.Slice(indices[0] + 1, indices[1] - indices[0] - 1);
                var encryptedData = tokenSpan.Slice(indices[1] + 1, indices[2] - indices[1] - 1);
                var signature = tokenSpan.Slice(indices[2] + 1);

                // 验证版本
                if (!version.SequenceEqual(TOKEN_VERSION.AsSpan()))
                {
                    _logger.LogWarning("Invalid token version");
                    return null;
                }

                // 验证时间戳（防重放攻击）
                if (!long.TryParse(timestamp, out var tokenTimestamp))
                {
                    _logger.LogWarning("Invalid token timestamp format");
                    return null;
                }

                var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var tokenAge = currentTimestamp - tokenTimestamp;

                // ✅ Token生成超过配置时间则拒绝，且不允许未来时间戳
                var maxTokenAge = _expirationMinutes * 60 + 300; // 额外5分钟宽限
                if (tokenAge > maxTokenAge || tokenAge < 0)
                {
                    _logger.LogWarning("Token timestamp out of valid range: age={TokenAge}s", tokenAge);
                    return null;
                }

                // ✅ 验证签名（使用完整签名）
                var dataToVerify = tokenSpan.Slice(0, indices[2]).ToString();
                var expectedSignature = GenerateSignature(dataToVerify);

                if (!signature.SequenceEqual(expectedSignature.AsSpan()))
                {
                    _logger.LogWarning("Token signature verification failed");
                    return null;
                }

                // 解密数据
                var decryptedData = _cryptoService.Decrypt(encryptedData.ToString());
                if (string.IsNullOrEmpty(decryptedData))
                {
                    _logger.LogWarning("Token decryption failed");
                    return null;
                }

                // 解析TokenInfo
                var tokenInfo = TokenInfo.Parse(decryptedData);
                if (tokenInfo == null)
                {
                    _logger.LogWarning("Token data parsing failed");
                    return null;
                }

                // 检查过期
                if (tokenInfo.IsExpired)
                {
                    _logger.LogDebug("Token expired for user {UserId}", tokenInfo.UserId);
                    return null;
                }

                return tokenInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token validation error");
                return null;
            }
        }

        /// <summary>
        /// ✅ 生成完整的HMAC-SHA256签名（使用全部32字节）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GenerateSignature(string data)
        {
            using var hmac = new HMACSHA256(_secretKeyBytes);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var hash = hmac.ComputeHash(dataBytes);

            // ✅ 使用完整的32字节签名，提高安全性
            return Convert.ToBase64String(hash)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_secretKeyBytes != null)
                Array.Clear(_secretKeyBytes, 0, _secretKeyBytes.Length);

            if (_cryptoService is IDisposable disposable)
                disposable.Dispose();

            _disposed = true;
        }
    }

    public class TokenOptions
    {
        public string SecretKey { get; set; } = string.Empty;
        public int ExpirationMinutes { get; set; } = 1440; // 默认24小时
        public string CookieName { get; set; } = "lovelypets-auth-token";
    }
}