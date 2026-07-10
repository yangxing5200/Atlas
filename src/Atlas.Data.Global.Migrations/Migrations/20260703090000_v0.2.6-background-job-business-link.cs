using Atlas.Data.Global;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Global.Migrations.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AtlasGlobalDbContext))]
    [Migration("20260703090000_v0.2.6-background-job-business-link")]
    public partial class v026backgroundjobbusinesslink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceModule",
                table: "BackgroundJobs",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                table: "BackgroundJobs",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "BusinessId",
                table: "BackgroundJobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "BackgroundJobs",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Tenant_BusinessLink",
                table: "BackgroundJobs",
                columns: new[] { "TenantId", "SourceModule", "BusinessType", "BusinessId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Tenant_Correlation",
                table: "BackgroundJobs",
                columns: new[] { "TenantId", "CorrelationId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackgroundJobs_Tenant_BusinessLink",
                table: "BackgroundJobs");

            migrationBuilder.DropIndex(
                name: "IX_BackgroundJobs_Tenant_Correlation",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "SourceModule",
                table: "BackgroundJobs");
        }
    }
}
