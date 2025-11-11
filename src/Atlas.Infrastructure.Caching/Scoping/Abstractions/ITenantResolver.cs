// Scoping/Abstractions/ITenantResolver.cs
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Caching.Scoping.Abstractions
{
    public interface ITenantResolver
    {
        Task<string?> ResolveTenantIdAsync();
    }
}