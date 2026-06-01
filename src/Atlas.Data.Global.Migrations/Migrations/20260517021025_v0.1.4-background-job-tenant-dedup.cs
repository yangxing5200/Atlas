using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Global.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v014backgroundjobtenantdedup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_BackgroundJobs_DeduplicationKey",
                table: "BackgroundJobs");

            migrationBuilder.CreateIndex(
                name: "UX_BackgroundJobs_Tenant_DeduplicationKey",
                table: "BackgroundJobs",
                columns: new[] { "TenantId", "DeduplicationKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_BackgroundJobs_Tenant_DeduplicationKey",
                table: "BackgroundJobs");

            migrationBuilder.CreateIndex(
                name: "UX_BackgroundJobs_DeduplicationKey",
                table: "BackgroundJobs",
                column: "DeduplicationKey",
                unique: true);
        }
    }
}
