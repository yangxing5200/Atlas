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
