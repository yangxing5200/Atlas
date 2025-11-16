using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Global.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v002 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "DatabaseInstanceId",
                table: "Tenants",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "DatabaseInstanceId",
                table: "Tenants",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
