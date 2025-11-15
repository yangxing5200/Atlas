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
    /// 只读数据库服务器配置
    /// </summary>
    public class DatabaseReadonlyServerConfiguration : IEntityTypeConfiguration<DatabaseReadonlyServer>
    {
        public void Configure(EntityTypeBuilder<DatabaseReadonlyServer> builder)
        {
            // 表名
            builder.ToTable("DatabaseReadonlyServers");

            // 主键
            builder.HasKey(e => e.Id);

            // 唯一索引
            builder.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("UX_DatabaseReadonlyServers_Code");

            // 普通索引
            builder.HasIndex(e => e.MasterServerCode)
                .HasDatabaseName("IX_DatabaseReadonlyServers_MasterServerCode");

            builder.HasIndex(e => new { e.IsReport, e.MasterServerCode })
                .HasDatabaseName("IX_DatabaseReadonlyServers_IsReport_MasterServerCode");

            builder.HasIndex(e => new { e.IsPublic, e.MasterServerCode })
                .HasDatabaseName("IX_DatabaseReadonlyServers_IsPublic_MasterServerCode");

            // 属性配置
            builder.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("只读服务器编码（唯一标识）");

            builder.Property(e => e.NickName)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("只读服务器昵称");

            builder.Property(e => e.MasterServerCode)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("所属主数据库Server编码");

            builder.Property(e => e.IsReport)
                .IsRequired()
                .HasDefaultValue(false)
                .HasComment("是否是报表只读库");

            builder.Property(e => e.IsPublic)
                .IsRequired()
                .HasDefaultValue(false)
                .HasComment("是否公开给周边服务访问");

            builder.Property(e => e.CreatedAt)
                .IsRequired()
                .HasComment("创建时间");

            builder.Property(e => e.UpdatedAt)
                .HasComment("更新时间");
        }
    }
}
