using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v0210bidopsrevieworglinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CreatedFromNoticeId",
                table: "bidops_supplier",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedFromNoticeTitle",
                table: "bidops_supplier",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "CreatedFromRawNoticeId",
                table: "bidops_supplier",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedFromSourceUrl",
                table: "bidops_supplier",
                type: "varchar(1500)",
                maxLength: 1500,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOutcomeAtUtc",
                table: "bidops_supplier",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastOutcomeNoticeId",
                table: "bidops_supplier",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastOutcomeNoticeTitle",
                table: "bidops_supplier",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "LastOutcomeRawNoticeId",
                table: "bidops_supplier",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bidops_buyer_procurement_record",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    BuyerId = table.Column<long>(type: "bigint", nullable: false),
                    RawNoticeId = table.Column<long>(type: "bigint", nullable: false),
                    NoticeId = table.Column<long>(type: "bigint", nullable: true),
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
                    Region = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PublishTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BudgetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PackageCount = table.Column<int>(type: "int", nullable: false),
                    SourceHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Remark = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_buyer_procurement_record", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_CreatedFromNoticeId",
                table: "bidops_supplier",
                column: "CreatedFromNoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_CreatedFromRawNoticeId",
                table: "bidops_supplier",
                column: "CreatedFromRawNoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_LastOutcomeNoticeId",
                table: "bidops_supplier",
                column: "LastOutcomeNoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_LastOutcomeRawNoticeId",
                table: "bidops_supplier",
                column: "LastOutcomeRawNoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_TenantId_CreatedFromRawNoticeId",
                table: "bidops_supplier",
                columns: new[] { "TenantId", "CreatedFromRawNoticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_TenantId_LastOutcomeNoticeId",
                table: "bidops_supplier",
                columns: new[] { "TenantId", "LastOutcomeNoticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_procurement_record_BuyerId",
                table: "bidops_buyer_procurement_record",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_procurement_record_NoticeId",
                table: "bidops_buyer_procurement_record",
                column: "NoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_procurement_record_RawNoticeId",
                table: "bidops_buyer_procurement_record",
                column: "RawNoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_procurement_record_TenantId",
                table: "bidops_buyer_procurement_record",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_procurement_record_TenantId_BuyerId_PublishTime",
                table: "bidops_buyer_procurement_record",
                columns: new[] { "TenantId", "BuyerId", "PublishTime" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_procurement_record_TenantId_CreatedAt",
                table: "bidops_buyer_procurement_record",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_procurement_record_TenantId_NoticeId",
                table: "bidops_buyer_procurement_record",
                columns: new[] { "TenantId", "NoticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_procurement_record_TenantId_ProjectCode",
                table: "bidops_buyer_procurement_record",
                columns: new[] { "TenantId", "ProjectCode" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_procurement_record_TenantId_RawNoticeId",
                table: "bidops_buyer_procurement_record",
                columns: new[] { "TenantId", "RawNoticeId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_procurement_record_TenantId_SourceHash",
                table: "bidops_buyer_procurement_record",
                columns: new[] { "TenantId", "SourceHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bidops_buyer_procurement_record");

            migrationBuilder.DropIndex(
                name: "IX_bidops_supplier_CreatedFromNoticeId",
                table: "bidops_supplier");

            migrationBuilder.DropIndex(
                name: "IX_bidops_supplier_CreatedFromRawNoticeId",
                table: "bidops_supplier");

            migrationBuilder.DropIndex(
                name: "IX_bidops_supplier_LastOutcomeNoticeId",
                table: "bidops_supplier");

            migrationBuilder.DropIndex(
                name: "IX_bidops_supplier_LastOutcomeRawNoticeId",
                table: "bidops_supplier");

            migrationBuilder.DropIndex(
                name: "IX_bidops_supplier_TenantId_CreatedFromRawNoticeId",
                table: "bidops_supplier");

            migrationBuilder.DropIndex(
                name: "IX_bidops_supplier_TenantId_LastOutcomeNoticeId",
                table: "bidops_supplier");

            migrationBuilder.DropColumn(
                name: "CreatedFromNoticeId",
                table: "bidops_supplier");

            migrationBuilder.DropColumn(
                name: "CreatedFromNoticeTitle",
                table: "bidops_supplier");

            migrationBuilder.DropColumn(
                name: "CreatedFromRawNoticeId",
                table: "bidops_supplier");

            migrationBuilder.DropColumn(
                name: "CreatedFromSourceUrl",
                table: "bidops_supplier");

            migrationBuilder.DropColumn(
                name: "LastOutcomeAtUtc",
                table: "bidops_supplier");

            migrationBuilder.DropColumn(
                name: "LastOutcomeNoticeId",
                table: "bidops_supplier");

            migrationBuilder.DropColumn(
                name: "LastOutcomeNoticeTitle",
                table: "bidops_supplier");

            migrationBuilder.DropColumn(
                name: "LastOutcomeRawNoticeId",
                table: "bidops_supplier");
        }
    }
}
