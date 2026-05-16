using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v012addtenantmessagingstate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantInboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConsumerName = table.Column<string>(type: "varchar(256)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantInboxMessages", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TenantOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    StoreId = table.Column<long>(type: "bigint", nullable: true),
                    EventId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EventName = table.Column<string>(type: "varchar(256)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MessageType = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Payload = table.Column<string>(type: "longtext", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AvailableAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProcessingAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProcessingBy = table.Column<string>(type: "varchar(256)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastError = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantOutboxMessages", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInboxMessages_MessageId",
                table: "TenantInboxMessages",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInboxMessages_Tenant_ReceivedAt",
                table: "TenantInboxMessages",
                columns: new[] { "TenantId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantInboxMessages_TenantId",
                table: "TenantInboxMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_TenantInboxMessages_Message_Consumer",
                table: "TenantInboxMessages",
                columns: new[] { "MessageId", "ConsumerName" },
                unique: true);

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
                name: "IX_TenantOutboxMessages_StoreId",
                table: "TenantOutboxMessages",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantOutboxMessages_Tenant_ProcessDue",
                table: "TenantOutboxMessages",
                columns: new[] { "TenantId", "ProcessedAtUtc", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantOutboxMessages_TenantId",
                table: "TenantOutboxMessages",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantInboxMessages");

            migrationBuilder.DropTable(
                name: "TenantOutboxMessages");
        }
    }
}
