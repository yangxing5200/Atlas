using Atlas.Data.Tenant.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
namespace Atlas.Extensions.DependencyInjection
{
    /// <summary>
    /// 租户相关中间件扩展
    /// </summary>
    public static class TenantMiddlewareExtensions
    {
        /// <summary>
        /// 添加租户认证授权管道
        /// 包含：认证 → 租户连接串预加载 → 授权
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        /// <returns></returns>
        public static IApplicationBuilder UseTenantAuthentication(this IApplicationBuilder app)
        {
            // 1. 认证：解析 JWT Token，设置 User.Claims（包含 TenantId）
            app.UseAuthentication();

            // 2. 预加载租户连接串：根据 TenantId 异步加载连接串到缓存
            app.UseMiddleware<TenantConnectionPreloadMiddleware>();

            // 3. 授权：检查用户权限
            app.UseAuthorization();

            return app;
        }

        /// <summary>
        /// 添加其他租户相关中间件（预留）
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        /// <returns></returns>
        public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app)
        {
            // 预留：可以添加租户上下文初始化、租户状态检查等中间件
            // app.UseMiddleware<TenantStatusCheckMiddleware>();
            // app.UseMiddleware<TenantFeatureToggleMiddleware>();

            return app;
        }
    }
}
