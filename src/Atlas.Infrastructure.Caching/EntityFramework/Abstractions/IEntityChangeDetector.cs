// EntityFramework/Abstractions/IEntityChangeDetector.cs
using Microsoft.EntityFrameworkCore;
using Atlas.Infrastructure.Caching.EntityFramework.ChangeTracking;

namespace Atlas.Infrastructure.Caching.EntityFramework.Abstractions
{
    public interface IEntityChangeDetector
    {
        EntityChangeSet DetectChanges(DbContext context);
    }
}