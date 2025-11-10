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
            if (!changes.Any())
            {
                _logger.LogDebug("No entity changes detected, skipping cache invalidation");
                return;
            }

            _logger.LogDebug("Captured {Count} entity changes", changes.Count);

            // ✅ 解析需要失效的标签（而不是键）
            var tagsToInvalidate = await _dependencyResolver.ResolveInvalidationTagsAsync(changes, cancellationToken);

            if (tagsToInvalidate == null || !tagsToInvalidate.Any())
            {
                _logger.LogDebug("No cache tags to invalidate");
                return;
            }

            // ✅ 使用标签失效方法（而不是直接删除键）
            await _invalidationCoordinator.InvalidateByTagsAsync(tagsToInvalidate, cancellationToken);

            _logger.LogInformation("Cache invalidation completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate cache during save changes");
            // 注意：这里可以选择是否抛出异常
            // 如果抛出，会导致整个 SaveChanges 失败
            // 如果不抛出，数据会保存但缓存可能不一致
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
                {
                    _logger.LogTrace("Entity {EntityType} marked as Modified but no properties changed",
                        entry.Entity.GetType().Name);
                    continue;
                }

                _logger.LogDebug("Entity {EntityType} modified properties: {Properties}",
                    entry.Entity.GetType().Name,
                    string.Join(", ", info.ModifiedProperties));
            }
            // 新增实体：捕获所有当前值
            else if (entry.State == EntityState.Added)
            {
                foreach (var property in entry.Properties)
                {
                    info.NewValues[property.Metadata.Name] = property.CurrentValue;
                }

                _logger.LogDebug("Entity {EntityType} added", entry.Entity.GetType().Name);
            }
            // 删除实体：捕获所有原始值
            else if (entry.State == EntityState.Deleted)
            {
                foreach (var property in entry.Properties)
                {
                    info.OldValues[property.Metadata.Name] = property.OriginalValue;
                }

                _logger.LogDebug("Entity {EntityType} deleted", entry.Entity.GetType().Name);
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