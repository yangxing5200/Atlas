using System.Reflection;

namespace Atlas.Data.Tenant.Context;

public interface IAtlasTenantEntityConfigurationAssemblyProvider
{
    IReadOnlyCollection<Assembly> Assemblies { get; }
}

public sealed class AtlasTenantEntityConfigurationAssemblyProvider : IAtlasTenantEntityConfigurationAssemblyProvider
{
    public AtlasTenantEntityConfigurationAssemblyProvider(IEnumerable<Assembly>? assemblies)
    {
        Assemblies = (assemblies ?? Array.Empty<Assembly>())
            .Where(assembly => assembly is not null)
            .Distinct()
            .ToArray();
    }

    public IReadOnlyCollection<Assembly> Assemblies { get; }
}
