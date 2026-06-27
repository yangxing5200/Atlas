using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v0220bidopssuppliernamenormalized : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameNormalized",
                table: "bidops_supplier",
                type: "varchar(191)",
                maxLength: 191,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(
                """
                UPDATE `bidops_supplier`
                SET `NameNormalized` = LEFT(UPPER(
                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(TRIM(`Name`),
                        ' ', ''), '\t', ''), '\r', ''), '\n', ''),
                        '(', ''), ')', ''), '（', ''), '）', ''), '[', ''), ']', ''),
                        '【', ''), '】', ''), '{', ''), '}', ''), '<', ''), '>', ''),
                        '《', ''), '》', ''), ',', ''), '，', ''), '.', ''), '。', ''),
                        ';', ''), '；', ''), ':', ''), '：', '')
                ), 191)
                WHERE (`NameNormalized` IS NULL OR `NameNormalized` = '')
                  AND `Name` IS NOT NULL
                  AND `Name` <> '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE `bidops_supplier`
                SET `NameNormalized` = LEFT(
                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`NameNormalized`,
                        '-', ''), '_', ''), '—', ''), '–', ''), '/', ''), '\\', ''), '|', ''),
                    191)
                WHERE `NameNormalized` <> '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_bidops_supplier_TenantId_NameNormalized",
                table: "bidops_supplier",
                columns: new[] { "TenantId", "NameNormalized" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bidops_supplier_TenantId_NameNormalized",
                table: "bidops_supplier");

            migrationBuilder.DropColumn(
                name: "NameNormalized",
                table: "bidops_supplier");
        }
    }
}
