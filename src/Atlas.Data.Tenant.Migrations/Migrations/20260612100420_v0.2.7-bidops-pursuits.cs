using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v027bidopspursuits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bidops_pursuit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    NoticeId = table.Column<long>(type: "bigint", nullable: false),
                    PackageId = table.Column<long>(type: "bigint", nullable: false),
                    OpportunityId = table.Column<long>(type: "bigint", nullable: true),
                    GoNoGoDecisionId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierNameSnapshot = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PursuitNo = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
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
                    BidDeadlineAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: true),
                    ProgressPercent = table.Column<int>(type: "int", nullable: false),
                    RiskLevel = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastStageChangedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Remark = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_pursuit", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_pursuit_follow_record",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PursuitId = table.Column<long>(type: "bigint", nullable: false),
                    FollowType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NextActionAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedByUserName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_pursuit_follow_record", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_pursuit_task",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PursuitId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TaskType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: true),
                    DueAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResultNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_pursuit_task", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_GoNoGoDecisionId",
                table: "bidops_pursuit",
                column: "GoNoGoDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_NoticeId",
                table: "bidops_pursuit",
                column: "NoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_OpportunityId",
                table: "bidops_pursuit",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_OwnerUserId",
                table: "bidops_pursuit",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_PackageId",
                table: "bidops_pursuit",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_SupplierId",
                table: "bidops_pursuit",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_TenantId",
                table: "bidops_pursuit",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_TenantId_BidDeadlineAtUtc",
                table: "bidops_pursuit",
                columns: new[] { "TenantId", "BidDeadlineAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_TenantId_CreatedAt",
                table: "bidops_pursuit",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_TenantId_NoticeId",
                table: "bidops_pursuit",
                columns: new[] { "TenantId", "NoticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_TenantId_OpportunityId",
                table: "bidops_pursuit",
                columns: new[] { "TenantId", "OpportunityId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_TenantId_OwnerUserId_Status",
                table: "bidops_pursuit",
                columns: new[] { "TenantId", "OwnerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_TenantId_PackageId_ActiveMarker",
                table: "bidops_pursuit",
                columns: new[] { "TenantId", "PackageId", "ActiveMarker" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_TenantId_PursuitNo",
                table: "bidops_pursuit",
                columns: new[] { "TenantId", "PursuitNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_TenantId_Stage_Status_CreatedAt",
                table: "bidops_pursuit",
                columns: new[] { "TenantId", "Stage", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_follow_record_CreatedByUserId",
                table: "bidops_pursuit_follow_record",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_follow_record_PursuitId",
                table: "bidops_pursuit_follow_record",
                column: "PursuitId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_follow_record_TenantId",
                table: "bidops_pursuit_follow_record",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_follow_record_TenantId_CreatedAt",
                table: "bidops_pursuit_follow_record",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_follow_record_TenantId_NextActionAtUtc",
                table: "bidops_pursuit_follow_record",
                columns: new[] { "TenantId", "NextActionAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_follow_record_TenantId_PursuitId_CreatedAt",
                table: "bidops_pursuit_follow_record",
                columns: new[] { "TenantId", "PursuitId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_task_OwnerUserId",
                table: "bidops_pursuit_task",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_task_PursuitId",
                table: "bidops_pursuit_task",
                column: "PursuitId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_task_TenantId",
                table: "bidops_pursuit_task",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_task_TenantId_CreatedAt",
                table: "bidops_pursuit_task",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_task_TenantId_DueAtUtc",
                table: "bidops_pursuit_task",
                columns: new[] { "TenantId", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_task_TenantId_OwnerUserId_Status_DueAtUtc",
                table: "bidops_pursuit_task",
                columns: new[] { "TenantId", "OwnerUserId", "Status", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_pursuit_task_TenantId_PursuitId_Status",
                table: "bidops_pursuit_task",
                columns: new[] { "TenantId", "PursuitId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bidops_pursuit");

            migrationBuilder.DropTable(
                name: "bidops_pursuit_follow_record");

            migrationBuilder.DropTable(
                name: "bidops_pursuit_task");
        }
    }
}
