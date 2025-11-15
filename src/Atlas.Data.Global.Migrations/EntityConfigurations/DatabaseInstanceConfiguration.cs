using Atlas.Models.Global.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Global.Migrations.EntityConfigurations
{
    /// <summary>
    /// 数据库实例配置
    /// </summary>
    public class DatabaseInstanceConfiguration : IEntityTypeConfiguration<DatabaseInstance>
    {
        public void Configure(EntityTypeBuilder<DatabaseInstance> builder)
        {
            // 表名
            builder.ToTable("DatabaseInstances");

            // 主键
            builder.HasKey(e => e.Id);

            // 索引
            builder.HasIndex(e => e.MasterServerCode)
                .HasDatabaseName("IX_DatabaseInstances_MasterServerCode");

            builder.HasIndex(e => e.DbType)
                .HasDatabaseName("IX_DatabaseInstances_DbType");

            builder.HasIndex(e => e.Region)
                .HasDatabaseName("IX_DatabaseInstances_Region");

            // 属性配置
            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("实例名称");

            builder.Property(e => e.DbType)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("数据库类型：SqlServer, MySQL, PostgreSQL");

            builder.Property(e => e.MasterServerCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("主数据库Server编码");

            builder.Property(e => e.DbName)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("数据库名称");

            builder.Property(e => e.Version)
                .HasMaxLength(50)
                .HasComment("数据库版本");

            builder.Property(e => e.Region)
                .HasMaxLength(50)
                .HasComment("所属区域：华东、华北、华南等");

            builder.Property(e => e.ConnectionString)
                .HasMaxLength(500)
                .HasComment("数据库连接串（可选，优先级高于ServerConfig）");

            builder.Property(e => e.CreatedAt)
                .IsRequired()
                .HasComment("创建时间");

            builder.Property(e => e.UpdatedAt)
                .HasComment("更新时间");

            // 关系配置
            builder.HasMany(e => e.Tenants)
                .WithOne(t => t.DatabaseInstance)
                .HasForeignKey(t => t.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
