// EntityFramework/Invalidation/EntityCacheInvalidator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.EntityFramework.Abstractions;
using Atlas.Infrastructure.Caching.EntityFramework.ChangeTracking;
using Atlas.Infrastructure.Caching.Tags.Conventions;

namespace Atlas.Infrastructure.Caching.EntityFramework.Invalidation
{
    public class EntityCacheInvalidator : IEntityCacheInvalidator
    {
        private readonly ICacheInvalidator _cacheInvalidator;
        private readonly EntityTagResolver _tagResolver;

        public EntityCacheInvalidator(
            ICacheInvalidator cacheInvalidator,
            EntityTagResolver tagResolver)
        {
            _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
            _tagResolver = tagResolver ?? throw new ArgumentNullException(nameof(tagResolver));
        }

        public async Task InvalidateAsync(EntityChangeSet changeSet, CancellationToken cancellationToken = default)
        {
            var allTags = new HashSet<string>();

            foreach (var change in changeSet.Changes)
            {
                var tags = _tagResolver.ResolveTags(change);
                foreach (var tag in tags)
                {
                    allTags.Add(tag);
                }
            }

            if (allTags.Any())
            {
                await _cacheInvalidator.InvalidateByTagsAsync(allTags, cancellationToken);
            }
        }
    }

    public class EntityTagResolver
    {
        public IEnumerable<string> ResolveTags(EntityChange change)
        {
            var tags = new List<string>();

            // Entity-level tag
            tags.Add(TagNamingConvention.Entity(change.EntityName));

            // Entity ID tag
            if (!string.IsNullOrEmpty(change.EntityId))
            {
                tags.Add(TagNamingConvention.EntityId(change.EntityName, change.EntityId));
            }

            // TODO: Add custom attribute-based tags
            // TODO: Add relationship tags

            return tags;
        }
    }
}