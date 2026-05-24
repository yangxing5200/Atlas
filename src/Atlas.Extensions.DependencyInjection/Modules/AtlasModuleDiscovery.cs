using System.Reflection;

namespace Atlas.Extensions.DependencyInjection;

public static class AtlasModuleDiscovery
{
    public static IReadOnlyCollection<IAtlasModule> FromAssemblies(params Assembly[] assemblies)
    {
        return FromAssemblies(assemblies.AsEnumerable());
    }

    public static IReadOnlyCollection<IAtlasModule> FromAssemblies(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var modules = new List<IAtlasModule>();

        foreach (var assembly in assemblies.Where(assembly => assembly is not null).Distinct())
        {
            foreach (var type in GetModuleTypes(assembly))
            {
                var constructor = type.GetConstructor(Type.EmptyTypes);
                if (constructor is null)
                {
                    throw new InvalidOperationException(
                        $"Atlas module '{type.FullName}' must expose a public parameterless constructor.");
                }

                modules.Add((IAtlasModule)constructor.Invoke(Array.Empty<object>()));
            }
        }

        return modules;
    }

    private static IEnumerable<Type> GetModuleTypes(Assembly assembly)
    {
        return assembly.DefinedTypes
            .Where(type =>
                (type.IsPublic || type.IsNestedPublic) &&
                !type.IsAbstract &&
                !type.IsInterface &&
                typeof(IAtlasModule).IsAssignableFrom(type.AsType()))
            .Select(type => type.AsType());
    }
}
