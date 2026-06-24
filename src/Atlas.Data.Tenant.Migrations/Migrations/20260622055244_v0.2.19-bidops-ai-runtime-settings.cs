using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v0219bidopsairuntimesettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bidops_runtime_setting",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    SettingKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SettingValue = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedByUserName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bidops_runtime_setting", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_runtime_setting_TenantId",
                table: "bidops_runtime_setting",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bidops_runtime_setting_TenantId_CreatedAt",
                table: "bidops_runtime_setting",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bidops_runtime_setting_TenantId_SettingKey",
                table: "bidops_runtime_setting",
                columns: new[] { "TenantId", "SettingKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bidops_runtime_setting");
        }
    }
}
