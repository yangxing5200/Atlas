using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
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
        private readonly ILogger<TenantConnectionPreloadMiddleware> _logger;

        public TenantConnectionPreloadMiddleware(
            RequestDelegate next,
            ILogger<TenantConnectionPreloadMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var connProvider = context.RequestServices.GetService<ITenantDbConnProvider>();
            var dataScope = context.RequestServices.GetService<IDataScope>();

            if (connProvider?.TenantId != null)
            {
                try
                {
                    // 预加载连接字符串
                    _ = await connProvider.GetConnStringAsync(context.RequestAborted);
                    _ = await connProvider.GetReadonlyConnStringAsync(context.RequestAborted);

                    // ✅ 预加载ShareStoreIds（如果有StoreId）
                    if (dataScope != null && dataScope.StoreId.HasValue)
                    {
                        await dataScope.PreloadShareStoreIdsAsync(context.RequestAborted);
                    }
                }
                catch (Exception ex)
                {
                    // 记录警告但不中断请求流程
                    _logger.LogWarning(ex,
                        "预加载租户数据失败 - TenantId: {TenantId}, StoreId: {StoreId}",
                        connProvider.TenantId,
                        dataScope?.StoreId);
                }
            }

            await _next(context);
        }

    }
}