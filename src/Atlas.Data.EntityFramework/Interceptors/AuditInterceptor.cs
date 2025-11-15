using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Atlas.Core.Services;
using Atlas.Core.Entities;

namespace Atlas.Data.Common.Interceptors
{
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly ICurrentIdentity _currentIdentity;

        public AuditInterceptor(ICurrentIdentity currentIdentity)
        {
            _currentIdentity = currentIdentity;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            UpdateAuditFields(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            UpdateAuditFields(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void UpdateAuditFields(DbContext? context)
        {
            if (context == null) return;

            var userId = _currentIdentity?.UserId;
            var tenantId = _currentIdentity?.TenantId;
            var storeId = _currentIdentity?.StoreId;
            var now = DateTime.UtcNow;

            var hasTenant = tenantId.HasValue;
            var hasStore = storeId.HasValue;

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Added)
                {
                    var entity = entry.Entity;

                    if (entity is IBaseEntity be)
                        be.CreatedAt = now;

                    if (entity is IAuditable au)
                        au.CreatedBy = userId;

                    if (entity is IVersioned ve)
                        ve.Version = 0;

                    if (hasTenant && entity is ITenantEntity te && te.TenantId == 0)
                        te.TenantId = tenantId!.Value;

                    if (hasStore && entity is IStoreEntity se && se.StoreId == 0)
                        se.StoreId = storeId!.Value;
                }
                else if (entry.State == EntityState.Modified)
                {
                    var entity = entry.Entity;

                    if (entity is IBaseEntity be)
                        be.UpdatedAt = now;

                    if (entity is IAuditable au)
                        au.UpdatedBy = userId;

                    if (entity is IVersioned ve)
                        ve.Version++;
                }
            }
        }
    }
}