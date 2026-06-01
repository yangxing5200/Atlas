using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Global.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v013addbackgroundjobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackgroundJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    JobType = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Queue = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, defaultValue: "default")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JobName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeduplicationKey = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    StoreId = table.Column<long>(type: "bigint", nullable: true),
                    Payload = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AvailableAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LockedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LockedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Result = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_DispatchDue",
                table: "BackgroundJobs",
                columns: new[] { "Queue", "Status", "AvailableAtUtc", "NextAttemptAtUtc", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_JobType",
                table: "BackgroundJobs",
                column: "JobType");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_RunningLocks",
                table: "BackgroundJobs",
                columns: new[] { "Queue", "Status", "LockedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Queue",
                table: "BackgroundJobs",
                column: "Queue");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_StoreId",
                table: "BackgroundJobs",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Tenant_CreatedAt",
                table: "BackgroundJobs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_TenantId",
                table: "BackgroundJobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_BackgroundJobs_DeduplicationKey",
                table: "BackgroundJobs",
                column: "DeduplicationKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackgroundJobs");
        }
    }
}
