using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations;

public partial class v020enterprisesecurity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_UserRoles_UserId_RoleId",
            table: "UserRoles");

        migrationBuilder.AddColumn<long>(
            name: "StoreId",
            table: "UserRoles",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "AuditEvents",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false),
                TenantId = table.Column<long>(type: "bigint", nullable: false),
                UserId = table.Column<long>(type: "bigint", nullable: true),
                StoreId = table.Column<long>(type: "bigint", nullable: true),
                SessionId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                TraceId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Category = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Action = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Outcome = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                EntityType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                EntityId = table.Column<long>(type: "bigint", nullable: true),
                IpAddress = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                UserAgent = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Metadata = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ErrorMessage = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditEvents", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Permissions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false),
                TenantId = table.Column<long>(type: "bigint", nullable: false),
                Code = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Module = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Scope = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                IsBuiltIn = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Permissions", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "RefreshTokens",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false),
                TenantId = table.Column<long>(type: "bigint", nullable: false),
                UserId = table.Column<long>(type: "bigint", nullable: false),
                StoreId = table.Column<long>(type: "bigint", nullable: true),
                SessionId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                TokenHash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevokedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                RevokedReason = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreatedByIp = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                UserAgent = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ReplacedByTokenId = table.Column<long>(type: "bigint", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RefreshTokens", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false),
                TenantId = table.Column<long>(type: "bigint", nullable: false),
                Code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Scope = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                StoreId = table.Column<long>(type: "bigint", nullable: true),
                IsSystem = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "RolePermissions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false),
                TenantId = table.Column<long>(type: "bigint", nullable: false),
                RoleId = table.Column<long>(type: "bigint", nullable: false),
                PermissionId = table.Column<long>(type: "bigint", nullable: false),
                GrantedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                GrantedBy = table.Column<long>(type: "bigint", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RolePermissions", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_Tenant_Category_Action",
            table: "AuditEvents",
            columns: new[] { "TenantId", "Category", "Action", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_Tenant_CreatedAt",
            table: "AuditEvents",
            columns: new[] { "TenantId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_Tenant_User_CreatedAt",
            table: "AuditEvents",
            columns: new[] { "TenantId", "UserId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_TraceId",
            table: "AuditEvents",
            column: "TraceId");

        migrationBuilder.CreateIndex(
            name: "IX_Permissions_Tenant_Module_Scope",
            table: "Permissions",
            columns: new[] { "TenantId", "Module", "Scope" });

        migrationBuilder.CreateIndex(
            name: "UX_Permissions_TenantId_Code",
            table: "Permissions",
            columns: new[] { "TenantId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_Tenant_Expiry_Status",
            table: "RefreshTokens",
            columns: new[] { "TenantId", "ExpiresAtUtc", "RevokedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_Tenant_User_Session",
            table: "RefreshTokens",
            columns: new[] { "TenantId", "UserId", "SessionId" });

        migrationBuilder.CreateIndex(
            name: "UX_RefreshTokens_Tenant_TokenHash",
            table: "RefreshTokens",
            columns: new[] { "TenantId", "TokenHash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_Tenant_Permission",
            table: "RolePermissions",
            columns: new[] { "TenantId", "PermissionId" });

        migrationBuilder.CreateIndex(
            name: "UX_RolePermissions_Tenant_Role_Permission",
            table: "RolePermissions",
            columns: new[] { "TenantId", "RoleId", "PermissionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Roles_Tenant_Scope_Store",
            table: "Roles",
            columns: new[] { "TenantId", "Scope", "StoreId" });

        migrationBuilder.CreateIndex(
            name: "UX_Roles_TenantId_Code",
            table: "Roles",
            columns: new[] { "TenantId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserRoles_TenantId_RoleId",
            table: "UserRoles",
            columns: new[] { "TenantId", "RoleId" });

        migrationBuilder.CreateIndex(
            name: "UX_UserRoles_Tenant_User_Role_Store",
            table: "UserRoles",
            columns: new[] { "TenantId", "UserId", "RoleId", "StoreId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuditEvents");
        migrationBuilder.DropTable(name: "Permissions");
        migrationBuilder.DropTable(name: "RefreshTokens");
        migrationBuilder.DropTable(name: "RolePermissions");
        migrationBuilder.DropTable(name: "Roles");

        migrationBuilder.DropIndex(
            name: "IX_UserRoles_TenantId_RoleId",
            table: "UserRoles");

        migrationBuilder.DropIndex(
            name: "UX_UserRoles_Tenant_User_Role_Store",
            table: "UserRoles");

        migrationBuilder.DropColumn(
            name: "StoreId",
            table: "UserRoles");

        migrationBuilder.CreateIndex(
            name: "IX_UserRoles_UserId_RoleId",
            table: "UserRoles",
            columns: new[] { "UserId", "RoleId" },
            unique: true);
    }
}
