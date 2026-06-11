using System.Reflection;
using Atlas.Core.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Extensions.DependencyInjection;

internal sealed class AtlasModuleCatalog
{
    private AtlasModuleCatalog(IReadOnlyList<IAtlasModule> modules)
    {
        Modules = modules;
        ControllerAssemblies = GetDistinctAssemblies(modules, module => module.ControllerAssemblies);
        ConsumerAssemblies = GetDistinctAssemblies(modules, module => module.ConsumerAssemblies);
        AutoMapperAssemblies = GetDistinctAssemblies(modules, module => module.AutoMapperAssemblies);
        EntityConfigurationAssemblies = GetDistinctAssemblies(modules, module => module.EntityConfigurationAssemblies);
        AuthorizationCatalog = BuildAuthorizationCatalog(modules);
    }

    public static AtlasModuleCatalog Empty { get; } = new(Array.Empty<IAtlasModule>());

    public IReadOnlyList<IAtlasModule> Modules { get; }

    public IReadOnlyCollection<Assembly> ControllerAssemblies { get; }

    public IReadOnlyCollection<Assembly> ConsumerAssemblies { get; }

    public IReadOnlyCollection<Assembly> AutoMapperAssemblies { get; }

    public IReadOnlyCollection<Assembly> EntityConfigurationAssemblies { get; }

    public AtlasAuthorizationCatalog AuthorizationCatalog { get; }

    public static AtlasModuleCatalog Create(IEnumerable<IAtlasModule>? modules)
    {
        if (modules is null)
            return Empty;

        var distinctModules = modules
            .Where(module => module is not null)
            .GroupBy(module => module.GetType())
            .Select(group => group.First())
            .ToArray();

        return distinctModules.Length == 0
            ? Empty
            : new AtlasModuleCatalog(distinctModules);
    }

    public static AtlasModuleCatalog CreateWithBuiltInModules(IEnumerable<IAtlasModule>? modules)
    {
        return Create(new IAtlasModule[] { new AtlasBuiltInModule() }
            .Concat(modules ?? Array.Empty<IAtlasModule>()));
    }

    public IServiceCollection AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(AuthorizationCatalog);
        services.TryAddSingleton<IAtlasAuthorizationCatalog>(AuthorizationCatalog);

        foreach (var module in Modules)
        {
            module.AddServices(new AtlasModuleContext(services, configuration, module));
        }

        return services;
    }

    private static AtlasAuthorizationCatalog BuildAuthorizationCatalog(IEnumerable<IAtlasModule> modules)
    {
        var catalogBuilder = new AtlasAuthorizationCatalogBuilder("Atlas.ModuleCatalog");

        foreach (var module in modules)
        {
            var moduleBuilder = new AtlasAuthorizationCatalogBuilder(module.Name);
            module.ConfigureAuthorization(moduleBuilder);
            catalogBuilder.Merge(moduleBuilder.Build(validateReferences: false));
        }

        return catalogBuilder.Build();
    }

    private static IReadOnlyCollection<Assembly> GetDistinctAssemblies(
        IEnumerable<IAtlasModule> modules,
        Func<IAtlasModule, IReadOnlyCollection<Assembly>> selector)
    {
        var assemblies = new List<Assembly>();
        var seen = new HashSet<Assembly>();

        foreach (var module in modules)
        {
            foreach (var assembly in selector(module))
            {
                if (assembly is not null && seen.Add(assembly))
                    assemblies.Add(assembly);
            }
        }

        return assemblies;
    }
}
