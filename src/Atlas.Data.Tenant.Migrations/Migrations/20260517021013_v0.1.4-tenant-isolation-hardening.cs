using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v014tenantisolationhardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantOutboxMessages_EventId",
                table: "TenantOutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_TenantOutboxMessages_Processing",
                table: "TenantOutboxMessages");

            migrationBuilder.DropIndex(
                name: "UX_TenantInboxMessages_Message_Consumer",
                table: "TenantInboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_TenantOutboxMessages_EventId",
                table: "TenantOutboxMessages",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantOutboxMessages_Tenant_Processing",
                table: "TenantOutboxMessages",
                columns: new[] { "TenantId", "ProcessingAtUtc", "ProcessedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_TenantOutboxMessages_Tenant_EventId",
                table: "TenantOutboxMessages",
                columns: new[] { "TenantId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TenantInboxMessages_Tenant_Message_Consumer",
                table: "TenantInboxMessages",
                columns: new[] { "TenantId", "MessageId", "ConsumerName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantOutboxMessages_EventId",
                table: "TenantOutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_TenantOutboxMessages_Tenant_Processing",
                table: "TenantOutboxMessages");

            migrationBuilder.DropIndex(
                name: "UX_TenantOutboxMessages_Tenant_EventId",
                table: "TenantOutboxMessages");

            migrationBuilder.DropIndex(
                name: "UX_TenantInboxMessages_Tenant_Message_Consumer",
                table: "TenantInboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_TenantOutboxMessages_EventId",
                table: "TenantOutboxMessages",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantOutboxMessages_Processing",
                table: "TenantOutboxMessages",
                columns: new[] { "ProcessingAtUtc", "ProcessedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_TenantInboxMessages_Message_Consumer",
                table: "TenantInboxMessages",
                columns: new[] { "MessageId", "ConsumerName" },
                unique: true);
        }
    }
}
