using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Global.Migrations.Migrations
{
    [Migration("20260604000100_v0.2.2-background-export")]
    public partial class v022backgroundexport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExportJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    BackgroundJobId = table.Column<long>(type: "bigint", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    StoreId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ExportTaskType = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    ResourceCode = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    PermissionCode = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Format = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    QueryJson = table.Column<string>(type: "longtext", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Progress = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ProcessedRows = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    TotalRows = table.Column<long>(type: "bigint", nullable: true),
                    FileName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    ContentType = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    StorageProvider = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    StorageKey = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    QueryHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportJobs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_ExpiresAtUtc",
                table: "ExportJobs",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_ExportTaskType",
                table: "ExportJobs",
                column: "ExportTaskType");

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_ResourceCode",
                table: "ExportJobs",
                column: "ResourceCode");

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_TenantId_Status",
                table: "ExportJobs",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_TenantId_UserId_RequestedAtUtc",
                table: "ExportJobs",
                columns: new[] { "TenantId", "UserId", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_ExportJobs_BackgroundJobId",
                table: "ExportJobs",
                column: "BackgroundJobId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportJobs");
        }
    }
}
