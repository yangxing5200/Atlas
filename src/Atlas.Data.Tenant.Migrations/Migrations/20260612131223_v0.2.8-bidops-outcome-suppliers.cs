using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v028bidopsoutcomesuppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bidops_outcome_supplier_record",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    RawNoticeId = table.Column<long>(type: "bigint", nullable: false),
                    NoticeId = table.Column<long>(type: "bigint", nullable: true),
                    TenderPackageId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: true),
                    SourceUrl = table.Column<string>(type: "varchar(1500)", maxLength: 1500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NoticeTitle = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NoticeType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BuyerName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Region = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PublishTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LotNo = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LotName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PackageNo = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PackageName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SupplierName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SupplierNameNormalized = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OutcomeType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    AwardAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvidenceText = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExtractionConfidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    SourceHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_outcome_supplier_record", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_outcome_record_Tenant_Category_Pub",
                table: "bidops_outcome_supplier_record",
                columns: new[] { "TenantId", "Category", "PublishTime" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_outcome_record_Tenant_CreatedAt",
                table: "bidops_outcome_supplier_record",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_outcome_record_Tenant_Outcome_Pub",
                table: "bidops_outcome_supplier_record",
                columns: new[] { "TenantId", "OutcomeType", "PublishTime" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_outcome_record_Tenant_PackageNo",
                table: "bidops_outcome_supplier_record",
                columns: new[] { "TenantId", "PackageNo" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_outcome_record_Tenant_ProjectCode",
                table: "bidops_outcome_supplier_record",
                columns: new[] { "TenantId", "ProjectCode" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_outcome_record_Tenant_RawNotice",
                table: "bidops_outcome_supplier_record",
                columns: new[] { "TenantId", "RawNoticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_outcome_record_Tenant_SourceHash",
                table: "bidops_outcome_supplier_record",
                columns: new[] { "TenantId", "SourceHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_outcome_record_Tenant_Supplier",
                table: "bidops_outcome_supplier_record",
                columns: new[] { "TenantId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_outcome_record_Tenant_SupplierNorm",
                table: "bidops_outcome_supplier_record",
                columns: new[] { "TenantId", "SupplierNameNormalized" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_outcome_record_Tenant_Package",
                table: "bidops_outcome_supplier_record",
                columns: new[] { "TenantId", "TenderPackageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bidops_outcome_supplier_record");
        }
    }
}
