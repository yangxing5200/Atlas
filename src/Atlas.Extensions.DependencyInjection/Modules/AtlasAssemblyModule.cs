using System.Reflection;

namespace Atlas.Extensions.DependencyInjection;

public sealed class AtlasAssemblyModule : AtlasModule
{
    public AtlasAssemblyModule(Assembly assembly)
    {
        Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
    }

    public override string Name => Assembly.GetName().Name ?? Assembly.FullName ?? nameof(AtlasAssemblyModule);

    public override Assembly Assembly { get; }
}
