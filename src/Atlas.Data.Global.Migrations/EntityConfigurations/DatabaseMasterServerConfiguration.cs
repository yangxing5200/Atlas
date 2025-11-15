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
    /// 主数据库服务器配置
    /// </summary>
    public class DatabaseMasterServerConfiguration : IEntityTypeConfiguration<DatabaseMasterServer>
    {
        public void Configure(EntityTypeBuilder<DatabaseMasterServer> builder)
        {
            // 表名
            builder.ToTable("DatabaseMasterServers");

            // 主键
            builder.HasKey(e => e.Id);

            // 唯一索引
            builder.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("UX_DatabaseMasterServers_Code");

            // 属性配置
            builder.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("服务器编码（唯一标识）");

            builder.Property(e => e.NickName)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("服务器昵称");

            builder.Property(e => e.CreatedAt)
                .IsRequired()
                .HasComment("创建时间");

            builder.Property(e => e.UpdatedAt)
                .HasComment("更新时间");
        }
    }
}
