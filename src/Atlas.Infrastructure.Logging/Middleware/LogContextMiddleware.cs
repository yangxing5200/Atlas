using Atlas.Core.Context;
using Atlas.Core.Logging;
using Atlas.Core.Services;
using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Diagnostics;

namespace Atlas.Infrastructure.Logging.Middleware
{
    public class LogContextMiddleware
    {
        private readonly RequestDelegate _next;

        public LogContextMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task InvokeAsync(HttpContext context, ICurrentIdentity tenantContext)
        {
            // 1. 准备上下文数据
            var correlationId = context.TraceIdentifier;
            var operationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

            // 添加响应头便于前端/网关追踪
            context.Response.Headers["X-Correlation-Id"] = correlationId;

            // 2. 仅负责推送上下文属性 (Push Properties)
            // 这些属性会自动附加到后续 Service/Data 层产生的所有日志中
            using (LogContext.PushProperty(LogContextKeys.CorrelationId, correlationId))
            using (LogContext.PushProperty(LogContextKeys.OperationId, operationId))
            using (LogContext.PushProperty(LogContextKeys.TenantId, tenantContext.TenantId))
            using (LogContext.PushProperty(LogContextKeys.StoreId, tenantContext.StoreId))
            using (LogContext.PushProperty(LogContextKeys.UserId, tenantContext.UserId))
            // 这里的 RequestPath 和 Method 其实 UseSerilogRequestLogging 默认会记录，可以根据需要决定是否移除
            {
                await _next(context);
            }
        }
    }
}