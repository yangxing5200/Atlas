// EntityFramework/ChangeTracking/EntityChangeDetector.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Atlas.Infrastructure.Caching.EntityFramework.Abstractions;
using Atlas.Infrastructure.Caching.EntityFramework.Attributes;

namespace Atlas.Infrastructure.Caching.EntityFramework.ChangeTracking
{
    public class EntityChangeDetector : IEntityChangeDetector
    {
        public EntityChangeSet DetectChanges(DbContext context)
        {
            var changes = new List<EntityChange>();

            foreach (var entry in context.ChangeTracker.Entries())
            {
                // Skip entities without cache attributes
                if (!HasCacheAttribute(entry.Entity.GetType()))
                    continue;

                var changeType = entry.State switch
                {
                    EntityState.Added => ChangeType.Added,
                    EntityState.Modified => ChangeType.Modified,
                    EntityState.Deleted => ChangeType.Deleted,
                    _ => ChangeType.None
                };

                if (changeType == ChangeType.None)
                    continue;

                var entityChange = new EntityChange
                {
                    EntityType = entry.Entity.GetType(),
                    EntityName = entry.Entity.GetType().Name,
                    ChangeType = changeType,
                    Entity = entry.Entity,
                    ModifiedProperties = GetModifiedProperties(entry)
                };

                // Extract primary key
                var keyValues = entry.Properties
                    .Where(p => p.Metadata.IsPrimaryKey())
                    .Select(p => p.CurrentValue)
                    .ToArray();

                entityChange.EntityId = keyValues.Length == 1
                    ? keyValues[0]?.ToString()
                    : string.Join("_", keyValues);

                changes.Add(entityChange);
            }

            return new EntityChangeSet
            {
                Changes = changes,
                Timestamp = DateTime.UtcNow
            };
        }

        private bool HasCacheAttribute(Type entityType)
        {
            return entityType.GetCustomAttributes(typeof(CacheableAttribute), true).Any()
                || entityType.GetCustomAttributes(typeof(CacheInvalidateAttribute), true).Any();
        }

        private HashSet<string> GetModifiedProperties(EntityEntry entry)
        {
            if (entry.State != EntityState.Modified)
                return new HashSet<string>();

            return entry.Properties
                .Where(p => p.IsModified)
                .Select(p => p.Metadata.Name)
                .ToHashSet();
        }
    }
}