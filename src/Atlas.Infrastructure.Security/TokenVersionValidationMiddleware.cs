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
    /// Token version validation middleware.
    /// Must be registered after TenantConnectionPreloadMiddleware.
    /// </summary>
    /// <remarks>
    /// 认证处理器只证明 Token 本身有效；该中间件再次确认用户当前 TokenVersion 和 Session 状态，
    /// 用于支持修改密码、管理员踢下线、退出登录等服务端主动撤销场景。
    /// </remarks>
    public class TokenVersionValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenVersionValidationMiddleware> _logger;
        
        // 默认跳过公开入口和基础设施端点，避免登录、健康检查等请求被版本校验拦截。
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
            // Step 0: Check if validation should be skipped
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

                // 先检查 Session 黑名单：这是最高频的主动失效路径，通常只访问本地缓存。
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

                // 再检查缓存中的 TokenVersion，避免每个请求都访问租户数据库。
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
                    // 缓存未命中时回源数据库，并使用显式 tenantId 查询以支持当前认证上下文初始化阶段。
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

                    // 先比对再写入缓存，避免把已经失效的版本重新推广到 L1。
                    if (currentVersion != tokenVersion)
                    {
                        _logger.LogWarning(
                            "TokenVersion mismatch (db) - UserId: {UserId}, Token: {TokenVersion}, Current: {CurrentVersion}",
                            userId, tokenVersion, currentVersion);
                        tokenCache.InvalidateUserTokens(userId); // Clean dirty cache
                        await HandleInvalidToken(context);
                        return;
                    }

                    tokenCache.SetUserTokenVersion(userId, currentVersion);
                    _logger.LogDebug("TokenVersion validated from DB - UserId: {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TokenVersion validation error - UserId: {UserId}", userId);
                await HandleInvalidToken(context); // Fail-safe: 校验链路异常时拒绝请求，避免放大权限风险。
                return;
            }

            await _next(context);
        }

        /// <summary>
        /// Checks if token version validation should be skipped for this request.
        /// </summary>
        private bool ShouldSkipValidation(HttpContext context)
        {
            // 1. Check if endpoint is marked with [AllowAnonymous]
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

            // 2. Check path whitelist
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            foreach (var skipPath in DefaultSkipPaths)
            {
                if (path.StartsWith(skipPath, StringComparison.OrdinalIgnoreCase))
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
