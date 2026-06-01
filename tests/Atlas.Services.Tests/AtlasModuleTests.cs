using Atlas.Core.Authorization;
using Atlas.Core.Enums;
using Atlas.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Services.Tests;

public sealed class AtlasModuleTests
{
    [Fact]
    public void AtlasModule_UsesImplementingAssemblyByDefault()
    {
        var module = new TestModule();
        var assembly = typeof(TestModule).Assembly;

        Assert.Equal(assembly, module.Assembly);
        Assert.Contains(assembly, module.ControllerAssemblies);
        Assert.Contains(assembly, module.ConsumerAssemblies);
        Assert.Contains(assembly, module.AutoMapperAssemblies);
    }

    [Fact]
    public void AddAtlasModules_RegistersModuleServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var module = new TestModule();

        services.AddAtlasModules(configuration, module);

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IModuleMarker));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Same(module, module.Context?.Module);
        Assert.Same(services, module.Context?.Services);
        Assert.Same(configuration, module.Context?.Configuration);
    }

    [Fact]
    public void AtlasAssemblyModule_DeclaresTheProvidedAssembly()
    {
        var assembly = typeof(AtlasModuleTests).Assembly;
        var module = new AtlasAssemblyModule(assembly);

        Assert.Equal(assembly, module.Assembly);
        Assert.Contains(assembly, module.ControllerAssemblies);
        Assert.Contains(assembly, module.ConsumerAssemblies);
        Assert.Contains(assembly, module.AutoMapperAssemblies);
    }

    [Fact]
    public void AtlasModuleDiscovery_CreatesPublicModulesFromAssemblies()
    {
        var modules = AtlasModuleDiscovery.FromAssemblies(typeof(DiscoveredAtlasModule).Assembly);

        Assert.Contains(modules, module => module.GetType() == typeof(DiscoveredAtlasModule));
    }

    [Fact]
    public void AddAtlasModules_RegistersDiscoveredModules()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddAtlasModules(
            configuration,
            modules => modules.AddModulesFromAssembly(typeof(DiscoveredAtlasModule).Assembly));

        Assert.Contains(services, service => service.ServiceType == typeof(IDiscoveredModuleMarker));
    }

    [Fact]
    public void AddAtlasModules_RegistersAuthorizationCatalog()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddAtlasModules(configuration, new AuthorizationTestModule());

        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IAtlasAuthorizationCatalog>();

        Assert.True(catalog.Capabilities.ContainsKey("test.capability"));
        Assert.True(catalog.Permissions.ContainsKey("test.permission.read"));
        Assert.True(catalog.MenuItems.ContainsKey("test.menu"));
        Assert.True(catalog.DataResources.ContainsKey("test.resource"));
    }

    [Fact]
    public void AddAtlasModules_Throws_WhenMenuReferencesMissingPermission()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddAtlasModules(configuration, new InvalidAuthorizationModule()));

        Assert.Contains("missing permission", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private interface IModuleMarker
    {
    }

    private sealed class ModuleMarker : IModuleMarker
    {
    }

    private sealed class TestModule : AtlasModule
    {
        public AtlasModuleContext? Context { get; private set; }

        public override void AddServices(AtlasModuleContext context)
        {
            Context = context;
            context.Services.AddSingleton<IModuleMarker, ModuleMarker>();
        }
    }

    private sealed class AuthorizationTestModule : AtlasModule
    {
        public override void ConfigureAuthorization(AtlasAuthorizationCatalogBuilder builder)
        {
            builder
                .AddCapability("test.capability", "Test capability", "Test")
                .AddPermission(
                    "test.permission.read",
                    "Read test permission",
                    "test.capability",
                    "Test",
                    PermissionScope.Tenant,
                    resource: "test.resource",
                    action: "read")
                .AddMenuItem(
                    "test.menu",
                    "Test menu",
                    "/test",
                    visibleWhen: AtlasAuthorizationCondition.RequirePermission("test.permission.read"))
                .AddDataResource(
                    "test.resource",
                    "Test resource",
                    supportedScopes: new[] { AtlasDataScopeType.AllTenant });
        }
    }

    private sealed class InvalidAuthorizationModule : AtlasModule
    {
        public override void ConfigureAuthorization(AtlasAuthorizationCatalogBuilder builder)
        {
            builder.AddMenuItem(
                "invalid.menu",
                "Invalid menu",
                "/invalid",
                visibleWhen: AtlasAuthorizationCondition.RequirePermission("missing.permission"));
        }
    }
}

public interface IDiscoveredModuleMarker
{
}

public sealed class DiscoveredModuleMarker : IDiscoveredModuleMarker
{
}

public sealed class DiscoveredAtlasModule : AtlasModule
{
    public override void AddServices(AtlasModuleContext context)
    {
        context.Services.AddSingleton<IDiscoveredModuleMarker, DiscoveredModuleMarker>();
    }
}
