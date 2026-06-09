using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Security
{
    public class TokenCacheService : ITokenCacheService
    {
        private static readonly TimeSpan TokenVersionLocalExpiration = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan InvalidSessionLocalExpiration = TimeSpan.FromSeconds(5);

        private readonly ICacheService _cacheService;
        private readonly ILogger<TokenCacheService> _logger;
        private readonly IMemoryCache _localCache;

        public TokenCacheService(
            ICacheService cacheService,
            ILogger<TokenCacheService> logger,
            IMemoryCache localCache)
        {
            _cacheService = cacheService;
            _logger = logger;
            _localCache = localCache;
        }

        public async Task<int?> GetUserTokenVersionAsync(long userId, CancellationToken ct = default)
        {
            var cacheKey = BuildTokenVersionLocalKey(userId);
            if (_localCache.TryGetValue<int?>(cacheKey, out var cachedVersion))
                return cachedVersion;

            try
            {
                var version = await _cacheService.GetAsync<int?>(TokenCacheKeys.UserTokenVersion, userId, ct);
                if (version.HasValue)
                    SetLocalTokenVersion(cacheKey, version);

                return version;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Get TokenVersion failed for user {UserId}", userId);
                return null;
            }
        }

        public async Task SetUserTokenVersionAsync(long userId, int version, CancellationToken ct = default)
        {
            try
            {
                await _cacheService.SetAsync<int?>(TokenCacheKeys.UserTokenVersion, version, userId, cancellationToken: ct);
                SetLocalTokenVersion(BuildTokenVersionLocalKey(userId), version);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Set TokenVersion failed for user {UserId}", userId);
            }
        }

        public async Task InvalidateUserTokensAsync(long userId, CancellationToken ct = default)
        {
            try
            {
                await _cacheService.RemoveAsync(TokenCacheKeys.UserTokenVersion, userId, ct);
                _localCache.Remove(BuildTokenVersionLocalKey(userId));

                _logger.LogInformation("Invalidated tokens for user {UserId}", userId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Invalidate tokens failed for user {UserId}", userId);
            }
        }

        public async Task<bool> IsSessionValidAsync(string sessionId, CancellationToken ct = default)
        {
            var cacheKey = BuildInvalidSessionLocalKey(sessionId);
            if (_localCache.TryGetValue<bool>(cacheKey, out var isInvalid))
                return !isInvalid;

            try
            {
                var exists = await _cacheService.ExistsAsync(TokenCacheKeys.InvalidSession, sessionId, ct);
                if (exists)
                    _localCache.Set(cacheKey, true, InvalidSessionLocalExpiration);

                return !exists;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Check session validity failed for {SessionId}", sessionId);
                return false;
            }
        }

        public async Task InvalidateSessionAsync(string sessionId, CancellationToken ct = default)
        {
            try
            {
                await _cacheService.SetAsync(TokenCacheKeys.InvalidSession, true, sessionId, cancellationToken: ct);
                _localCache.Set(BuildInvalidSessionLocalKey(sessionId), true, InvalidSessionLocalExpiration);

                _logger.LogInformation("Invalidated session {SessionId}", sessionId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Invalidate session failed for {SessionId}", sessionId);
            }
        }

        private void SetLocalTokenVersion(string cacheKey, int? version)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TokenVersionLocalExpiration,
                Size = 1
            };
            _localCache.Set(cacheKey, version, cacheOptions);
        }

        private static string BuildTokenVersionLocalKey(long userId)
        {
            return $"tv_{userId}";
        }

        private static string BuildInvalidSessionLocalKey(string sessionId)
        {
            return $"si_{sessionId}";
        }
    }
}
