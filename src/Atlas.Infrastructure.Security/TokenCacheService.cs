using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace Atlas.Infrastructure.Security
{
    public class TokenCacheService : ITokenCacheService
    {
        private readonly ICacheService _cacheService;
        private readonly ILogger<TokenCacheService> _logger;
        private readonly IMemoryCache _localCache; // ✅ L1本地缓存

        public TokenCacheService(
            ICacheService cacheService,
            ILogger<TokenCacheService> logger,
            IMemoryCache localCache)
        {
            _cacheService = cacheService;
            _logger = logger;
            _localCache = localCache;
        }

        public int? GetUserTokenVersion(long userId)
        {
            try
            {
                // ✅ L1缓存：先查内存（极快，~10ns）
                var cacheKey = $"tv_{userId}";
                if (_localCache.TryGetValue<int?>(cacheKey, out var cachedVersion))
                {
                    return cachedVersion;
                }

                // ✅ L2缓存：Redis/分布式缓存（较快，~1-5ms）
                var version = _cacheService.Get<int?>(TokenCacheKeys.UserTokenVersion, userId);

                if (version.HasValue)
                {
                    // 缓存到本地，5秒过期
                    _localCache.Set(cacheKey, version, TimeSpan.FromSeconds(5));
                }

                return version;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get TokenVersion failed for user {UserId}", userId);
                return null;
            }
        }

        public void SetUserTokenVersion(long userId, int version)
        {
            try
            {
                // ✅ 先更新L2（持久化）
                _cacheService.Set<int?>(TokenCacheKeys.UserTokenVersion, version, userId);

                // ✅ 再更新L1（立即生效）
                var cacheKey = $"tv_{userId}";
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5),
                    Size = 1  // 指定该缓存项占用的大小单位
                };
                _localCache.Set(cacheKey, version, cacheOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Set TokenVersion failed for user {UserId}", userId);
            }
        }

        public void InvalidateUserTokens(long userId)
        {
            try
            {
                // ✅ 清除L1和L2
                _cacheService.Remove(TokenCacheKeys.UserTokenVersion, userId);
                _localCache.Remove($"tv_{userId}");

                _logger.LogInformation("Invalidated tokens for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalidate tokens failed for user {UserId}", userId);
            }
        }

        public bool IsSessionValid(string sessionId)
        {
            try
            {
                // ✅ Session黑名单也使用L1+L2缓存
                var cacheKey = $"si_{sessionId}";

                // L1检查
                if (_localCache.TryGetValue<bool>(cacheKey, out var isInvalid))
                {
                    return !isInvalid;
                }

                // L2检查
                var exists = _cacheService.Exists(TokenCacheKeys.InvalidSession, sessionId);

                // 如果在黑名单中，缓存到L1（5秒）
                if (exists)
                {
                    _localCache.Set(cacheKey, true, TimeSpan.FromSeconds(5));
                }

                return !exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Check session validity failed for {SessionId}", sessionId);
                return false; // Fail-safe
            }
        }

        public void InvalidateSession(string sessionId)
        {
            try
            {
                // ✅ 同时更新L1和L2
                _cacheService.Set(TokenCacheKeys.InvalidSession, true, sessionId);
                _localCache.Set($"si_{sessionId}", true, TimeSpan.FromSeconds(5));

                _logger.LogInformation("Invalidated session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalidate session failed for {SessionId}", sessionId);
            }
        }

        /// <summary>
        /// ✅ 新增：批量检查Session有效性（优化性能）
        /// </summary>
        public Dictionary<string, bool> AreSessionsValid(IEnumerable<string> sessionIds)
        {
            var result = new Dictionary<string, bool>();
            var toCheck = new List<string>();

            foreach (var sessionId in sessionIds)
            {
                var cacheKey = $"si_{sessionId}";
                if (_localCache.TryGetValue<bool>(cacheKey, out var isInvalid))
                {
                    result[sessionId] = !isInvalid;
                }
                else
                {
                    toCheck.Add(sessionId);
                }
            }

            // 批量检查剩余的
            if (toCheck.Any())
            {
                foreach (var sessionId in toCheck)
                {
                    var exists = _cacheService.Exists(TokenCacheKeys.InvalidSession, sessionId);
                    result[sessionId] = !exists;

                    if (exists)
                    {
                        _localCache.Set($"si_{sessionId}", true, TimeSpan.FromSeconds(5));
                    }
                }
            }

            return result;
        }
    }
}