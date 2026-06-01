using System.Reflection;
using Atlas.Core.Authorization;

namespace Atlas.Extensions.DependencyInjection;

public abstract class AtlasModule : IAtlasModule
{
    public virtual string Name => Assembly.GetName().Name ?? GetType().Name;

    public virtual Assembly Assembly => GetType().Assembly;

    public virtual IReadOnlyCollection<Assembly> ControllerAssemblies => new[] { Assembly };

    public virtual IReadOnlyCollection<Assembly> ConsumerAssemblies => new[] { Assembly };

    public virtual IReadOnlyCollection<Assembly> AutoMapperAssemblies => new[] { Assembly };

    public virtual void AddServices(AtlasModuleContext context)
    {
    }

    public virtual void ConfigureAuthorization(AtlasAuthorizationCatalogBuilder builder)
    {
    }
}
