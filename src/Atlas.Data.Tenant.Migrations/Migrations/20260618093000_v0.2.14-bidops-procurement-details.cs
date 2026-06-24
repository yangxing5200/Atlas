using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations;

[Migration("20260618093000_v0.2.14-bidops-procurement-details")]
public partial class v0214bidopsprocurementdetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
CREATE TABLE `bidops_procurement_detail_staging` (
    `Id` bigint NOT NULL,
    `NoticeStagingId` bigint NOT NULL,
    `PackageStagingId` bigint NULL,
    `RawNoticeId` bigint NOT NULL,
    `RawAttachmentId` bigint NULL,
    `TableIndex` int NULL,
    `RowIndex` int NULL,
    `SourceSheetName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `ProjectCode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `ProjectName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `ProcurementApplicationNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `LineItemNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `MaterialCode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `LotSequence` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `LotNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `LotName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `EcpLotName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `PackageNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `PackageName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `PackageType` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `Category` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `ProcurementMethod` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `BuyerName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `ProjectUnit` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `ConstructionUnit` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `ProcurementContent` text CHARACTER SET utf8mb4 NOT NULL,
    `ScopeText` text CHARACTER SET utf8mb4 NOT NULL,
    `ProjectOverview` text CHARACTER SET utf8mb4 NOT NULL,
    `Location` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `VoltageLevel` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `ProcurementAmount` decimal(18,2) NULL,
    `BudgetAmount` decimal(18,2) NULL,
    `ItemEstimatedAmount` decimal(18,2) NULL,
    `PackageEstimatedAmount` decimal(18,2) NULL,
    `MaxPrice` decimal(18,2) NULL,
    `MaxPriceRatePercent` decimal(9,4) NULL,
    `TaxRatePercent` decimal(9,4) NULL,
    `ResponseGuaranteeAmount` decimal(18,2) NULL,
    `QuoteMode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `SettlementMode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `PlannedStartDate` datetime(6) NULL,
    `PlannedCompletionDate` datetime(6) NULL,
    `ServicePeriodDays` int NULL,
    `ServicePeriodText` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `QualificationRequirement` text CHARACTER SET utf8mb4 NOT NULL,
    `PerformanceRequirement` text CHARACTER SET utf8mb4 NOT NULL,
    `PersonnelRequirement` text CHARACTER SET utf8mb4 NOT NULL,
    `OtherRequirement` text CHARACTER SET utf8mb4 NOT NULL,
    `JointVentureAllowed` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `SubcontractAllowed` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `AwardLimit` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `TechnicalSpecId` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
    `ContractTemplate` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `BusinessWeight` decimal(5,2) NULL,
    `TechnicalWeight` decimal(5,2) NULL,
    `PriceWeight` decimal(5,2) NULL,
    `PriceCalculationMethod` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `PriceParameter` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `Remarks` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `OriginalHeaderJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    `OriginalRowJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    `NormalizedFieldsJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    `AiConfidence` decimal(5,4) NOT NULL,
    `ReviewStatus` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `TenantId` bigint NOT NULL,
    CONSTRAINT `PK_bidops_procurement_detail_staging` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;
""");

        migrationBuilder.Sql("""
CREATE TABLE `bidops_procurement_detail` (
    `Id` bigint NOT NULL,
    `NoticeId` bigint NOT NULL,
    `TenderPackageId` bigint NULL,
    `ProcurementDetailStagingId` bigint NULL,
    `RawNoticeId` bigint NOT NULL,
    `RawAttachmentId` bigint NULL,
    `TableIndex` int NULL,
    `RowIndex` int NULL,
    `SourceSheetName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `ProjectCode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `ProjectName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `ProcurementApplicationNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `LineItemNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `MaterialCode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `LotSequence` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `LotNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `LotName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `EcpLotName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `PackageNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `PackageName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `PackageType` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `Category` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `ProcurementMethod` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `BuyerName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `ProjectUnit` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `ConstructionUnit` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `ProcurementContent` text CHARACTER SET utf8mb4 NOT NULL,
    `ScopeText` text CHARACTER SET utf8mb4 NOT NULL,
    `ProjectOverview` text CHARACTER SET utf8mb4 NOT NULL,
    `Location` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `VoltageLevel` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `ProcurementAmount` decimal(18,2) NULL,
    `BudgetAmount` decimal(18,2) NULL,
    `ItemEstimatedAmount` decimal(18,2) NULL,
    `PackageEstimatedAmount` decimal(18,2) NULL,
    `MaxPrice` decimal(18,2) NULL,
    `MaxPriceRatePercent` decimal(9,4) NULL,
    `TaxRatePercent` decimal(9,4) NULL,
    `ResponseGuaranteeAmount` decimal(18,2) NULL,
    `QuoteMode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `SettlementMode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `PlannedStartDate` datetime(6) NULL,
    `PlannedCompletionDate` datetime(6) NULL,
    `ServicePeriodDays` int NULL,
    `ServicePeriodText` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `QualificationRequirement` text CHARACTER SET utf8mb4 NOT NULL,
    `PerformanceRequirement` text CHARACTER SET utf8mb4 NOT NULL,
    `PersonnelRequirement` text CHARACTER SET utf8mb4 NOT NULL,
    `OtherRequirement` text CHARACTER SET utf8mb4 NOT NULL,
    `JointVentureAllowed` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `SubcontractAllowed` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `AwardLimit` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `TechnicalSpecId` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
    `ContractTemplate` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `BusinessWeight` decimal(5,2) NULL,
    `TechnicalWeight` decimal(5,2) NULL,
    `PriceWeight` decimal(5,2) NULL,
    `PriceCalculationMethod` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `PriceParameter` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `Remarks` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `OriginalHeaderJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    `OriginalRowJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    `NormalizedFieldsJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Status` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `TenantId` bigint NOT NULL,
    CONSTRAINT `PK_bidops_procurement_detail` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;
""");

        migrationBuilder.Sql("""
CREATE TABLE `bidops_lifecycle_package_link` (
    `Id` bigint NOT NULL,
    `ProcurementDetailId` bigint NULL,
    `ProcurementDetailStagingId` bigint NULL,
    `TenderPackageId` bigint NULL,
    `CandidateOutcomeRecordId` bigint NULL,
    `AwardOutcomeRecordId` bigint NULL,
    `ProcurementRawNoticeId` bigint NULL,
    `CandidateRawNoticeId` bigint NULL,
    `AwardRawNoticeId` bigint NULL,
    `ProjectCode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `ProjectName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `LotNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `LotName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `PackageNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `PackageName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `SupplierName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `SupplierNameNormalized` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `FinalAwardAmount` decimal(18,2) NULL,
    `FinalAwardAmountSource` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `Currency` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
    `MatchScore` decimal(5,4) NOT NULL,
    `MatchType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `LinkStatus` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `RequiresManualReview` tinyint(1) NOT NULL,
    `MatchReasonsJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    `MissingFieldsJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    `EvidenceJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ManualRemark` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `ConfirmedBy` bigint NULL,
    `ConfirmedAt` datetime(6) NULL,
    `SourceHash` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `TenantId` bigint NOT NULL,
    CONSTRAINT `PK_bidops_lifecycle_package_link` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;
""");

        migrationBuilder.CreateIndex(
            name: "IX_bidops_proc_detail_stg_Tenant_Notice",
            table: "bidops_procurement_detail_staging",
            columns: new[] { "TenantId", "NoticeStagingId" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_proc_detail_stg_Tenant_Pkg",
            table: "bidops_procurement_detail_staging",
            columns: new[] { "TenantId", "PackageStagingId" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_proc_detail_stg_Tenant_ProjectLotPkg",
            table: "bidops_procurement_detail_staging",
            columns: new[] { "TenantId", "ProjectCode", "LotNo", "PackageNo" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_proc_detail_stg_Tenant_SourceRow",
            table: "bidops_procurement_detail_staging",
            columns: new[] { "TenantId", "RawNoticeId", "RawAttachmentId", "TableIndex", "RowIndex" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_proc_detail_stg_Tenant_StatusCreated",
            table: "bidops_procurement_detail_staging",
            columns: new[] { "TenantId", "ReviewStatus", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_proc_detail_Tenant_Notice",
            table: "bidops_procurement_detail",
            columns: new[] { "TenantId", "NoticeId" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_proc_detail_Tenant_Pkg",
            table: "bidops_procurement_detail",
            columns: new[] { "TenantId", "TenderPackageId" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_proc_detail_Tenant_ProjectLotPkg",
            table: "bidops_procurement_detail",
            columns: new[] { "TenantId", "ProjectCode", "LotNo", "PackageNo" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_proc_detail_Tenant_SourceRow",
            table: "bidops_procurement_detail",
            columns: new[] { "TenantId", "RawNoticeId", "RawAttachmentId", "TableIndex", "RowIndex" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_proc_detail_Tenant_StatusCreated",
            table: "bidops_procurement_detail",
            columns: new[] { "TenantId", "Status", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_lifecycle_link_Tenant_Award",
            table: "bidops_lifecycle_package_link",
            columns: new[] { "TenantId", "AwardOutcomeRecordId" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_lifecycle_link_Tenant_Candidate",
            table: "bidops_lifecycle_package_link",
            columns: new[] { "TenantId", "CandidateOutcomeRecordId" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_lifecycle_link_Tenant_ProcDetail",
            table: "bidops_lifecycle_package_link",
            columns: new[] { "TenantId", "ProcurementDetailId" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_lifecycle_link_Tenant_ProcStaging",
            table: "bidops_lifecycle_package_link",
            columns: new[] { "TenantId", "ProcurementDetailStagingId" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_lifecycle_link_Tenant_ProjectLotPkg",
            table: "bidops_lifecycle_package_link",
            columns: new[] { "TenantId", "ProjectCode", "LotNo", "PackageNo" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_lifecycle_link_Tenant_SourceHash",
            table: "bidops_lifecycle_package_link",
            columns: new[] { "TenantId", "SourceHash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_bidops_lifecycle_link_Tenant_StatusReviewScore",
            table: "bidops_lifecycle_package_link",
            columns: new[] { "TenantId", "LinkStatus", "RequiresManualReview", "MatchScore" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_lifecycle_link_Tenant_TenderPkg",
            table: "bidops_lifecycle_package_link",
            columns: new[] { "TenantId", "TenderPackageId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "bidops_lifecycle_package_link");
        migrationBuilder.DropTable(name: "bidops_procurement_detail");
        migrationBuilder.DropTable(name: "bidops_procurement_detail_staging");
    }
}
