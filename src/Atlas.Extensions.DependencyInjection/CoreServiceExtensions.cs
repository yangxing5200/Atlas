using System.Reflection.Emit;
using Atlas.Core.Configuration;
using Atlas.Core.IdGenerators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Extensions.DependencyInjection;

/// <summary>
/// Atlas Core 服务注册扩展
/// </summary>
public static class CoreServiceExtensions
{
    /// <summary>
    /// 添加 Atlas Core 所有服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAtlasCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 添加 Snowflake ID 生成器
        services.AddSnowflakeIdGenerator(configuration);

        // 未来可以在这里添加其他 Core 服务
        // services.AddOtherCoreServices();

        return services;
    }

    /// <summary>
    /// 添加 Snowflake ID 生成器（从配置文件）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// appsettings.json:
    /// {
    ///   "Snowflake": {
    ///     "WorkerId": 1,
    ///     "DatacenterId": 1
    ///   }
    /// }
    /// 
    /// Program.cs:
    /// builder.Services.AddSnowflakeIdGenerator(builder.Configuration);
    /// </example>
    public static IServiceCollection AddSnowflakeIdGenerator(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 读取配置
        var section = configuration.GetSection("Snowflake");
        var options = section.Get<SnowflakeOptions>();

        if (options == null)
        {
            // 如果配置不存在，使用默认值（基于环境变量或机器名）
            options = GetDefaultOptions();
        }

        // 验证配置
        options.Validate();

        // 注册配置对象
        services.Configure<SnowflakeOptions>(opt =>
        {
            opt.WorkerId = options.WorkerId;
            opt.DatacenterId = options.DatacenterId;
        });

        // 注册 ID 生成器为单例
        services.TryAddSingleton<IIdGenerator>(sp =>
        {
            return new SnowflakeIdGenerator(options.WorkerId, options.DatacenterId);
        });

        return services;
    }

    /// <summary>
    /// 添加 Snowflake ID 生成器（手动指定参数）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="workerId">机器ID (0-31)</param>
    /// <param name="datacenterId">数据中心ID (0-31)</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// builder.Services.AddSnowflakeIdGenerator(workerId: 1, datacenterId: 1);
    /// </example>
    public static IServiceCollection AddSnowflakeIdGenerator(
        this IServiceCollection services,
        long workerId,
        long datacenterId)
    {
        var options = new SnowflakeOptions
        {
            WorkerId = workerId,
            DatacenterId = datacenterId
        };
        options.Validate();

        services.TryAddSingleton<IIdGenerator>(
            new SnowflakeIdGenerator(workerId, datacenterId));

        return services;
    }

    /// <summary>
    /// 添加 Snowflake ID 生成器（自动检测配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="datacenterId">数据中心ID，WorkerId 将自动检测</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// // WorkerId 将基于机器名或环境变量自动生成
    /// builder.Services.AddSnowflakeIdGeneratorAuto(datacenterId: 1);
    /// </example>
    public static IServiceCollection AddSnowflakeIdGeneratorAuto(
        this IServiceCollection services,
        long datacenterId = 1)
    {
        var workerId = GetDefaultWorkerId();
        return services.AddSnowflakeIdGenerator(workerId, datacenterId);
    }

    /// <summary>
    /// 添加 Snowflake ID 生成器（从委托工厂）
    /// 适用于需要在运行时动态确定配置的场景
    /// </summary>
    public static IServiceCollection AddSnowflakeIdGenerator(
        this IServiceCollection services,
        Func<IServiceProvider, (long workerId, long datacenterId)> optionsFactory)
    {
        services.TryAddSingleton<IIdGenerator>(sp =>
        {
            var (workerId, datacenterId) = optionsFactory(sp);
            return new SnowflakeIdGenerator(workerId, datacenterId);
        });

        return services;
    }

    #region 私有辅助方法

    /// <summary>
    /// 获取默认配置
    /// </summary>
    private static SnowflakeOptions GetDefaultOptions()
    {
        // 优先尝试从环境变量读取
        var envWorkerId = Environment.GetEnvironmentVariable("SNOWFLAKE_WORKER_ID");
        var envDatacenterId = Environment.GetEnvironmentVariable("SNOWFLAKE_DATACENTER_ID");

        if (!string.IsNullOrEmpty(envWorkerId) && long.TryParse(envWorkerId, out var workerId) &&
            !string.IsNullOrEmpty(envDatacenterId) && long.TryParse(envDatacenterId, out var datacenterId))
        {
            return new SnowflakeOptions
            {
                WorkerId = workerId,
                DatacenterId = datacenterId
            };
        }

        // 否则使用基于机器名的默认值
        return new SnowflakeOptions
        {
            WorkerId = GetDefaultWorkerId(),
            DatacenterId = 1
        };
    }

    /// <summary>
    /// 获取默认的 WorkerId（基于机器名）
    /// </summary>
    private static long GetDefaultWorkerId()
    {
        // 方式1: 从环境变量
        var envWorkerId = Environment.GetEnvironmentVariable("SNOWFLAKE_WORKER_ID");
        if (!string.IsNullOrEmpty(envWorkerId) && long.TryParse(envWorkerId, out var parsedId))
        {
            if (parsedId >= 0 && parsedId <= 31)
                return parsedId;
        }

        // 方式2: 基于机器名的 Hash
        var machineName = Environment.MachineName;
        var hash = machineName.GetHashCode();
        return Math.Abs(hash) % 32;
    }

    #endregion
}