// Scoping/TenantResolver.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Atlas.Infrastructure.Caching.Scoping.Abstractions;

namespace Atlas.Infrastructure.Caching.Scoping
{
    public class TenantResolver : ITenantResolver
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantResolver(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public Task<string?> ResolveTenantIdAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return Task.FromResult<string?>(null);

            // Try header first
            if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue))
            {
                return Task.FromResult<string?>(headerValue.FirstOrDefault());
            }

            // Try claim
            var tenantClaim = httpContext.User?.FindFirst("tenant_id")?.Value
                           ?? httpContext.User?.FindFirst("http://schemas.atlas.com/tenant")?.Value;

            return Task.FromResult(tenantClaim);
        }
    }
}