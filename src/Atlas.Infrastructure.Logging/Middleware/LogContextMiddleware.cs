using Atlas.Core.Context;
using Atlas.Core.Logging;
using Atlas.Core.Services;
using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Diagnostics;

namespace Atlas.Infrastructure.Logging.Middleware
{
    /// <summary>
    /// 日志上下文中间件 - 将业务上下文信息推送到日志系统
    /// </summary>
    /// <remarks>
    /// 该中间件应放在认证之后、业务处理之前，这样后续日志能自动携带租户、门店和用户信息。
    /// </remarks>
    public class LogContextMiddleware
    {
        private readonly RequestDelegate _next;

        public LogContextMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task InvokeAsync(HttpContext context, ICurrentIdentity? currentIdentity = null)
        {
            // 1. 获取追踪标识
            var correlationId = context.TraceIdentifier;
            var operationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

            // 2. 添加响应头便于前端/网关追踪
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Correlation-Id"] = correlationId;
                context.Response.Headers["X-Operation-Id"] = operationId;
                return Task.CompletedTask;
            });

            // LogContext 基于 async flow 传播；using 作用域结束后属性会自动弹出，避免污染其他请求。
            using (LogContext.PushProperty(LogContextKeys.CorrelationId, correlationId))
            using (LogContext.PushProperty(LogContextKeys.OperationId, operationId))
            using (LogContext.PushProperty(LogContextKeys.RequestPath, context.Request.Path.Value))
            using (LogContext.PushProperty(LogContextKeys.RequestMethod, context.Request.Method))
            {
                // 如果存在租户上下文，添加租户信息
                if (currentIdentity != null)
                {
                    using (LogContext.PushProperty(LogContextKeys.TenantId, currentIdentity.TenantId))
                    using (LogContext.PushProperty(LogContextKeys.StoreId, currentIdentity.StoreId))
                    using (LogContext.PushProperty(LogContextKeys.UserId, currentIdentity.UserId))
                    {
                        await _next(context);
                    }
                }
                else
                {
                    await _next(context);
                }
            }
        }
    }
}
