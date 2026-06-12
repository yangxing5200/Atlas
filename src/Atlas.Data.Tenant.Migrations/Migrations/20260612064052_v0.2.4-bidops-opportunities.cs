using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v024bidopsopportunities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bidops_opportunity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    NoticeId = table.Column<long>(type: "bigint", nullable: false),
                    PackageId = table.Column<long>(type: "bigint", nullable: false),
                    OpportunityNo = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Stage = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActiveMarker = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    EstimatedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ValueScore = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    ValueLevel = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Decision = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: true),
                    NextActionAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastStageChangedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AssessmentSummary = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Remark = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_opportunity", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_opportunity_stage_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OpportunityId = table.Column<long>(type: "bigint", nullable: false),
                    FromStage = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ToStage = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OperatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_opportunity_stage_history", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_opportunity_watch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OpportunityId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Remark = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_opportunity_watch", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_NoticeId",
                table: "bidops_opportunity",
                column: "NoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_OwnerUserId",
                table: "bidops_opportunity",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_PackageId",
                table: "bidops_opportunity",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_TenantId",
                table: "bidops_opportunity",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_TenantId_CreatedAt",
                table: "bidops_opportunity",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_TenantId_NextActionAtUtc",
                table: "bidops_opportunity",
                columns: new[] { "TenantId", "NextActionAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_TenantId_NoticeId",
                table: "bidops_opportunity",
                columns: new[] { "TenantId", "NoticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_TenantId_OpportunityNo",
                table: "bidops_opportunity",
                columns: new[] { "TenantId", "OpportunityNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_TenantId_PackageId_ActiveMarker",
                table: "bidops_opportunity",
                columns: new[] { "TenantId", "PackageId", "ActiveMarker" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_TenantId_Stage_Status_CreatedAt",
                table: "bidops_opportunity",
                columns: new[] { "TenantId", "Stage", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_stage_history_OperatorUserId",
                table: "bidops_opportunity_stage_history",
                column: "OperatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_stage_history_OpportunityId",
                table: "bidops_opportunity_stage_history",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_stage_history_TenantId",
                table: "bidops_opportunity_stage_history",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_stage_history_TenantId_CreatedAt",
                table: "bidops_opportunity_stage_history",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_stage_history_TenantId_OpportunityId_Occu~",
                table: "bidops_opportunity_stage_history",
                columns: new[] { "TenantId", "OpportunityId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_stage_history_TenantId_ToStage_OccurredAt~",
                table: "bidops_opportunity_stage_history",
                columns: new[] { "TenantId", "ToStage", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_watch_OpportunityId",
                table: "bidops_opportunity_watch",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_watch_TenantId",
                table: "bidops_opportunity_watch",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_watch_TenantId_CreatedAt",
                table: "bidops_opportunity_watch",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_watch_TenantId_OpportunityId_UserId",
                table: "bidops_opportunity_watch",
                columns: new[] { "TenantId", "OpportunityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_watch_TenantId_UserId_Enabled",
                table: "bidops_opportunity_watch",
                columns: new[] { "TenantId", "UserId", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_opportunity_watch_UserId",
                table: "bidops_opportunity_watch",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bidops_opportunity");

            migrationBuilder.DropTable(
                name: "bidops_opportunity_stage_history");

            migrationBuilder.DropTable(
                name: "bidops_opportunity_watch");
        }
    }
}
