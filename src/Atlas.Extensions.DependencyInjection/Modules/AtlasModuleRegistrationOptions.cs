using System.Reflection;

namespace Atlas.Extensions.DependencyInjection;

public sealed class AtlasModuleRegistrationOptions
{
    private readonly List<IAtlasModule> _modules = new();
    private readonly List<Assembly> _moduleDiscoveryAssemblies = new();

    public IReadOnlyCollection<IAtlasModule> Modules => _modules;

    public IReadOnlyCollection<Assembly> ModuleDiscoveryAssemblies => _moduleDiscoveryAssemblies;

    public AtlasModuleRegistrationOptions AddModule(IAtlasModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        _modules.Add(module);
        return this;
    }

    public AtlasModuleRegistrationOptions AddModule<TModule>()
        where TModule : IAtlasModule, new()
    {
        return AddModule(new TModule());
    }

    public AtlasModuleRegistrationOptions AddModuleAssembly(Assembly assembly)
    {
        return AddModule(new AtlasAssemblyModule(assembly));
    }

    public AtlasModuleRegistrationOptions AddModuleAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            AddModuleAssembly(assembly);
        }

        return this;
    }

    public AtlasModuleRegistrationOptions AddModulesFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _moduleDiscoveryAssemblies.Add(assembly);
        return this;
    }

    public AtlasModuleRegistrationOptions AddModulesFromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            AddModulesFromAssembly(assembly);
        }

        return this;
    }

    internal IReadOnlyCollection<IAtlasModule> BuildModules()
    {
        return _modules
            .Concat(AtlasModuleDiscovery.FromAssemblies(_moduleDiscoveryAssemblies))
            .ToArray();
    }
}
