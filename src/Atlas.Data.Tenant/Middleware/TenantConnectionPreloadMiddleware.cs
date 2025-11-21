using Atlas.Data.Tenant.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Middleware
{
    /// <summary>
    /// 租户连接串预加载中间件
    /// 在请求开始时异步加载连接串到缓存，后续Repository可同步获取
    /// </summary>
    public class TenantConnectionPreloadMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantConnectionPreloadMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 修正：使用泛型方法获取服务
            var factory = context.RequestServices.GetService<ITenantDbContextFactory>();

            if (factory != null)
            {
                try
                {
                    // 预加载只读库连接串到缓存
                    // 创建后立即释放，仅为了触发连接串加载
                    using (var readContext = await factory.GetReadonlyDbContextAsync(context.RequestAborted))
                    {
                        // 连接串已缓存，后续可同步获取
                    }
                }
                catch
                {
                    // 预加载失败不影响请求继续，后续会重新尝试
                }
            }

            await _next(context);
        }
    }
}