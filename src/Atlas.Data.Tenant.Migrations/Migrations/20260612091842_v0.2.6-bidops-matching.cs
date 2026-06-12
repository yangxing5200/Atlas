using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v026bidopsmatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bidops_go_no_go_decision",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PackageId = table.Column<long>(type: "bigint", nullable: false),
                    OpportunityId = table.Column<long>(type: "bigint", nullable: true),
                    MatchRunId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierMatchResultId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: true),
                    Decision = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RiskSummary = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DecidedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    DecidedByUserName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_go_no_go_decision", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_missing_evidence_check",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    ResultId = table.Column<long>(type: "bigint", nullable: false),
                    PackageId = table.Column<long>(type: "bigint", nullable: false),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    RequirementId = table.Column<long>(type: "bigint", nullable: true),
                    MatchedEvidenceDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    RequiredEvidenceType = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequirementText = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Explanation = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_missing_evidence_check", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_supplier_match_result",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    PackageId = table.Column<long>(type: "bigint", nullable: false),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    SupplierNameSnapshot = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    MatchLevel = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Recommendation = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CategoryMatched = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RegionMatched = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EvidenceMatchedCount = table.Column<int>(type: "int", nullable: false),
                    MissingEvidenceCount = table.Column<int>(type: "int", nullable: false),
                    RiskFlags = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Explanation = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_supplier_match_result", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bidops_supplier_match_run",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PackageId = table.Column<long>(type: "bigint", nullable: false),
                    BackgroundJobId = table.Column<long>(type: "bigint", nullable: true),
                    RunNo = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedByUserName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CriteriaSummary = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MaxSuppliers = table.Column<int>(type: "int", nullable: false),
                    SupplierCount = table.Column<int>(type: "int", nullable: false),
                    MatchedCount = table.Column<int>(type: "int", nullable: false),
                    MissingEvidenceCount = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_supplier_match_run", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_go_no_go_decision_DecidedByUserId",
                table: "bidops_go_no_go_decision",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_go_no_go_decision_MatchRunId",
                table: "bidops_go_no_go_decision",
                column: "MatchRunId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_go_no_go_decision_OpportunityId",
                table: "bidops_go_no_go_decision",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_go_no_go_decision_PackageId",
                table: "bidops_go_no_go_decision",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_go_no_go_decision_SupplierId",
                table: "bidops_go_no_go_decision",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_go_no_go_decision_SupplierMatchResultId",
                table: "bidops_go_no_go_decision",
                column: "SupplierMatchResultId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_go_no_go_decision_TenantId",
                table: "bidops_go_no_go_decision",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_go_no_go_decision_TenantId_CreatedAt",
                table: "bidops_go_no_go_decision",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_go_no_go_decision_TenantId_MatchRunId",
                table: "bidops_go_no_go_decision",
                columns: new[] { "TenantId", "MatchRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_go_no_go_decision_TenantId_PackageId_DecidedAtUtc",
                table: "bidops_go_no_go_decision",
                columns: new[] { "TenantId", "PackageId", "DecidedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_go_no_go_decision_TenantId_SupplierId",
                table: "bidops_go_no_go_decision",
                columns: new[] { "TenantId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_missing_evidence_check_MatchedEvidenceDocumentId",
                table: "bidops_missing_evidence_check",
                column: "MatchedEvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_missing_evidence_check_PackageId",
                table: "bidops_missing_evidence_check",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_missing_evidence_check_RequirementId",
                table: "bidops_missing_evidence_check",
                column: "RequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_missing_evidence_check_ResultId",
                table: "bidops_missing_evidence_check",
                column: "ResultId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_missing_evidence_check_RunId",
                table: "bidops_missing_evidence_check",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_missing_evidence_check_SupplierId",
                table: "bidops_missing_evidence_check",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_missing_evidence_check_TenantId",
                table: "bidops_missing_evidence_check",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_missing_evidence_check_TenantId_CreatedAt",
                table: "bidops_missing_evidence_check",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_missing_evidence_check_TenantId_ResultId",
                table: "bidops_missing_evidence_check",
                columns: new[] { "TenantId", "ResultId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_missing_evidence_check_TenantId_RunId_SupplierId",
                table: "bidops_missing_evidence_check",
                columns: new[] { "TenantId", "RunId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_missing_evidence_check_TenantId_Status_CreatedAt",
                table: "bidops_missing_evidence_check",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_result_PackageId",
                table: "bidops_supplier_match_result",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_result_RunId",
                table: "bidops_supplier_match_result",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_result_SupplierId",
                table: "bidops_supplier_match_result",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_result_TenantId",
                table: "bidops_supplier_match_result",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_result_TenantId_CreatedAt",
                table: "bidops_supplier_match_result",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_result_TenantId_PackageId_SupplierId",
                table: "bidops_supplier_match_result",
                columns: new[] { "TenantId", "PackageId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_result_TenantId_RunId_Rank",
                table: "bidops_supplier_match_result",
                columns: new[] { "TenantId", "RunId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_result_TenantId_SupplierId_CreatedAt",
                table: "bidops_supplier_match_result",
                columns: new[] { "TenantId", "SupplierId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_run_BackgroundJobId",
                table: "bidops_supplier_match_run",
                column: "BackgroundJobId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_run_PackageId",
                table: "bidops_supplier_match_run",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_run_RequestedByUserId",
                table: "bidops_supplier_match_run",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_run_TenantId",
                table: "bidops_supplier_match_run",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_run_TenantId_BackgroundJobId",
                table: "bidops_supplier_match_run",
                columns: new[] { "TenantId", "BackgroundJobId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_run_TenantId_CreatedAt",
                table: "bidops_supplier_match_run",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_run_TenantId_PackageId_CreatedAt",
                table: "bidops_supplier_match_run",
                columns: new[] { "TenantId", "PackageId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_run_TenantId_RunNo",
                table: "bidops_supplier_match_run",
                columns: new[] { "TenantId", "RunNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_match_run_TenantId_Status_CreatedAt",
                table: "bidops_supplier_match_run",
                columns: new[] { "TenantId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bidops_go_no_go_decision");

            migrationBuilder.DropTable(
                name: "bidops_missing_evidence_check");

            migrationBuilder.DropTable(
                name: "bidops_supplier_match_result");

            migrationBuilder.DropTable(
                name: "bidops_supplier_match_run");
        }
    }
}
