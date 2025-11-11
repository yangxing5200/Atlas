// EntityFramework/Interceptors/CacheInvalidationInterceptor.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.EntityFramework.Abstractions;
using Atlas.Infrastructure.Caching.EntityFramework.ChangeTracking;

namespace Atlas.Infrastructure.Caching.EntityFramework.Interceptors
{
    public class CacheInvalidationInterceptor : SaveChangesInterceptor
    {
        private readonly IEntityChangeDetector _changeDetector;
        private readonly IEntityCacheInvalidator _cacheInvalidator;

        public CacheInvalidationInterceptor(
            IEntityChangeDetector changeDetector,
            IEntityCacheInvalidator cacheInvalidator)
        {
            _changeDetector = changeDetector ?? throw new ArgumentNullException(nameof(changeDetector));
            _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context != null)
            {
                var changeSet = _changeDetector.DetectChanges(eventData.Context);

                // Store in context for post-save processing
                eventData.Context.ChangeTracker.Tracked += (sender, args) => { };
                eventData.Context.GetType().GetProperty("__CacheChangeSet")?.SetValue(eventData.Context, changeSet);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context != null)
            {
                var changeSetProperty = eventData.Context.GetType().GetProperty("__CacheChangeSet");
                if (changeSetProperty?.GetValue(eventData.Context) is EntityChangeSet changeSet)
                {
                    await _cacheInvalidator.InvalidateAsync(changeSet, cancellationToken);
                }
            }

            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            if (eventData.Context != null)
            {
                var changeSet = _changeDetector.DetectChanges(eventData.Context);
                eventData.Context.GetType().GetProperty("__CacheChangeSet")?.SetValue(eventData.Context, changeSet);
            }

            return base.SavingChanges(eventData, result);
        }

        public override int SavedChanges(
            SaveChangesCompletedEventData eventData,
            int result)
        {
            if (eventData.Context != null)
            {
                var changeSetProperty = eventData.Context.GetType().GetProperty("__CacheChangeSet");
                if (changeSetProperty?.GetValue(eventData.Context) is EntityChangeSet changeSet)
                {
                    _cacheInvalidator.InvalidateAsync(changeSet).GetAwaiter().GetResult();
                }
            }

            return base.SavedChanges(eventData, result);
        }
    }
}