using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v021authorizationrbacmetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stores_Stores_ParentStoreId",
                table: "Stores");

            migrationBuilder.DropForeignKey(
                name: "FK_UserLoginLogs_Users_UserId",
                table: "UserLoginLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Users_UserId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Stores_DefaultStoreId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_UserStores_Stores_StoreId",
                table: "UserStores");

            migrationBuilder.DropForeignKey(
                name: "FK_UserStores_Users_UserId",
                table: "UserStores");

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
                    SessionId = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TraceId = table.Column<string>(type: "varchar(256)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(256)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Action = table.Column<string>(type: "varchar(256)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Outcome = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    EntityType = table.Column<string>(type: "varchar(256)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityId = table.Column<long>(type: "bigint", nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(256)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserAgent = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Metadata = table.Column<string>(type: "varchar(256)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: true)
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
                    Code = table.Column<string>(type: "varchar(256)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CapabilityCode = table.Column<string>(type: "varchar(256)", maxLength: 150, nullable: false, defaultValue: "")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Module = table.Column<string>(type: "varchar(256)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Scope = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Resource = table.Column<string>(type: "varchar(256)", maxLength: 150, nullable: false, defaultValue: "")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Action = table.Column<string>(type: "varchar(256)", maxLength: 100, nullable: false, defaultValue: "")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsAssignable = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    IsSystem = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    RiskLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
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
                    SessionId = table.Column<string>(type: "varchar(256)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TokenHash = table.Column<string>(type: "varchar(256)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RevokedReason = table.Column<string>(type: "varchar(256)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByIp = table.Column<string>(type: "varchar(256)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserAgent = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: true)
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
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionId = table.Column<long>(type: "bigint", nullable: false),
                    Effect = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    DataScopeType = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DataScopeJson = table.Column<string>(type: "longtext", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
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

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "varchar(256)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_StoreId",
                table: "UserRoles",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TenantId_RoleId",
                table: "UserRoles",
                columns: new[] { "TenantId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "UX_UserRoles_Tenant_User_Role_Store",
                table: "UserRoles",
                columns: new[] { "TenantId", "UserId", "RoleId", "StoreId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EntityId",
                table: "AuditEvents",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_SessionId",
                table: "AuditEvents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_StoreId",
                table: "AuditEvents",
                column: "StoreId");

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
                name: "IX_AuditEvents_TenantId",
                table: "AuditEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TraceId",
                table: "AuditEvents",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_UserId",
                table: "AuditEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Tenant_Capability",
                table: "Permissions",
                columns: new[] { "TenantId", "CapabilityCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Tenant_Module_Scope",
                table: "Permissions",
                columns: new[] { "TenantId", "Module", "Scope" });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_TenantId",
                table: "Permissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_Permissions_TenantId_Code",
                table: "Permissions",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ReplacedByTokenId",
                table: "RefreshTokens",
                column: "ReplacedByTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_SessionId",
                table: "RefreshTokens",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_StoreId",
                table: "RefreshTokens",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Tenant_Expiry_Status",
                table: "RefreshTokens",
                columns: new[] { "TenantId", "ExpiresAtUtc", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Tenant_User_Session",
                table: "RefreshTokens",
                columns: new[] { "TenantId", "UserId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TenantId",
                table: "RefreshTokens",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_RefreshTokens_Tenant_TokenHash",
                table: "RefreshTokens",
                columns: new[] { "TenantId", "TokenHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_Tenant_Permission",
                table: "RolePermissions",
                columns: new[] { "TenantId", "PermissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_TenantId",
                table: "RolePermissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_RolePermissions_Tenant_Role_Permission",
                table: "RolePermissions",
                columns: new[] { "TenantId", "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_StoreId",
                table: "Roles",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Tenant_Scope_Store",
                table: "Roles",
                columns: new[] { "TenantId", "Scope", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId",
                table: "Roles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_Roles_TenantId_Code",
                table: "Roles",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_StoreId",
                table: "UserRoles");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_Stores_ParentStoreId",
                table: "Stores",
                column: "ParentStoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserLoginLogs_Users_UserId",
                table: "UserLoginLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Users_UserId",
                table: "UserRoles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Stores_DefaultStoreId",
                table: "Users",
                column: "DefaultStoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserStores_Stores_StoreId",
                table: "UserStores",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserStores_Users_UserId",
                table: "UserStores",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
