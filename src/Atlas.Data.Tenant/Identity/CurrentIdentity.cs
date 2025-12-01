using Atlas.Core.Services;
using Atlas.Data.Tenant.Repositories;
using Atlas.Infrastructure.Caching.Abstractions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Atlas.Data.Tenant.Identity
{
    /// <summary>
    /// 当前用户服务实现（从HttpContext获取）
    /// </summary>
    public class CurrentIdentity :  ICurrentIdentity
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentIdentity(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

        public string? SessionId => User?.FindFirst("session_id")?.Value;

        public long? UserId =>
            long.TryParse(User?.FindFirst("uid")?.Value, out var id)
                ? id : null;

        public string UserName =>
            User?.FindFirst("uname")?.Value ?? string.Empty;

        public long? StoreId =>
            long.TryParse(User?.FindFirst("sid")?.Value, out var id)
                ? id : null;

        public long? TenantId =>
            long.TryParse(User?.FindFirst("tid")?.Value, out var id)
                ? id : null;

        public bool IsAuthenticated =>
            User?.Identity?.IsAuthenticated ?? false;
    }
}