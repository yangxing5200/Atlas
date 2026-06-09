using Atlas.Core.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;
namespace Atlas.Data.Common.Extensions
{
    public enum ForeignKeyConstraintPolicy
    {
        Preserve = 0,
        SuppressConstraintNames = 1
    }

    /// <summary>
    /// EF Core ModelBuilder 扩展方法
    /// </summary>
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// Applies the database foreign key constraint policy for the model.
        /// </summary>
        public static ModelBuilder ApplyForeignKeyConstraintPolicy(
            this ModelBuilder modelBuilder,
            ForeignKeyConstraintPolicy policy)
        {
            return policy switch
            {
                ForeignKeyConstraintPolicy.Preserve => modelBuilder,
                ForeignKeyConstraintPolicy.SuppressConstraintNames => modelBuilder.RemoveAllForeignKeyConstraints(),
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported foreign key constraint policy.")
            };
        }

        /// <summary>
        /// 抑制所有外键约束名（保留导航属性和索引），仅用于兼容旧模型或测试场景。
        /// </summary>
        public static ModelBuilder RemoveAllForeignKeyConstraints(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // 获取所有外键并转为列表（避免迭代时修改集合）
                var foreignKeys = entityType.GetForeignKeys().ToList();

                foreach (var foreignKey in foreignKeys)
                {
                    // 强制设置约束名为 null，覆盖 Configuration 中的设置
                    foreignKey.SetConstraintName(null);

                    // 或者使用更底层的 Metadata API
                    foreignKey.SetAnnotation("Relational:Name", null);
                }
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

        /// <summary>
        /// 自动配置ID生成策略
        /// - 实现了 ISnowflakeId 接口的实体：不使用数据库生成（由应用层生成雪花ID）
        /// - 其他实体：使用数据库自增ID
        /// </summary>
        public static ModelBuilder ConfigureIdGenerationStrategy(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;

                // 跳过非实体类型
                if (!typeof(IBaseEntity).IsAssignableFrom(clrType))
                {
                    continue;
                }

                var idProperty = entityType.FindProperty(nameof(IBaseEntity.Id));
                if (idProperty == null)
                {
                    continue;
                }

                // 检查是否实现了 ISnowflakeId 接口
                if (typeof(ISnowflakeId).IsAssignableFrom(clrType))
                {
                    // 雪花ID：不使用数据库生成
                    modelBuilder.Entity(clrType)
                        .Property(nameof(IBaseEntity.Id))
                        .ValueGeneratedNever();
                }
                else
                {
                    // 默认：使用数据库自增ID
                    modelBuilder.Entity(clrType)
                        .Property(nameof(IBaseEntity.Id))
                        .ValueGeneratedOnAdd();
                }
            }
            return modelBuilder;
        }
    }
}
