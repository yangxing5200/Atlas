using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Atlas.Core.Services;

namespace Atlas.Data.Common.Interceptors
{
    /// <summary>
    /// 审计拦截器（自动设置CreatedBy, UpdatedBy等字段）
    /// 微软官方推荐方式，用于SaveChanges场景
    /// </summary>
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly ICurrentUserService _currentUserService;

        public AuditInterceptor(ICurrentUserService currentUserService)
        {
            _currentUserService = currentUserService;
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

        private void UpdateAuditFields(DbContext context)
        {
            if (context == null) return;

            var userId = _currentUserService.UserId;
            var now = DateTime.UtcNow;

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Added)
                {
                    // 新增时设置创建信息
                    TrySetProperty(entry, "CreatedAt", now);
                    TrySetProperty(entry, "CreatedBy", userId);
                    TrySetProperty(entry, "Version", 0);
                }
                else if (entry.State == EntityState.Modified)
                {
                    // 修改时设置更新信息
                    TrySetProperty(entry, "UpdatedAt", now);
                    TrySetProperty(entry, "UpdatedBy", userId);

                    // 版本号递增（乐观锁）
                    IncrementVersion(entry);
                }
            }
        }

        /// <summary>
        /// 尝试设置属性值
        /// </summary>
        private static void TrySetProperty(EntityEntry entry, string propertyName, object value)
        {
            try
            {
                var property = entry.Property(propertyName);
                if (property?.Metadata != null)
                {
                    property.CurrentValue = value;
                }
            }
            catch
            {
                // 属性不存在，忽略（不是所有实体都有审计字段）
            }
        }

        /// <summary>
        /// 递增版本号
        /// </summary>
        private static void IncrementVersion(EntityEntry entry)
        {
            try
            {
                var versionProperty = entry.Property("Version");
                if (versionProperty?.Metadata != null)
                {
                    var currentVersion = versionProperty.CurrentValue as int? ?? 0;
                    versionProperty.CurrentValue = currentVersion + 1;
                }
            }
            catch
            {
                // 属性不存在，忽略
            }
        }
    }
}