using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Global.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v01_init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DatabaseInstances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "实例名称")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DbType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "数据库类型：SqlServer, MySQL, PostgreSQL")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MasterServerCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "主数据库Server编码")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DbName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "数据库名称")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "数据库版本")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Region = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "所属区域：华东、华北、华南等")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConnectionString = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, comment: "数据库连接串（可选，优先级高于ServerConfig）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, comment: "创建时间"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, comment: "更新时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseInstances", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DatabaseMasterServers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "服务器编码（唯一标识）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NickName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "服务器昵称")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, comment: "创建时间"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, comment: "更新时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseMasterServers", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DatabaseReadonlyServers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "只读服务器编码（唯一标识）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NickName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "只读服务器昵称")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MasterServerCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "所属主数据库Server编码")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsReport = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "是否是报表只读库"),
                    IsPublic = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "是否公开给周边服务访问"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, comment: "创建时间"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, comment: "更新时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseReadonlyServers", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DatabaseServerConfigs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ServerCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "服务器编码（关联MasterServer或ReadonlyServer）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NetworkEnvCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "default", comment: "网络环境编码：default, classic, vpc")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DbType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "数据库类型：SqlServer, MySQL, PostgreSQL")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConnString = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, comment: "数据库连接串")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, comment: "创建时间"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, comment: "更新时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseServerConfigs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, comment: "公司名称")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BrandName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true, comment: "品牌名称")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, comment: "公司地址")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhoneNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "公司电话")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContactName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "联系人姓名")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContactPhoneNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "联系人手机号")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContactEmail = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, comment: "联系人邮箱")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Domain = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "公司代码（租户唯一标识，用于登录）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "租户类型：Enterprise, Individual")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Province = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true, comment: "省份")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    City = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "城市")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true, comment: "租户类别：试用、Mobile等")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "租户状态：Active, Inactive, Suspended")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BusinessType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, comment: "连锁类型：Single, Chain, Franchise")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DatabaseInstanceId = table.Column<long>(type: "bigint", nullable: false, comment: "关联的数据库实例ID"),
                    OfficeCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "诊所/门店数量"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, comment: "创建时间"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, comment: "更新时间"),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "创建人ID"),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true, comment: "更新人ID"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "是否已删除（软删除）"),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, comment: "删除时间"),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true, comment: "删除人ID")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseInstances_DbType",
                table: "DatabaseInstances",
                column: "DbType");

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseInstances_MasterServerCode",
                table: "DatabaseInstances",
                column: "MasterServerCode");

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseInstances_Region",
                table: "DatabaseInstances",
                column: "Region");

            migrationBuilder.CreateIndex(
                name: "UX_DatabaseMasterServers_Code",
                table: "DatabaseMasterServers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseReadonlyServers_IsPublic_MasterServerCode",
                table: "DatabaseReadonlyServers",
                columns: new[] { "IsPublic", "MasterServerCode" });

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseReadonlyServers_IsReport_MasterServerCode",
                table: "DatabaseReadonlyServers",
                columns: new[] { "IsReport", "MasterServerCode" });

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseReadonlyServers_MasterServerCode",
                table: "DatabaseReadonlyServers",
                column: "MasterServerCode");

            migrationBuilder.CreateIndex(
                name: "UX_DatabaseReadonlyServers_Code",
                table: "DatabaseReadonlyServers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseServerConfigs_NetworkEnvCode",
                table: "DatabaseServerConfigs",
                column: "NetworkEnvCode");

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseServerConfigs_ServerCode",
                table: "DatabaseServerConfigs",
                column: "ServerCode");

            migrationBuilder.CreateIndex(
                name: "UX_DatabaseServerConfigs_ServerCode_NetworkEnvCode_DbType",
                table: "DatabaseServerConfigs",
                columns: new[] { "ServerCode", "NetworkEnvCode", "DbType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_City",
                table: "Tenants",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_DatabaseInstanceId",
                table: "Tenants",
                column: "DatabaseInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IsDeleted",
                table: "Tenants",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status",
                table: "Tenants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status_TenantType",
                table: "Tenants",
                columns: new[] { "Status", "TenantType" });

            migrationBuilder.CreateIndex(
                name: "UX_Tenants_Domain",
                table: "Tenants",
                column: "Domain",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatabaseInstances");

            migrationBuilder.DropTable(
                name: "DatabaseMasterServers");

            migrationBuilder.DropTable(
                name: "DatabaseReadonlyServers");

            migrationBuilder.DropTable(
                name: "DatabaseServerConfigs");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
