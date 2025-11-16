using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Global.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v003 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedBy",
                table: "Tenants",
                type: "bigint",
                nullable: true,
                comment: "更新人ID",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Tenants",
                type: "datetime(6)",
                nullable: true,
                comment: "更新时间",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TenantType",
                table: "Tenants",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "租户类型：Enterprise, Individual",
                oldClrType: typeof(byte),
                oldType: "tinyint unsigned")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Tenants",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "租户状态：Active, Inactive, Suspended",
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Province",
                table: "Tenants",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                comment: "省份",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Tenants",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "公司电话",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "OfficeCount",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "诊所/门店数量",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Tenants",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                comment: "公司名称",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "Tenants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false,
                comment: "是否已删除（软删除）",
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<string>(
                name: "Domain",
                table: "Tenants",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "公司代码（租户唯一标识，用于登录）",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<long>(
                name: "DeletedBy",
                table: "Tenants",
                type: "bigint",
                nullable: true,
                comment: "删除人ID",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DeletedAt",
                table: "Tenants",
                type: "datetime(6)",
                nullable: true,
                comment: "删除时间",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "DatabaseInstanceId",
                table: "Tenants",
                type: "bigint",
                nullable: false,
                comment: "关联的数据库实例ID",
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "CreatedBy",
                table: "Tenants",
                type: "bigint",
                nullable: true,
                comment: "创建人ID",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Tenants",
                type: "datetime(6)",
                nullable: false,
                comment: "创建时间",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<string>(
                name: "ContactPhoneNumber",
                table: "Tenants",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "联系人手机号",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ContactName",
                table: "Tenants",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "联系人姓名",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ContactEmail",
                table: "Tenants",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "联系人邮箱",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Tenants",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "城市",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Tenants",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                comment: "租户类别：试用、Mobile等",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "BusinessType",
                table: "Tenants",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "连锁类型：Single, Chain, Franchise",
                oldClrType: typeof(byte),
                oldType: "tinyint unsigned")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "BrandName",
                table: "Tenants",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "品牌名称",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Tenants",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "公司地址",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "DatabaseServerConfigs",
                type: "datetime(6)",
                nullable: true,
                comment: "更新时间",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ServerCode",
                table: "DatabaseServerConfigs",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "服务器编码（关联MasterServer或ReadonlyServer）",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "NetworkEnvCode",
                table: "DatabaseServerConfigs",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "default",
                comment: "网络环境编码：default, classic, vpc",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "DbType",
                table: "DatabaseServerConfigs",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "数据库类型：SqlServer, MySQL, PostgreSQL",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "DatabaseServerConfigs",
                type: "datetime(6)",
                nullable: false,
                comment: "创建时间",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<string>(
                name: "ConnString",
                table: "DatabaseServerConfigs",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                comment: "数据库连接串",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "DatabaseReadonlyServers",
                type: "datetime(6)",
                nullable: true,
                comment: "更新时间",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NickName",
                table: "DatabaseReadonlyServers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "只读服务器昵称",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "MasterServerCode",
                table: "DatabaseReadonlyServers",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "所属主数据库Server编码",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "IsReport",
                table: "DatabaseReadonlyServers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false,
                comment: "是否是报表只读库",
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPublic",
                table: "DatabaseReadonlyServers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false,
                comment: "是否公开给周边服务访问",
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "DatabaseReadonlyServers",
                type: "datetime(6)",
                nullable: false,
                comment: "创建时间",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "DatabaseReadonlyServers",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "只读服务器编码（唯一标识）",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "DatabaseMasterServers",
                type: "datetime(6)",
                nullable: true,
                comment: "更新时间",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NickName",
                table: "DatabaseMasterServers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "服务器昵称",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "DatabaseMasterServers",
                type: "datetime(6)",
                nullable: false,
                comment: "创建时间",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "DatabaseMasterServers",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "服务器编码（唯一标识）",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Version",
                table: "DatabaseInstances",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "数据库版本",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "DatabaseInstances",
                type: "datetime(6)",
                nullable: true,
                comment: "更新时间",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Region",
                table: "DatabaseInstances",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "所属区域：华东、华北、华南等",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DatabaseInstances",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "实例名称",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "MasterServerCode",
                table: "DatabaseInstances",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "主数据库Server编码",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "DbType",
                table: "DatabaseInstances",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                comment: "数据库类型：SqlServer, MySQL, PostgreSQL",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "DbName",
                table: "DatabaseInstances",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "数据库名称",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "DatabaseInstances",
                type: "datetime(6)",
                nullable: false,
                comment: "创建时间",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<string>(
                name: "ConnectionString",
                table: "DatabaseInstances",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                comment: "数据库连接串（可选，优先级高于ServerConfig）",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_City",
                table: "Tenants",
                column: "City");

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
                name: "UX_DatabaseMasterServers_Code",
                table: "DatabaseMasterServers",
                column: "Code",
                unique: true);

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

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropIndex(
                name: "IX_Tenants_City",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_IsDeleted",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_Status",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_Status_TenantType",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "UX_Tenants_Domain",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_DatabaseServerConfigs_NetworkEnvCode",
                table: "DatabaseServerConfigs");

            migrationBuilder.DropIndex(
                name: "IX_DatabaseServerConfigs_ServerCode",
                table: "DatabaseServerConfigs");

            migrationBuilder.DropIndex(
                name: "UX_DatabaseServerConfigs_ServerCode_NetworkEnvCode_DbType",
                table: "DatabaseServerConfigs");

            migrationBuilder.DropIndex(
                name: "IX_DatabaseReadonlyServers_IsPublic_MasterServerCode",
                table: "DatabaseReadonlyServers");

            migrationBuilder.DropIndex(
                name: "IX_DatabaseReadonlyServers_IsReport_MasterServerCode",
                table: "DatabaseReadonlyServers");

            migrationBuilder.DropIndex(
                name: "IX_DatabaseReadonlyServers_MasterServerCode",
                table: "DatabaseReadonlyServers");

            migrationBuilder.DropIndex(
                name: "UX_DatabaseReadonlyServers_Code",
                table: "DatabaseReadonlyServers");

            migrationBuilder.DropIndex(
                name: "UX_DatabaseMasterServers_Code",
                table: "DatabaseMasterServers");

            migrationBuilder.DropIndex(
                name: "IX_DatabaseInstances_DbType",
                table: "DatabaseInstances");

            migrationBuilder.DropIndex(
                name: "IX_DatabaseInstances_MasterServerCode",
                table: "DatabaseInstances");

            migrationBuilder.DropIndex(
                name: "IX_DatabaseInstances_Region",
                table: "DatabaseInstances");

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedBy",
                table: "Tenants",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "更新人ID");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Tenants",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true,
                oldComment: "更新时间");

            migrationBuilder.AlterColumn<byte>(
                name: "TenantType",
                table: "Tenants",
                type: "tinyint unsigned",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "租户类型：Enterprise, Individual")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Tenants",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "租户状态：Active, Inactive, Suspended")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Province",
                table: "Tenants",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "省份")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Tenants",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "公司电话")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "OfficeCount",
                table: "Tenants",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0,
                oldComment: "诊所/门店数量");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Tenants",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200,
                oldComment: "公司名称")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "Tenants",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldDefaultValue: false,
                oldComment: "是否已删除（软删除）");

            migrationBuilder.AlterColumn<string>(
                name: "Domain",
                table: "Tenants",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldComment: "公司代码（租户唯一标识，用于登录）")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<long>(
                name: "DeletedBy",
                table: "Tenants",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "删除人ID");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DeletedAt",
                table: "Tenants",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true,
                oldComment: "删除时间");

            migrationBuilder.AlterColumn<long>(
                name: "DatabaseInstanceId",
                table: "Tenants",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "关联的数据库实例ID");

            migrationBuilder.AlterColumn<long>(
                name: "CreatedBy",
                table: "Tenants",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "创建人ID");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Tenants",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldComment: "创建时间");

            migrationBuilder.AlterColumn<string>(
                name: "ContactPhoneNumber",
                table: "Tenants",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "联系人手机号")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ContactName",
                table: "Tenants",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldComment: "联系人姓名")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ContactEmail",
                table: "Tenants",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "联系人邮箱")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Tenants",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "城市")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Tenants",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "租户类别：试用、Mobile等")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<byte>(
                name: "BusinessType",
                table: "Tenants",
                type: "tinyint unsigned",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "连锁类型：Single, Chain, Franchise")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "BrandName",
                table: "Tenants",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "品牌名称")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Tenants",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "公司地址")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "DatabaseServerConfigs",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true,
                oldComment: "更新时间");

            migrationBuilder.AlterColumn<string>(
                name: "ServerCode",
                table: "DatabaseServerConfigs",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "服务器编码（关联MasterServer或ReadonlyServer）")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "NetworkEnvCode",
                table: "DatabaseServerConfigs",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "default",
                oldComment: "网络环境编码：default, classic, vpc")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "DbType",
                table: "DatabaseServerConfigs",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "数据库类型：SqlServer, MySQL, PostgreSQL")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "DatabaseServerConfigs",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldComment: "创建时间");

            migrationBuilder.AlterColumn<string>(
                name: "ConnString",
                table: "DatabaseServerConfigs",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldComment: "数据库连接串")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "DatabaseReadonlyServers",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true,
                oldComment: "更新时间");

            migrationBuilder.AlterColumn<string>(
                name: "NickName",
                table: "DatabaseReadonlyServers",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldComment: "只读服务器昵称")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "MasterServerCode",
                table: "DatabaseReadonlyServers",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "所属主数据库Server编码")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "IsReport",
                table: "DatabaseReadonlyServers",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldDefaultValue: false,
                oldComment: "是否是报表只读库");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPublic",
                table: "DatabaseReadonlyServers",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldDefaultValue: false,
                oldComment: "是否公开给周边服务访问");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "DatabaseReadonlyServers",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldComment: "创建时间");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "DatabaseReadonlyServers",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "只读服务器编码（唯一标识）")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "DatabaseMasterServers",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true,
                oldComment: "更新时间");

            migrationBuilder.AlterColumn<string>(
                name: "NickName",
                table: "DatabaseMasterServers",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldComment: "服务器昵称")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "DatabaseMasterServers",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldComment: "创建时间");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "DatabaseMasterServers",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "服务器编码（唯一标识）")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Version",
                table: "DatabaseInstances",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "数据库版本")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "DatabaseInstances",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true,
                oldComment: "更新时间");

            migrationBuilder.AlterColumn<string>(
                name: "Region",
                table: "DatabaseInstances",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "所属区域：华东、华北、华南等")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DatabaseInstances",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldComment: "实例名称")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "MasterServerCode",
                table: "DatabaseInstances",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "主数据库Server编码")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "DbType",
                table: "DatabaseInstances",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldComment: "数据库类型：SqlServer, MySQL, PostgreSQL")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "DbName",
                table: "DatabaseInstances",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldComment: "数据库名称")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "DatabaseInstances",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldComment: "创建时间");

            migrationBuilder.AlterColumn<string>(
                name: "ConnectionString",
                table: "DatabaseInstances",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldComment: "数据库连接串（可选，优先级高于ServerConfig）")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

        }
    }
}
