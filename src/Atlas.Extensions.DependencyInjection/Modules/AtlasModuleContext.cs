using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Extensions.DependencyInjection;

public sealed class AtlasModuleContext
{
    public AtlasModuleContext(
        IServiceCollection services,
        IConfiguration configuration,
        IAtlasModule module)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Module = module ?? throw new ArgumentNullException(nameof(module));
    }

    public IServiceCollection Services { get; }

    public IConfiguration Configuration { get; }

    public IAtlasModule Module { get; }

    public string ModuleName => Module.Name;

    public Assembly ModuleAssembly => Module.Assembly;
}
