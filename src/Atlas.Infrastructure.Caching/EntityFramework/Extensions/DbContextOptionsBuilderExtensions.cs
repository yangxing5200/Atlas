// EntityFramework/Extensions/DbContextOptionsBuilderExtensions.cs
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Atlas.Infrastructure.Caching.EntityFramework.Interceptors;

namespace Atlas.Infrastructure.Caching.EntityFramework.Extensions
{
    public static class DbContextOptionsBuilderExtensions
    {
        public static DbContextOptionsBuilder AddCacheInvalidation(
            this DbContextOptionsBuilder optionsBuilder,
            IServiceProvider serviceProvider)
        {
            var interceptor = serviceProvider.GetRequiredService<CacheInvalidationInterceptor>();
            return optionsBuilder.AddInterceptors(interceptor);
        }
    }
}