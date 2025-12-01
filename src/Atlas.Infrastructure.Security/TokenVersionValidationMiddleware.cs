using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Microsoft.AspNetCore.Authorization;
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
        
        // 默认跳过验证的路径前缀
        private static readonly string[] DefaultSkipPaths = new[]
        {
            "/api/user/login",
            "/api/user/register", 
            "/api/test/",
            "/swagger",
            "/health",
            "/.well-known"
        };

        public TokenVersionValidationMiddleware(
            RequestDelegate next,
            ILogger<TokenVersionValidationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Step 0: 检查是否应该跳过验证
            if (ShouldSkipValidation(context))
            {
                await _next(context);
                return;
            }

            if (context.User?.Identity?.IsAuthenticated != true)
            {
                await _next(context);
                return;
            }

            var userIdClaim = context.User.FindFirst("uid")?.Value;
            var tokenVersionClaim = context.User.FindFirst("token_version")?.Value;
            var sessionIdClaim = context.User.FindFirst("session_id")?.Value;
            var tenantIdClaim = context.User.FindFirst("tid")?.Value;

            if (!long.TryParse(userIdClaim, out var userId) ||
                !int.TryParse(tokenVersionClaim, out var tokenVersion) ||
                !long.TryParse(tenantIdClaim, out var tenantId))
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
                    var queryBuilder = await userRepo.QueryAsync(tenantId);
                    var user = await queryBuilder
                        .Where(u => u.Id == userId && !u.IsDeleted)
                        .FirstOrDefaultAsync();
                    if (user == null)
                    {
                        _logger.LogWarning("User not found or deleted - UserId: {UserId}, TenantId: {TenantId}", userId, tenantId);
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

        /// <summary>
        /// 检查是否应该跳过 Token 版本验证
        /// </summary>
        private bool ShouldSkipValidation(HttpContext context)
        {
            // 1. 检查端点是否标记了 [AllowAnonymous]
            var endpoint = context.GetEndpoint();
            if (endpoint != null)
            {
                var allowAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>();
                if (allowAnonymous != null)
                {
                    _logger.LogDebug("Skipping token validation for anonymous endpoint: {Path}", context.Request.Path);
                    return true;
                }
            }

            // 2. 检查路径白名单
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            foreach (var skipPath in DefaultSkipPaths)
            {
                if (path.StartsWith(skipPath.ToLowerInvariant()))
                {
                    _logger.LogDebug("Skipping token validation for whitelisted path: {Path}", context.Request.Path);
                    return true;
                }
            }

            return false;
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
