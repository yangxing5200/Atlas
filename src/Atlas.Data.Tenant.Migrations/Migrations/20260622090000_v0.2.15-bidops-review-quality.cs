using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations;

[Migration("20260622090000_v0.2.15-bidops-review-quality")]
public partial class v0215bidopsreviewquality : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
ALTER TABLE `bidops_review_task`
    ADD COLUMN `QualityScore` int NOT NULL DEFAULT 100,
    ADD COLUMN `RiskLevel` int NOT NULL DEFAULT 0,
    ADD COLUMN `QualityIssueCount` int NOT NULL DEFAULT 0,
    ADD COLUMN `HighRiskIssueCount` int NOT NULL DEFAULT 0,
    ADD COLUMN `ReviewRecommendation` int NOT NULL DEFAULT 0;
""");

        migrationBuilder.Sql("""
CREATE TABLE `bidops_review_quality_issue` (
    `Id` bigint NOT NULL,
    `ReviewTaskId` bigint NOT NULL,
    `RawNoticeId` bigint NOT NULL,
    `NoticeStagingId` bigint NOT NULL,
    `PackageStagingId` bigint NULL,
    `OutcomeSupplierRecordId` bigint NULL,
    `ProcurementDetailStagingId` bigint NULL,
    `IssueType` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `Severity` int NOT NULL,
    `FieldName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `Message` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `EvidenceJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    `IsResolved` tinyint(1) NOT NULL,
    `ResolvedBy` bigint NULL,
    `ResolvedAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `TenantId` bigint NOT NULL,
    CONSTRAINT `PK_bidops_review_quality_issue` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;
""");

        migrationBuilder.CreateIndex(
            name: "IX_bidops_review_quality_issue_Tenant_Task",
            table: "bidops_review_quality_issue",
            columns: new[] { "TenantId", "ReviewTaskId", "IsResolved" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_review_quality_issue_Tenant_Raw",
            table: "bidops_review_quality_issue",
            columns: new[] { "TenantId", "RawNoticeId" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_review_quality_issue_Tenant_Notice",
            table: "bidops_review_quality_issue",
            columns: new[] { "TenantId", "NoticeStagingId" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_review_quality_issue_Tenant_SeverityCreated",
            table: "bidops_review_quality_issue",
            columns: new[] { "TenantId", "Severity", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_review_quality_issue_Tenant_Type",
            table: "bidops_review_quality_issue",
            columns: new[] { "TenantId", "IssueType" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_review_task_Tenant_RiskStatusCreated",
            table: "bidops_review_task",
            columns: new[] { "TenantId", "RiskLevel", "Status", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_bidops_review_task_Tenant_RiskStatusCreated",
            table: "bidops_review_task");

        migrationBuilder.DropTable(name: "bidops_review_quality_issue");

        migrationBuilder.Sql("""
ALTER TABLE `bidops_review_task`
    DROP COLUMN `QualityScore`,
    DROP COLUMN `RiskLevel`,
    DROP COLUMN `QualityIssueCount`,
    DROP COLUMN `HighRiskIssueCount`,
    DROP COLUMN `ReviewRecommendation`;
""");
    }
}
