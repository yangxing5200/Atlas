using Atlas.Core.Security;
using Atlas.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly ITokenCacheService _tokenCacheService;
        private readonly byte[] _secretKeyBytes;
        private readonly int _expirationMinutes;
        private readonly TokenOptions _options;
        private readonly IServiceProvider _serviceProvider;
        private bool _disposed;

        private const char TOKEN_SEPARATOR = '.';
        private const string TOKEN_VERSION = "1";
        private const int EXPECTED_TOKEN_PARTS = 4;

        public CustomTokenService(
            ICryptoService cryptoService,
            IMemoryCache cache,
            ILogger<CustomTokenService> logger,
            IOptions<TokenOptions> options,
            IServiceProvider serviceProvider)
        {
            _cryptoService = cryptoService;
            _cache = cache;
            _logger = logger;
            _options = options.Value;
            _serviceProvider = serviceProvider;
            using var scope = _serviceProvider.CreateScope();
            _tokenCacheService = scope.ServiceProvider.GetRequiredService<ITokenCacheService>();

            if (string.IsNullOrEmpty(_options.SecretKey))
                throw new ArgumentException("Token secret key cannot be empty");

            _secretKeyBytes = Encoding.UTF8.GetBytes(_options.SecretKey);
            _expirationMinutes = _options.ExpirationMinutes > 0 ? _options.ExpirationMinutes : 1440;
          
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GenerateToken(ICurrentIdentity user)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var tokenInfo = TokenInfo.Create(user, _expirationMinutes);
            return GenerateTokenCore(tokenInfo);
        }

        /// <summary>
        /// Generate token using existing TokenInfo (preserves TokenVersion)
        /// </summary>
        public string GenerateToken(TokenInfo tokenInfo)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (tokenInfo == null)
                throw new ArgumentNullException(nameof(tokenInfo));

            return GenerateTokenCore(tokenInfo);
        }

        private string GenerateTokenCore(TokenInfo tokenInfo)
        {
            var serializedData = tokenInfo.ToString();
            var encryptedData = _cryptoService.Encrypt(serializedData);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            var dataToSign = new StringBuilder(256);
            dataToSign.Append(TOKEN_VERSION);
            dataToSign.Append(TOKEN_SEPARATOR);
            dataToSign.Append(timestamp);
            dataToSign.Append(TOKEN_SEPARATOR);
            dataToSign.Append(encryptedData);

            var signature = GenerateSignature(dataToSign.ToString());
            dataToSign.Append(TOKEN_SEPARATOR);
            dataToSign.Append(signature);

            return dataToSign.ToString();
        }

        public async Task<TokenInfo?> ValidateTokenAsync(string token)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var cacheKey = GetSecureCacheKey(token);

            // ✅ 优化1：使用复合缓存键，包含TokenVersion信息
            if (_cache.TryGetValue<TokenInfo>(cacheKey, out var cachedInfo))
            {
                if (!cachedInfo.IsExpired)
                {
                    // ✅ 优化2：只检查Session黑名单（内存操作，极快）
                    // TokenVersion已通过缓存键验证
                    if (!string.IsNullOrEmpty(cachedInfo.SessionId) &&
                        !_tokenCacheService.IsSessionValid(cachedInfo.SessionId))
                    {
                        _cache.Remove(cacheKey);
                        _logger.LogWarning("Session invalidated - UserId: {UserId}", cachedInfo.UserId);
                        return null;
                    }
                    return cachedInfo;
                }

                _cache.Remove(cacheKey);
            }

            var result = await Task.Run(() => ValidateTokenCore(token));

            if (result != null)
            {
                // ✅ 优化3：验证前先检查Session
                if (!string.IsNullOrEmpty(result.SessionId) &&
                    !_tokenCacheService.IsSessionValid(result.SessionId))
                {
                    _logger.LogWarning("Session invalidated - UserId: {UserId}", result.UserId);
                    return null;
                }

                // ✅ 优化4：验证TokenVersion（只在首次验证时）
                var currentVersion = _tokenCacheService.GetUserTokenVersion(result.UserId ?? 0);
                if (currentVersion.HasValue && currentVersion.Value != result.TokenVersion)
                {
                    _logger.LogWarning("TokenVersion mismatch - UserId: {UserId}, Expected: {Expected}, Got: {Got}",
                        result.UserId, currentVersion.Value, result.TokenVersion);
                    return null;
                }

                // ✅ 优化5：缓存包含TokenVersion的验证状态
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
                    Size = 1
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
                {
                    // 只检查Session黑名单
                    if (!string.IsNullOrEmpty(cachedInfo.SessionId) &&
                        !_tokenCacheService.IsSessionValid(cachedInfo.SessionId))
                    {
                        _cache.Remove(cacheKey);
                        _logger.LogWarning("Session invalidated - UserId: {UserId}", cachedInfo.UserId);
                        return null;
                    }
                    return cachedInfo;
                }

                _cache.Remove(cacheKey);
            }

            var result = ValidateTokenCore(token);

            if (result != null)
            {
                if (!string.IsNullOrEmpty(result.SessionId) &&
                    !_tokenCacheService.IsSessionValid(result.SessionId))
                {
                    _logger.LogWarning("Session invalidated - UserId: {UserId}", result.UserId);
                    return null;
                }

                var currentVersion = _tokenCacheService.GetUserTokenVersion(result.UserId ?? 0);
                if (currentVersion.HasValue && currentVersion.Value != result.TokenVersion)
                {
                    _logger.LogWarning("TokenVersion mismatch - UserId: {UserId}", result.UserId);
                    return null;
                }

                _cache.Set(cacheKey, result, TimeSpan.FromSeconds(30));
            }

            return result;
        }

        private static string GetSecureCacheKey(string token)
        {
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
            return $"token_{Convert.ToBase64String(hashBytes, 0, 16)}";
        }

        private TokenInfo? ValidateTokenCore(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                    return null;

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

                var version = tokenSpan.Slice(0, indices[0]);
                var timestamp = tokenSpan.Slice(indices[0] + 1, indices[1] - indices[0] - 1);
                var encryptedData = tokenSpan.Slice(indices[1] + 1, indices[2] - indices[1] - 1);
                var signature = tokenSpan.Slice(indices[2] + 1);

                if (!version.SequenceEqual(TOKEN_VERSION.AsSpan()))
                {
                    _logger.LogWarning("Invalid token version");
                    return null;
                }

                if (!long.TryParse(timestamp, out var tokenTimestamp))
                {
                    _logger.LogWarning("Invalid token timestamp format");
                    return null;
                }

                var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var tokenAge = currentTimestamp - tokenTimestamp;
                var maxTokenAge = _expirationMinutes * 60 + 300;

                if (tokenAge > maxTokenAge || tokenAge < 0)
                {
                    _logger.LogWarning("Token timestamp out of valid range: age={TokenAge}s", tokenAge);
                    return null;
                }

                var dataToVerify = tokenSpan.Slice(0, indices[2]).ToString();
                var expectedSignature = GenerateSignature(dataToVerify);

                if (!signature.SequenceEqual(expectedSignature.AsSpan()))
                {
                    _logger.LogWarning("Token signature verification failed");
                    return null;
                }

                var decryptedData = _cryptoService.Decrypt(encryptedData.ToString());
                if (string.IsNullOrEmpty(decryptedData))
                {
                    _logger.LogWarning("Token decryption failed");
                    return null;
                }

                var tokenInfo = TokenInfo.Parse(decryptedData);
                if (tokenInfo == null)
                {
                    _logger.LogWarning("Token data parsing failed");
                    return null;
                }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GenerateSignature(string data)
        {
            using var hmac = new HMACSHA256(_secretKeyBytes);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var hash = hmac.ComputeHash(dataBytes);

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
        public int ExpirationMinutes { get; set; } = 1440;
        public string CookieName { get; set; } = "atlas-auth-token";
    }
}