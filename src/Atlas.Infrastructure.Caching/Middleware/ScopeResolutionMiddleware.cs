// Middleware/ScopeResolutionMiddleware.cs
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Scoping.Abstractions;
using System.Net.Http;

namespace Atlas.Infrastructure.Caching.Middleware
{
    public class ScopeResolutionMiddleware
    {
        private readonly RequestDelegate _next;

        public ScopeResolutionMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task InvokeAsync(
            HttpContext context,
            IScopeContextAccessor scopeAccessor,
            ITenantResolver tenantResolver)
        {
            var scopeContext = new ScopeContext
            {
                TenantId = await tenantResolver.ResolveTenantIdAsync(),
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers["User-Agent"].ToString()
            };

            // Try to get store/user from headers or claims
            if (context.Request.Headers.TryGetValue("X-Store-Id", out var storeId))
            {
                scopeContext.StoreId = storeId;
            }

            if (context.User?.Identity?.IsAuthenticated == true)
            {
                scopeContext.UserId = context.User.FindFirst("sub")?.Value
                    ?? context.User.FindFirst("user_id")?.Value;
                scopeContext.UserName = context.User.Identity.Name;
            }

            scopeAccessor.Current = scopeContext;

            await _next(context);
        }
    }
}