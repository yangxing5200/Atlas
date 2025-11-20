using Atlas.Core.Context;
using Atlas.Core.Services;
using Atlas.Infrastructure.Logging.Enrichers;
using Atlas.Infrastructure.Logging.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Atlas.Infrastructure.Logging.Configuration
{
    /// <summary>
    /// Serilog 配置
    /// </summary>
    public static class SerilogConfiguration
    {
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
            Log.Logger = CreateLogger(configuration, options, services.BuildServiceProvider());

            return services;
        }

        private static Serilog.ILogger CreateLogger(
            IConfiguration configuration,
            LoggingOptions options,
            IServiceProvider serviceProvider)
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

            // 解析最小日志级别
            var minimumLevel = Enum.Parse<LogEventLevel>(options.MinimumLevel);
            loggerConfig.MinimumLevel.Is(minimumLevel);

            // 配置框架日志级别
            loggerConfig
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information);

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
                    retainedFileCountLimit: options.RetainedFileCountLimit,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Information));

                // 错误日志单独文件
                loggerConfig.WriteTo.Async(a => a.File(
                    path: options.ErrorFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: options.ErrorRetainedFileCountLimit,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Error));

                // 审计日志（JSON 格式）
                loggerConfig.WriteTo.Async(a => a.File(
                    formatter: new CompactJsonFormatter(),
                    path: options.AuditFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 365,
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

            // 敏感数据过滤
            if (options.EnableSensitiveDataFilter)
            {
                loggerConfig.Filter.With(new SensitiveDataFilter(options.SensitiveFields));
            }

            return loggerConfig.CreateLogger();
        }
    }
}