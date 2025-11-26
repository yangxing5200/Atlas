using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Security
{
    public class TokenCacheService : ITokenCacheService
    {
        private readonly ICacheService _cacheService;
        private readonly ILogger<TokenCacheService> _logger;

        public TokenCacheService(
            ICacheService cacheService,
            ILogger<TokenCacheService> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        public int? GetUserTokenVersion(long userId)
        {
            try
            {
                return _cacheService.Get<int?>(TokenCacheKeys.UserTokenVersion, userId);
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
                _cacheService.Set<int?>(TokenCacheKeys.UserTokenVersion, version, userId);
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
                _cacheService.Remove(TokenCacheKeys.UserTokenVersion, userId);
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
                return !_cacheService.Exists(TokenCacheKeys.InvalidSession, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Check session validity failed for {SessionId}", sessionId);
                return false; // Fail-safe: reject on error
            }
        }

        public void InvalidateSession(string sessionId)
        {
            _cacheService.Set(TokenCacheKeys.InvalidSession, true, sessionId);
            _logger.LogInformation("Invalidated session {SessionId}", sessionId);
        }
    }
}
