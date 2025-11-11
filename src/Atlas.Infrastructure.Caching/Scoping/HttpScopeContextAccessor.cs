// Scoping/HttpScopeContextAccessor.cs
using System;
using Microsoft.AspNetCore.Http;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.Scoping
{
    public class HttpScopeContextAccessor : IScopeContextAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string ContextKey = "__CacheScope";

        public HttpScopeContextAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public ScopeContext? Current
        {
            get
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext?.Items.TryGetValue(ContextKey, out var value) == true)
                {
                    return value as ScopeContext;
                }
                return null;
            }
            set
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    httpContext.Items[ContextKey] = value;
                }
            }
        }

        public string? TenantId => Current?.TenantId;
        public string? StoreId => Current?.StoreId;
        public string? UserId => Current?.UserId;
        public bool HasTenant => !string.IsNullOrEmpty(TenantId);
        public bool HasStore => !string.IsNullOrEmpty(StoreId);
        public bool HasUser => !string.IsNullOrEmpty(UserId);
    }
}