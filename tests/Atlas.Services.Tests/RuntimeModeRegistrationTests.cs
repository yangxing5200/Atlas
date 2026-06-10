using Atlas.BackgroundTasks;
using Atlas.Exporting;
using Atlas.Exporting.Reconciliation;
using Atlas.Core.Services;
using Atlas.Data.Global;
using Atlas.Extensions.DependencyInjection;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Messaging.Abstractions;
using Atlas.Services.Tenant;
using Atlas.Services.Tenant.Runtime.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Atlas.Services.Tests;

public sealed class RuntimeModeRegistrationTests
{
    [Theory]
    [InlineData("WebApi", AtlasRuntimeMode.WebApi)]
    [InlineData("web-api", AtlasRuntimeMode.WebApi)]
    [InlineData("Worker", AtlasRuntimeMode.Worker)]
    [InlineData("Migration", AtlasRuntimeMode.Migration)]
    public void ParseMode_RecognizesSupportedValues(string value, AtlasRuntimeMode expected)
    {
        Assert.Equal(expected, AtlasRuntimeModeOptions.ParseMode(value));
    }

    [Fact]
    public void ParseMode_RejectsInvalidValue()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => AtlasRuntimeModeOptions.ParseMode("server"));

        Assert.Contains("Supported values are WebApi, Worker, and Migration", exception.Message);
    }

    [Fact]
    public void WebApiMode_Defaults_DoNotEnableBackgroundExecutionPlane()
    {
        var options = AtlasRuntimeModeOptions.FromConfiguration(
            new ConfigurationBuilder().Build(),
            AtlasRuntimeMode.WebApi);

        Assert.False(options.ShouldEnableMessagingConsumers());
        Assert.False(options.ShouldEnableTenantOutboxDispatcher());
        Assert.False(options.ShouldEnableBackgroundJobWorker());
        Assert.False(options.ShouldEnableRecurringTaskRunner());
    }

    [Fact]
    public void WorkerMode_Defaults_EnableBackgroundExecutionPlane()
    {
        var options = AtlasRuntimeModeOptions.FromConfiguration(
            new ConfigurationBuilder().Build(),
            AtlasRuntimeMode.Worker);

        Assert.True(options.ShouldEnableMessagingConsumers());
        Assert.True(options.ShouldEnableTenantOutboxDispatcher());
        Assert.True(options.ShouldEnableBackgroundJobWorker());
        Assert.True(options.ShouldEnableRecurringTaskRunner());
    }

    [Fact]
    public void AddAtlasBackgroundTaskRuntime_WebApiDefaults_RegisterNoHostedWorkers()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddAtlasBackgroundTaskRuntime(
            configuration,
            enableRecurringTaskRunnerByDefault: false,
            enableBackgroundJobWorkerByDefault: false);

        Assert.Contains(services, service => service.ServiceType == typeof(IBackgroundJobClient));
        Assert.DoesNotContain(services, IsHostedService<RecurringTaskRunner>);
        Assert.DoesNotContain(services, IsHostedService<BackgroundJobWorker>);
    }

    [Fact]
    public void AddAtlasBackgroundTaskRuntime_WorkerDefaults_RegisterHostedWorkers()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddAtlasBackgroundTaskRuntime(
            configuration,
            enableRecurringTaskRunnerByDefault: true,
            enableBackgroundJobWorkerByDefault: true);

        Assert.Contains(services, IsHostedService<RecurringTaskRunner>);
        Assert.Contains(services, IsHostedService<BackgroundJobWorker>);
    }

    [Fact]
    public void AddAtlasBackgroundTaskRuntime_EmptyQueues_UseDefaultQueue()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundTasks:OneTimeJobs:Queues:0"] = " "
            })
            .Build();

        services.AddAtlasBackgroundTaskRuntime(
            configuration,
            enableRecurringTaskRunnerByDefault: true,
            enableBackgroundJobWorkerByDefault: true);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BackgroundJobWorkerOptions>>().Value;

        Assert.Collection(
            options.Queues,
            queue => Assert.Equal(BackgroundJobQueues.Default, queue));
    }

    [Fact]
    public void AddAtlasCore_WebApiMode_DoesNotRegisterBackgroundHostedServices()
    {
        var services = new ServiceCollection();

        services.AddAtlasCore(CreateCoreConfiguration(AtlasRuntimeMode.WebApi, messagingProvider: "RabbitMQ"));

        Assert.DoesNotContain(services, service => service.ServiceType == typeof(IHostedService));
        Assert.Contains(services, service => service.ServiceType == typeof(IBackgroundJobClient));
        Assert.Contains(services, service => service.ServiceType == typeof(IExportJobService));
        Assert.Contains(services, service => service.ServiceType == typeof(ITenantDomainEventOutbox));
        Assert.Contains(services, service =>
            service.ServiceType == typeof(IDomainEventPublisher) &&
            service.ImplementationType == typeof(NoOpDomainEventPublisher));
    }

    [Fact]
    public void AddAtlasCore_WorkerMode_Defaults_RegisterBackgroundHostedServices()
    {
        var services = new ServiceCollection();

        services.AddAtlasCore(CreateCoreConfiguration(AtlasRuntimeMode.Worker, messagingProvider: "RabbitMQ"));

        Assert.Contains(services, IsHostedService<TenantOutboxDispatcher>);
        Assert.Contains(services, IsHostedService<BackgroundJobWorker>);
        Assert.Contains(services, IsHostedService<RecurringTaskRunner>);
        Assert.Contains(services, service =>
            service.ServiceType == typeof(IBackgroundJobHandler) &&
            service.ImplementationType == typeof(ExportJobHandler));
        Assert.Contains(services, service =>
            service.ServiceType == typeof(IRecurringTask) &&
            service.ImplementationType == typeof(ExportJobReconciliationTask));
    }

    [Fact]
    public void AddAtlasWorker_WithoutConfiguredMode_DefaultsToWorkerExecutionPlane()
    {
        var services = new ServiceCollection();

        services.AddAtlasWorker(
            CreateCoreConfiguration(
                AtlasRuntimeMode.WebApi,
                messagingProvider: "RabbitMQ",
                includeRuntimeMode: false));

        Assert.Contains(services, IsHostedService<TenantOutboxDispatcher>);
        Assert.Contains(services, IsHostedService<BackgroundJobWorker>);
        Assert.Contains(services, IsHostedService<RecurringTaskRunner>);
    }

    [Fact]
    public void AddAtlasMigration_WithoutConfiguredMode_DefaultsToNoBackgroundExecutionPlane()
    {
        var services = new ServiceCollection();

        services.AddAtlasMigration(
            CreateCoreConfiguration(
                AtlasRuntimeMode.Worker,
                messagingProvider: "RabbitMQ",
                includeRuntimeMode: false));

        Assert.DoesNotContain(services, service => service.ServiceType == typeof(IHostedService));
        Assert.Contains(services, service => service.ServiceType == typeof(IBackgroundJobClient));
        Assert.Contains(services, service => service.ServiceType == typeof(ITenantDomainEventOutbox));
        Assert.Contains(services, service =>
            service.ServiceType == typeof(IDomainEventPublisher) &&
            service.ImplementationType == typeof(NoOpDomainEventPublisher));
    }

    [Fact]
    public void AddAtlasRuntimeOptions_ReturnsResolvedOptionsAndRegistersOptions()
    {
        var services = new ServiceCollection();
        var configuration = CreateCoreConfiguration(AtlasRuntimeMode.Migration);

        var runtimeOptions = services.AddAtlasRuntimeOptions(configuration, AtlasRuntimeMode.WebApi);

        Assert.Equal(AtlasRuntimeMode.Migration, runtimeOptions.Mode);

        using var provider = services.BuildServiceProvider();
        Assert.Equal(AtlasRuntimeMode.Migration, provider.GetRequiredService<IOptions<AtlasRuntimeModeOptions>>().Value.Mode);
        Assert.Equal("Memory", provider.GetRequiredService<IOptions<AtlasCacheSettingsOptions>>().Value.Provider);
        Assert.Equal("None", provider.GetRequiredService<IOptions<AtlasMessagingOptions>>().Value.Provider);
    }

    [Fact]
    public void AddAtlasIdentity_RegistersHttpIdentityAndAccessor()
    {
        var services = new ServiceCollection();

        services.AddAtlasIdentity();

        Assert.Contains(services, service => service.ServiceType == typeof(IHttpContextAccessor));
        Assert.Contains(services, service => service.ServiceType == typeof(ICurrentIdentity));
    }

    [Fact]
    public void AddAtlasDatabase_RegistersGlobalDbContextWithoutHostedServices()
    {
        var services = new ServiceCollection();
        var configuration = CreateCoreConfiguration(AtlasRuntimeMode.WebApi);

        services.AddLogging();
        services.AddAtlasSnowflakeId(configuration);
        services.AddAtlasIdentity();
        services.AddAtlasDatabase(configuration);

        Assert.Contains(services, service => service.ServiceType == typeof(AtlasGlobalDbContext));
        Assert.DoesNotContain(services, service => service.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddAtlasCache_MemoryProvider_RegistersCacheOnlyRuntime()
    {
        var services = new ServiceCollection();

        services.AddAtlasCache(CreateCoreConfiguration(AtlasRuntimeMode.WebApi));

        Assert.Contains(services, service => service.ServiceType == typeof(ICacheService));
        Assert.Contains(services, service => service.ServiceType == typeof(ICacheProvider));
        Assert.Contains(services, service => service.ServiceType == typeof(IDistributedLockProvider));
        Assert.DoesNotContain(services, service => service.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddAtlasMessagingRuntime_NoneProvider_RegistersNoOpTransportOnly()
    {
        var services = new ServiceCollection();
        var configuration = CreateCoreConfiguration(AtlasRuntimeMode.WebApi);
        var runtimeOptions = AtlasRuntimeModeOptions.FromConfiguration(configuration, AtlasRuntimeMode.WebApi);

        services.AddAtlasMessagingRuntime(configuration, runtimeOptions);

        Assert.Contains(services, service =>
            service.ServiceType == typeof(IDomainEventPublisher) &&
            service.ImplementationType == typeof(NoOpDomainEventPublisher));
        Assert.Contains(services, service =>
            service.ServiceType == typeof(IDomainEventTransport) &&
            service.ImplementationType == typeof(NoOpDomainEventTransport));
        Assert.DoesNotContain(services, service => service.ServiceType == typeof(IHostedService));
    }

    private static bool IsHostedService<THostedService>(ServiceDescriptor descriptor)
        where THostedService : IHostedService
    {
        return descriptor.ServiceType == typeof(IHostedService) &&
               descriptor.ImplementationType == typeof(THostedService);
    }

    private static IConfiguration CreateCoreConfiguration(
        AtlasRuntimeMode runtimeMode,
        string messagingProvider = "None",
        bool includeRuntimeMode = true)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:AtlasGlobal"] = "Server=localhost;Port=3306;Database=atlas_global;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;",
            ["CacheSettings:Provider"] = "Memory",
            ["Messaging:Provider"] = messagingProvider,
            ["Messaging:RabbitMQ:Host"] = "localhost",
            ["Messaging:RabbitMQ:Port"] = "5672",
            ["Messaging:RabbitMQ:VirtualHost"] = "/",
            ["Messaging:RabbitMQ:Username"] = "guest",
            ["Messaging:RabbitMQ:Password"] = "guest"
        };

        if (includeRuntimeMode)
            values["Atlas:Runtime:Mode"] = runtimeMode.ToString();

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
