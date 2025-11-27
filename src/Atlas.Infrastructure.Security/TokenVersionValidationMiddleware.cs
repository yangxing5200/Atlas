using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Atlas.Infrastructure.Security
{
    /// <summary>
    /// Token 版本验证中间件
    /// 必须在 TenantConnectionPreloadMiddleware 之后注册
    /// </summary>
    public class TokenVersionValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenVersionValidationMiddleware> _logger;

        public TokenVersionValidationMiddleware(
            RequestDelegate next,
            ILogger<TokenVersionValidationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                await _next(context);
                return;
            }

            var userIdClaim = context.User.FindFirst("uid")?.Value;
            var tokenVersionClaim = context.User.FindFirst("token_version")?.Value;
            var sessionIdClaim = context.User.FindFirst("session_id")?.Value;

            if (!long.TryParse(userIdClaim, out var userId) ||
                !int.TryParse(tokenVersionClaim, out var tokenVersion))
            {
                await _next(context);
                return;
            }

            try
            {
                var tokenCache = context.RequestServices.GetRequiredService<ITokenCacheService>();

                // Step 1: Check session blacklist (memory only, fastest)
                if (!string.IsNullOrEmpty(sessionIdClaim))
                {
                    if (!tokenCache.IsSessionValid(sessionIdClaim))
                    {
                        _logger.LogWarning(
                            "Session invalidated - UserId: {UserId}, SessionId: {SessionId}",
                            userId, sessionIdClaim);
                        await HandleInvalidToken(context);
                        return;
                    }
                }

                // Step 2: Check cached TokenVersion (L1 memory, ~99% hit rate)
                var cachedVersion = tokenCache.GetUserTokenVersion(userId);

                if (cachedVersion.HasValue)
                {
                    if (cachedVersion.Value != tokenVersion)
                    {
                        _logger.LogWarning(
                            "TokenVersion mismatch (cached) - UserId: {UserId}, Token: {TokenVersion}, Current: {CurrentVersion}",
                            userId, tokenVersion, cachedVersion.Value);
                        await HandleInvalidToken(context);
                        return;
                    }
                }
                else
                {
                    // Step 3: Cache miss - load from database
                    var userRepo = context.RequestServices.GetRequiredService<IRepository<User>>();
                    var queryBuilder = await userRepo.QueryAsync();
                    var user = await queryBuilder
                        .Where(u => u.Id == userId && !u.IsDeleted)
                        .FirstOrDefaultAsync();
                    if (user == null)
                    {
                        _logger.LogWarning("User not found or deleted - UserId: {UserId}", userId);
                        await HandleInvalidToken(context);
                        return;
                    }

                    var currentVersion = user.TokenVersion;

                    // Verify before caching
                    if (currentVersion != tokenVersion)
                    {
                        _logger.LogWarning(
                            "TokenVersion mismatch (db) - UserId: {UserId}, Token: {TokenVersion}, Current: {CurrentVersion}",
                            userId, tokenVersion, currentVersion);
                        tokenCache.InvalidateUserTokens(userId); // Clean dirty cache
                        await HandleInvalidToken(context);
                        return;
                    }

                    // Cache valid version
                    tokenCache.SetUserTokenVersion(userId, currentVersion);
                    _logger.LogDebug("TokenVersion validated from DB - UserId: {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TokenVersion validation error - UserId: {UserId}", userId);
                await HandleInvalidToken(context); // Fail-safe: reject on error
                return;
            }

            await _next(context);
        }

        private static async Task HandleInvalidToken(HttpContext context)
        {
            var isAjaxOrApi = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                || context.Request.Headers[HeaderNames.Accept].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/api");

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;

            if (isAjaxOrApi)
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(
                    "{\"code\":401,\"message\":\"Token has been revoked. Please login again.\"}");
            }
            else
            {
                context.Response.Redirect("/login");
            }
        }
    }
}
