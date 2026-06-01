using System.Reflection;

namespace Atlas.Extensions.DependencyInjection;

public interface IAtlasModule
{
    string Name { get; }

    Assembly Assembly { get; }

    IReadOnlyCollection<Assembly> ControllerAssemblies { get; }

    IReadOnlyCollection<Assembly> ConsumerAssemblies { get; }

    IReadOnlyCollection<Assembly> AutoMapperAssemblies { get; }

    void AddServices(AtlasModuleContext context);
}
