using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v003changetablename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessOperationLogs");

            migrationBuilder.CreateTable(
                name: "OperationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    StoreId = table.Column<long>(type: "bigint", nullable: true),
                    SessionId = table.Column<string>(type: "varchar(256)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Module = table.Column<string>(type: "varchar(256)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OperationType = table.Column<string>(type: "varchar(256)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityId = table.Column<long>(type: "bigint", nullable: true),
                    Changes = table.Column<string>(type: "varchar(256)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IpAddress = table.Column<string>(type: "varchar(256)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSuccess = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    ErrorMessage = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_CreatedAt",
                table: "OperationLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_Module_EntityId",
                table: "OperationLogs",
                columns: new[] { "Module", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_Module_Type_CreatedAt",
                table: "OperationLogs",
                columns: new[] { "Module", "OperationType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_SessionId",
                table: "OperationLogs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_TenantId_UserId",
                table: "OperationLogs",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_UserId",
                table: "OperationLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_EntityId",
                table: "OperationLogs",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_StoreId",
                table: "OperationLogs",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_TenantId",
                table: "OperationLogs",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationLogs");

            migrationBuilder.CreateTable(
                name: "BusinessOperationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Changes = table.Column<string>(type: "varchar(256)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Description = table.Column<string>(type: "varchar(256)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityId = table.Column<long>(type: "bigint", nullable: true),
                    ErrorMessage = table.Column<string>(type: "varchar(256)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IpAddress = table.Column<string>(type: "varchar(256)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSuccess = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    Module = table.Column<string>(type: "varchar(256)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OperationType = table.Column<string>(type: "varchar(256)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SessionId = table.Column<string>(type: "varchar(256)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoreId = table.Column<long>(type: "bigint", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessOperationLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_CreatedAt",
                table: "BusinessOperationLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_EntityId",
                table: "BusinessOperationLogs",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_Module_EntityId",
                table: "BusinessOperationLogs",
                columns: new[] { "Module", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_Module_Type_CreatedAt",
                table: "BusinessOperationLogs",
                columns: new[] { "Module", "OperationType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_SessionId",
                table: "BusinessOperationLogs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_StoreId",
                table: "BusinessOperationLogs",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_TenantId",
                table: "BusinessOperationLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_TenantId_UserId",
                table: "BusinessOperationLogs",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperationLogs_UserId",
                table: "BusinessOperationLogs",
                column: "UserId");
        }
    }
}
