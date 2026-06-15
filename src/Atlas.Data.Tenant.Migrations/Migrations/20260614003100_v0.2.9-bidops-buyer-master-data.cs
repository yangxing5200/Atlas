using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v029bidopsbuyermasterdata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bidops_buyer",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    BuyerNo = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NameNormalized = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UnifiedSocialCreditCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Region = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceUrl = table.Column<string>(type: "varchar(1500)", maxLength: 1500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastProjectCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastProjectName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastNoticeTitle = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Remark = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_buyer", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "BuyerId",
                table: "bidops_outcome_supplier_record",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_TenantId",
                table: "bidops_buyer",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_TenantId_BuyerNo",
                table: "bidops_buyer",
                columns: new[] { "TenantId", "BuyerNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_TenantId_CreatedAt",
                table: "bidops_buyer",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_TenantId_LastSeenAtUtc",
                table: "bidops_buyer",
                columns: new[] { "TenantId", "LastSeenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_TenantId_NameNormalized",
                table: "bidops_buyer",
                columns: new[] { "TenantId", "NameNormalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_TenantId_Status_CreatedAt",
                table: "bidops_buyer",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_buyer_TenantId_UnifiedSocialCreditCode",
                table: "bidops_buyer",
                columns: new[] { "TenantId", "UnifiedSocialCreditCode" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_outcome_record_Tenant_Buyer",
                table: "bidops_outcome_supplier_record",
                columns: new[] { "TenantId", "BuyerId" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_outcome_supplier_record_BuyerId",
                table: "bidops_outcome_supplier_record",
                column: "BuyerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bidops_outcome_record_Tenant_Buyer",
                table: "bidops_outcome_supplier_record");

            migrationBuilder.DropIndex(
                name: "IX_bidops_outcome_supplier_record_BuyerId",
                table: "bidops_outcome_supplier_record");

            migrationBuilder.DropColumn(
                name: "BuyerId",
                table: "bidops_outcome_supplier_record");

            migrationBuilder.DropTable(
                name: "bidops_buyer");
        }
    }
}
