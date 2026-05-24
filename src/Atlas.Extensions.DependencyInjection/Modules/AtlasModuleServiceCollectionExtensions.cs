using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Extensions.DependencyInjection;

public static class AtlasModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasModules(
        this IServiceCollection services,
        IConfiguration configuration,
        params IAtlasModule[] modules)
    {
        return services.AddAtlasModules(configuration, modules.AsEnumerable());
    }

    public static IServiceCollection AddAtlasModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<IAtlasModule> modules)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return AtlasModuleCatalog.Create(modules)
            .AddServices(services, configuration);
    }

    public static IServiceCollection AddAtlasModules(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AtlasModuleRegistrationOptions> configureModules)
    {
        ArgumentNullException.ThrowIfNull(configureModules);

        var moduleOptions = new AtlasModuleRegistrationOptions();
        configureModules(moduleOptions);

        return services.AddAtlasModules(configuration, moduleOptions.BuildModules());
    }
}
