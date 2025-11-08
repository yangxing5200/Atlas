using Atlas.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;
namespace Atlas.Data.Common.Extensions
{
    /// <summary>
    /// EF Core ModelBuilder 扩展方法
    /// </summary>
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// 移除所有外键约束（保留导航属性和索引）
        /// </summary>
        public static ModelBuilder RemoveAllForeignKeyConstraints(this ModelBuilder modelBuilder)
        {
            foreach (var relationship in modelBuilder.Model
                .GetEntityTypes()
                .SelectMany(e => e.GetForeignKeys()))
            {
                // 不在数据库中创建外键约束
                relationship.SetConstraintName(null);

                // 设置删除行为为客户端处理
                relationship.DeleteBehavior = DeleteBehavior.ClientSetNull;
            }

            return modelBuilder;
        }

        /// <summary>
        /// 为所有外键字段创建索引（提升查询性能）
        /// </summary>
        public static ModelBuilder EnsureForeignKeyIndexes(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties()
                    .Where(p => p.Name.EndsWith("Id") && p.Name != "Id"))
                {
                    // 检查是否已存在索引
                    var existingIndex = entityType.GetIndexes()
                        .FirstOrDefault(i => i.Properties.Count == 1
                            && i.Properties[0].Name == property.Name);

                    if (existingIndex == null)
                    {
                        entityType.AddIndex(property);
                    }
                }
            }

            return modelBuilder;
        }

        /// <summary>
        /// 配置软删除全局查询过滤器
        /// </summary>
        public static ModelBuilder ApplySoftDeleteFilter(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // 检查实体是否继承自支持软删除的基类
                if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
                {
                    var method = typeof(ModelBuilderExtensions)
                        .GetMethod(nameof(SetSoftDeleteFilter),
                            BindingFlags.NonPublic | BindingFlags.Static)?
                        .MakeGenericMethod(entityType.ClrType);

                    method?.Invoke(null, new object[] { modelBuilder });
                }
            }

            return modelBuilder;
        }

        private static void SetSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
            where TEntity : class, ISoftDelete
        {
            modelBuilder.Entity<TEntity>()
                .HasQueryFilter(e => !e.IsDeleted);
        }

        /// <summary>
        /// 配置租户隔离全局查询过滤器
        /// </summary>
        public static ModelBuilder ApplyTenantFilter(this ModelBuilder modelBuilder,
            Func<string?> getTenantId)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var property = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
                    var tenantId = Expression.Constant(getTenantId());
                    var filter = Expression.Lambda(
                        Expression.Equal(property, tenantId),
                        parameter);

                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
                }
            }

            return modelBuilder;
        }

        /// <summary>
        /// 批量应用 IEntityTypeConfiguration
        /// </summary>
        public static ModelBuilder ApplyConfigurationsFromNamespace(
            this ModelBuilder modelBuilder,
            Assembly assembly,
            string namespacePrefix)
        {
            var types = assembly.GetTypes()
                .Where(t => t.Namespace?.StartsWith(namespacePrefix) == true
                    && t.GetInterfaces().Any(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)));

            foreach (var type in types)
            {
                dynamic? configurationInstance = Activator.CreateInstance(type);
                if (configurationInstance != null)
                {
                    modelBuilder.ApplyConfiguration(configurationInstance);
                }
            }

            return modelBuilder;
        }
    }
}