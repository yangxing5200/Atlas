using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations;

[Migration("20260622100000_v0.2.16-bidops-review-automation-completion")]
public partial class v0216bidopsreviewautomationcompletion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
CREATE TABLE `bidops_review_correction_sample` (
    `Id` bigint NOT NULL,
    `ReviewTaskId` bigint NOT NULL,
    `RawNoticeId` bigint NOT NULL,
    `NoticeType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `SourceKind` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `FieldName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `OriginalValue` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CorrectedValue` longtext CHARACTER SET utf8mb4 NOT NULL,
    `OriginalHeader` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `OriginalRowJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ReviewerPrompt` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Reason` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedBy` bigint NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `TenantId` bigint NOT NULL,
    CONSTRAINT `PK_bidops_review_correction_sample` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;
""");

        migrationBuilder.CreateIndex(
            name: "IX_bidops_review_correction_Tenant_TaskCreated",
            table: "bidops_review_correction_sample",
            columns: new[] { "TenantId", "ReviewTaskId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_review_correction_Tenant_RawCreated",
            table: "bidops_review_correction_sample",
            columns: new[] { "TenantId", "RawNoticeId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_review_correction_Tenant_SourceCreated",
            table: "bidops_review_correction_sample",
            columns: new[] { "TenantId", "SourceKind", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_review_correction_Tenant_FieldCreated",
            table: "bidops_review_correction_sample",
            columns: new[] { "TenantId", "FieldName", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_review_correction_Tenant_TypeCreated",
            table: "bidops_review_correction_sample",
            columns: new[] { "TenantId", "NoticeType", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "bidops_review_correction_sample");
    }
}
