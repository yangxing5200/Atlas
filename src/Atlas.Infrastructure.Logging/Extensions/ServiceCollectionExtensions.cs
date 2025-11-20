using Atlas.Infrastructure.Logging.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace Atlas.Infrastructure.Logging.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加 Atlas 日志服务
        /// </summary>
        public static IServiceCollection AddAtlasLogging(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            return services.AddAtlasLogging(configuration);
        }

        /// <summary>
        /// 使用 Atlas 日志中间件
        /// </summary>
        public static IApplicationBuilder UseAtlasLogging(
            this IApplicationBuilder app)
        {
            // 添加 Serilog 请求日志
            app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate =
                    "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

                options.GetLevel = (httpContext, elapsed, ex) =>
                {
                    if (ex != null) return LogEventLevel.Error;
                    if (httpContext.Response.StatusCode >= 500) return LogEventLevel.Error;
                    if (httpContext.Response.StatusCode >= 400) return LogEventLevel.Warning;
                    if (elapsed > 1000) return LogEventLevel.Warning; // 慢请求
                    return LogEventLevel.Information;
                };

                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                    diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                    diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
                    diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());

                    if (httpContext.User.Identity?.IsAuthenticated == true)
                    {
                        diagnosticContext.Set("UserIdentity", httpContext.User.Identity.Name);
                    }
                };
            });

            // 添加日志上下文中间件
            app.UseMiddleware<LogContextMiddleware>();

            return app;
        }
    }
}