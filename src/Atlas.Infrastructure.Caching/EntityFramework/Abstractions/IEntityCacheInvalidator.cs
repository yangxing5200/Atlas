// EntityFramework/Abstractions/IEntityCacheInvalidator.cs
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.EntityFramework.ChangeTracking;

namespace Atlas.Infrastructure.Caching.EntityFramework.Abstractions
{
    public interface IEntityCacheInvalidator
    {
        Task InvalidateAsync(EntityChangeSet changeSet, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 标记接口，表示实体可被缓存
    /// </summary>
    public interface ICacheableEntity
    {
        string[] GetCacheTags();
    }
}