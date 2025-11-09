using Atlas.Infrastructure.Caching.Dependencies;
using Atlas.Infrastructure.Caching.Invalidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Atlas.Data.Abstractions.Caching;

public class CacheSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IDependencyResolver _dependencyResolver;
    private readonly IInvalidationCoordinator _invalidationCoordinator;
    private readonly ILogger<CacheSaveChangesInterceptor> _logger;

    public CacheSaveChangesInterceptor(
        IDependencyResolver dependencyResolver,
        IInvalidationCoordinator invalidationCoordinator,
        ILogger<CacheSaveChangesInterceptor> logger)
    {
        _dependencyResolver = dependencyResolver;
        _invalidationCoordinator = invalidationCoordinator;
        _logger = logger;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null)
        {
            await InvalidateCacheAsync(eventData.Context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task InvalidateCacheAsync(DbContext context, CancellationToken cancellationToken)
    {
        try
        {
            var changes = CaptureEntityChanges(context);
            if (!changes.Any()) return;

            var keysToInvalidate = await _dependencyResolver.ResolveInvalidationKeysAsync(changes, cancellationToken);

            // 防御性检查：处理 null 或空集合
            if (keysToInvalidate == null || !keysToInvalidate.Any())
                return;

            await _invalidationCoordinator.InvalidateAsync(keysToInvalidate, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate cache during save changes");
            throw;
        }
    }

    private List<EntityChangeInfo> CaptureEntityChanges(DbContext context)
    {
        var changes = new List<EntityChangeInfo>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Unchanged || entry.State == EntityState.Detached)
                continue;

            var info = new EntityChangeInfo
            {
                EntityType = entry.Entity.GetType(),
                State = MapState(entry.State),
                Entity = entry.Entity
            };

            // 捕获修改的属性信息
            if (entry.State == EntityState.Modified)
            {
                foreach (var property in entry.Properties)
                {
                    if (property.IsModified)
                    {
                        info.ModifiedProperties.Add(property.Metadata.Name);
                        info.OldValues[property.Metadata.Name] = property.OriginalValue;
                        info.NewValues[property.Metadata.Name] = property.CurrentValue;
                    }
                }

                // 如果没有任何属性被修改，跳过这个实体（没有实际变更）
                if (!info.ModifiedProperties.Any())
                    continue;
            }
            // 新增实体：捕获所有当前值
            else if (entry.State == EntityState.Added)
            {
                foreach (var property in entry.Properties)
                {
                    info.NewValues[property.Metadata.Name] = property.CurrentValue;
                }
            }
            // 删除实体：捕获所有原始值
            else if (entry.State == EntityState.Deleted)
            {
                foreach (var property in entry.Properties)
                {
                    info.OldValues[property.Metadata.Name] = property.OriginalValue;
                }
            }

            changes.Add(info);
        }

        return changes;
    }

    private EntityChangeState MapState(EntityState state) =>
        state switch
        {
            EntityState.Added => EntityChangeState.Added,
            EntityState.Modified => EntityChangeState.Modified,
            EntityState.Deleted => EntityChangeState.Deleted,
            _ => throw new ArgumentException($"Unsupported entity state: {state}", nameof(state))
        };
}