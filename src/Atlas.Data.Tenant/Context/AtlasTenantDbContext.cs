using Atlas.Core.Context;
using Atlas.Core.Entities;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Common;
using Atlas.Data.Common.Extensions;
using Atlas.Models.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Internal;
using System.Reflection;

namespace Atlas.Data.Tenant.Context
{
    /// <summary>
    /// Tenant数据库上下文
    /// </summary>
    public class AtlasTenantDbContext : DbContextBase
    {
        public AtlasTenantDbContext(
            DbContextOptions<AtlasTenantDbContext> options)
            : base(options)
        {
        }
        internal DbSet<TEntity> GetDbSet<TEntity>() where TEntity : class
        {
            return Set<TEntity>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // 应用所有实体配置
            var migrationsAssembly = Assembly.Load("Atlas.Data.Tenant.Migrations");
            modelBuilder.ApplyConfigurationsFromAssembly(migrationsAssembly);

            // 配置ID生成策略
            modelBuilder.ConfigureIdGenerationStrategy();
       
            // 确保外键字段有索引
            modelBuilder.EnsureForeignKeyIndexes();

            // 应用软删除过滤器
            modelBuilder.ApplySoftDeleteFilter();

            // 移除所有外键约束 
            modelBuilder.RemoveAllForeignKeyConstraints();
        }
    }
}