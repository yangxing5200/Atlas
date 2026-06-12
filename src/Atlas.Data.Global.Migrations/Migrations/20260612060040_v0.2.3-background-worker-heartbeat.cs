using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Global.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v023backgroundworkerheartbeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackgroundWorkerHeartbeats",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    WorkerId = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HostName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessId = table.Column<int>(type: "int", nullable: false),
                    RuntimeMode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QueuesJson = table.Column<string>(type: "longtext", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OneTimeJobWorkerEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RecurringTaskRunnerEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CurrentJobId = table.Column<long>(type: "bigint", nullable: true),
                    CurrentJobType = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrentQueue = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundWorkerHeartbeats", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundWorkerHeartbeats_CurrentJobId",
                table: "BackgroundWorkerHeartbeats",
                column: "CurrentJobId");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundWorkerHeartbeats_LastSeenAtUtc",
                table: "BackgroundWorkerHeartbeats",
                column: "LastSeenAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundWorkerHeartbeats_ProcessId",
                table: "BackgroundWorkerHeartbeats",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundWorkerHeartbeats_Runtime_LastSeen",
                table: "BackgroundWorkerHeartbeats",
                columns: new[] { "RuntimeMode", "LastSeenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_BackgroundWorkerHeartbeats_WorkerId",
                table: "BackgroundWorkerHeartbeats",
                column: "WorkerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackgroundWorkerHeartbeats");
        }
    }
}
