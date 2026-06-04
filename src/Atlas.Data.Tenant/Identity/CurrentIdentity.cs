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
        private readonly IExecutionIdentityAccessor _executionIdentityAccessor;

        public CurrentIdentity(
            IHttpContextAccessor httpContextAccessor,
            IExecutionIdentityAccessor executionIdentityAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _executionIdentityAccessor = executionIdentityAccessor;
        }

        private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

        public string? SessionId => _executionIdentityAccessor.Current?.SessionId ?? User?.FindFirst("session_id")?.Value;

        public long? UserId =>
            _executionIdentityAccessor.Current?.UserId ??
            (long.TryParse(User?.FindFirst("uid")?.Value, out var id)
                ? id : null);

        public string UserName =>
            _executionIdentityAccessor.Current?.UserName ?? User?.FindFirst("uname")?.Value ?? string.Empty;

        public long? StoreId =>
            _executionIdentityAccessor.Current?.StoreId ??
            (long.TryParse(User?.FindFirst("sid")?.Value, out var id)
                ? id : null);

        public long? TenantId =>
            _executionIdentityAccessor.Current?.TenantId ??
            (long.TryParse(User?.FindFirst("tid")?.Value, out var id)
                ? id : null);

        public bool IsAuthenticated =>
            _executionIdentityAccessor.Current?.IsAuthenticated ?? User?.Identity?.IsAuthenticated ?? false;
    }
}
