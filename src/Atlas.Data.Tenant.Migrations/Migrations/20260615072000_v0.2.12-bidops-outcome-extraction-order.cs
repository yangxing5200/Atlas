using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations;

[Migration("20260615072000_v0.2.12-bidops-outcome-extraction-order")]
public partial class v0212bidopsoutcomeextractionorder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ExtractionOrder",
            table: "bidops_outcome_supplier_record",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.Sql("DELETE FROM `bidops_outcome_supplier_record`;");

        migrationBuilder.CreateIndex(
            name: "IX_bidops_outcome_record_Tenant_RawNotice_Order",
            table: "bidops_outcome_supplier_record",
            columns: new[] { "TenantId", "RawNoticeId", "ExtractionOrder" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_bidops_outcome_record_Tenant_RawNotice_Order",
            table: "bidops_outcome_supplier_record");

        migrationBuilder.DropColumn(
            name: "ExtractionOrder",
            table: "bidops_outcome_supplier_record");
    }
}
