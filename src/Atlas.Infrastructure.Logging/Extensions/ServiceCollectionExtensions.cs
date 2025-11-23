using Atlas.Infrastructure.Logging.Configuration;
using Atlas.Infrastructure.Logging.Enrichers;
using Atlas.Infrastructure.Logging.Middleware;
using Atlas.Infrastructure.Logging.Policies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

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
            // 绑定配置
            var options = configuration
                .GetSection(LoggingOptions.SectionName)
                .Get<LoggingOptions>() ?? new LoggingOptions();

            services.Configure<LoggingOptions>(
                configuration.GetSection(LoggingOptions.SectionName));

            // 配置 Serilog
            Log.Logger = CreateLogger(configuration, options);

            // 注册自定义服务
            services.AddSingleton<CorrelationIdEnricher>();
            services.AddSingleton<PerformanceEnricher>();

            return services;
        }

        private static Serilog.ILogger CreateLogger(
            IConfiguration configuration,
            LoggingOptions options)
        {
            var loggerConfig = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "Atlas")
                .Enrich.WithProperty("Environment",
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProcessId();

            // 解析最小日志级别 (带容错)
            if (!Enum.TryParse<LogEventLevel>(options.MinimumLevel, true, out var minimumLevel))
            {
                minimumLevel = LogEventLevel.Information;
                Console.WriteLine($"警告: 无效的日志级别 '{options.MinimumLevel}', 使用默认值 'Information'");
            }
            loggerConfig.MinimumLevel.Is(minimumLevel);

            // 配置框架日志级别
            loggerConfig
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information);

            // 敏感数据脱敏策略
            if (options.EnableSensitiveDataFilter)
            {
                loggerConfig.Destructure.With(new SensitiveDataDestructuringPolicy(options.SensitiveFields));
            }

            // 控制台输出
            if (options.EnableConsole)
            {
                loggerConfig.WriteTo.Async(a => a.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Debug));
            }

            // 文件输出 - 所有日志
            if (options.EnableFile)
            {
                loggerConfig.WriteTo.Async(a => a.File(
                    path: options.FilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: options.RetainedFileCount,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Information));

                // 错误日志单独文件
                loggerConfig.WriteTo.Async(a => a.File(
                    path: options.ErrorFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: options.ErrorRetainedFileCount,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Error));

                // 审计日志（JSON 格式）
                loggerConfig.WriteTo.Async(a => a.File(
                    formatter: new CompactJsonFormatter(),
                    path: options.AuditFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: options.AuditRetainedFileCount,
                    restrictedToMinimumLevel: LogEventLevel.Information));
            }

            // Seq 输出
            if (options.EnableSeq && !string.IsNullOrEmpty(options.SeqServerUrl))
            {
                loggerConfig.WriteTo.Seq(
                    serverUrl: options.SeqServerUrl,
                    apiKey: options.SeqApiKey,
                    restrictedToMinimumLevel: LogEventLevel.Information);
            }

            return loggerConfig.CreateLogger();
        }

        /// <summary>
        /// 使用 Atlas 日志中间件
        /// </summary>
        public static IApplicationBuilder UseAtlasLogging(
            this IApplicationBuilder app)
        {
            // 从配置中获取选项
            var options = app.ApplicationServices
                .GetService<Microsoft.Extensions.Options.IOptions<LoggingOptions>>()?.Value
                ?? new LoggingOptions();

            // 添加 Serilog 请求日志
            app.UseSerilogRequestLogging(opts =>
            {
                opts.MessageTemplate =
                    "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

                opts.GetLevel = (httpContext, elapsed, ex) =>
                {
                    if (ex != null) return LogEventLevel.Error;
                    if (httpContext.Response.StatusCode >= 500) return LogEventLevel.Error;
                    if (httpContext.Response.StatusCode >= 400) return LogEventLevel.Warning;
                    if (elapsed > options.SlowOperationThresholdMs) return LogEventLevel.Warning;
                    return LogEventLevel.Information;
                };

                opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                    diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                    diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
                    diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());

                    if (httpContext.User.Identity?.IsAuthenticated == true)
                    {
                        diagnosticContext.Set("UserName", httpContext.User.Identity.Name);
                    }

                    // 根据配置记录请求体和响应体
                    if (options.LogRequestBody && httpContext.Request.ContentLength > 0)
                    {
                        // 注意：实际读取请求体需要启用 EnableBuffering
                        diagnosticContext.Set("RequestBodyLogged", true);
                    }

                    if (options.LogResponseBody)
                    {
                        diagnosticContext.Set("ResponseBodyLogged", true);
                    }
                };
            });

            // 添加日志上下文中间件
            app.UseMiddleware<LogContextMiddleware>();

            return app;
        }

        /// <summary>
        /// 使用 Atlas 日志中间件 (WebApplication 重载)
        /// </summary>
        public static WebApplication UseAtlasLogging(
            this WebApplication app)
        {
            UseAtlasLogging((IApplicationBuilder)app);
            return app;
        }
    }
}