// Extensions/ApplicationBuilderExtensions.cs
using Microsoft.AspNetCore.Builder;
using Atlas.Infrastructure.Caching.Middleware;

namespace Atlas.Infrastructure.Caching.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseAtlasCaching(this IApplicationBuilder app)
        {
            app.UseMiddleware<ScopeResolutionMiddleware>();
            return app;
        }
    }
}