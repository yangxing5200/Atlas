using Atlas.Core.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Atlas.Infrastructure.Common.Services
{
    /// <summary>
    /// 当前用户服务实现（从HttpContext获取）
    /// </summary>
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public long? UserId => GetUserId();

        public string UserName => GetUserName();

        public long? TenantId => GetTenantId();

        public bool IsAuthenticated =>
            _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

        private long? GetUserId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User == null) return null;

            // 尝试从多个Claim中获取UserId（兼容不同的JWT配置）
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier) // 标准Claim
                ?? httpContext.User.FindFirst("sub")                                  // JWT标准(subject)
                ?? httpContext.User.FindFirst("userId")                               // 自定义
                ?? httpContext.User.FindFirst("uid");                                 // 自定义

            if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }

            return null;
        }

        private string GetUserName()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User == null) return null;

            // 尝试获取用户名
            return httpContext.User.Identity?.Name
                ?? httpContext.User.FindFirst(ClaimTypes.Name)?.Value
                ?? httpContext.User.FindFirst("name")?.Value;
        }

        private long? GetTenantId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User == null) return null;

            // 尝试从Claim中获取TenantId
            var tenantIdClaim = httpContext.User.FindFirst("tenantId")
                ?? httpContext.User.FindFirst("tid");

            if (tenantIdClaim != null && long.TryParse(tenantIdClaim.Value, out var tenantId))
            {
                return tenantId;
            }

            return null;
        }
    }
}