using Atlas.Data.Tenant.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations;

[Migration("20260629093000_v0.2.21-bidops-amount-candidates")]
[DbContext(typeof(AtlasTenantDbContext))]
public partial class v0221bidopsamountcandidates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
CREATE TABLE `bidops_amount_candidate` (
    `Id` bigint NOT NULL,
    `LifecyclePackageLinkId` bigint NULL,
    `RawNoticeId` bigint NOT NULL,
    `ResultRawNoticeId` bigint NULL,
    `RawAttachmentId` bigint NULL,
    `OutcomeSupplierRecordId` bigint NULL,
    `ProcurementDetailStagingId` bigint NULL,
    `TenderPackageId` bigint NULL,
    `SourceKind` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `SourceNoticeType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `SourceTitle` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `SourceFileName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `SourceLocation` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
    `ProjectCode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `ProjectName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `LotNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `LotName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `PackageNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `PackageName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `SupplierName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `AmountType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `AmountRaw` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `AmountValue` decimal(18,6) NULL,
    `AmountUnit` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `Currency` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
    `IsPotentialFinalAmount` tinyint(1) NOT NULL,
    `Confidence` decimal(5,4) NOT NULL,
    `Status` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `RejectReason` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `EvidenceText` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
    `ContextText` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `ManualRemark` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `SelectedBy` bigint NULL,
    `SelectedAt` datetime(6) NULL,
    `RejectedBy` bigint NULL,
    `RejectedAt` datetime(6) NULL,
    `SourceHash` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `TenantId` bigint NOT NULL,
    CONSTRAINT `PK_bidops_amount_candidate` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;
""");

        migrationBuilder.CreateIndex(
            name: "IX_bidops_amount_candidate_Tenant_CreatedAt",
            table: "bidops_amount_candidate",
            columns: new[] { "TenantId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_amount_candidate_Tenant_SourceHash",
            table: "bidops_amount_candidate",
            columns: new[] { "TenantId", "SourceHash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_bidops_amount_candidate_Tenant_RawNotice_Status",
            table: "bidops_amount_candidate",
            columns: new[] { "TenantId", "RawNoticeId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_amount_candidate_Tenant_Link_Status",
            table: "bidops_amount_candidate",
            columns: new[] { "TenantId", "LifecyclePackageLinkId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_amount_candidate_Tenant_Result_Package",
            table: "bidops_amount_candidate",
            columns: new[] { "TenantId", "ResultRawNoticeId", "PackageNo" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_amount_candidate_Tenant_OutcomeRecord",
            table: "bidops_amount_candidate",
            columns: new[] { "TenantId", "OutcomeSupplierRecordId" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_amount_candidate_Tenant_ProcDetail",
            table: "bidops_amount_candidate",
            columns: new[] { "TenantId", "ProcurementDetailStagingId" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_amount_candidate_Tenant_Attachment",
            table: "bidops_amount_candidate",
            columns: new[] { "TenantId", "RawAttachmentId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "bidops_amount_candidate");
    }
}
