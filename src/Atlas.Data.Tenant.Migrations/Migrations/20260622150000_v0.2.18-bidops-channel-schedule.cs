using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations;

[Migration("20260622150000_v0.2.18-bidops-channel-schedule")]
public partial class v0218bidopschannelschedule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ScheduleMode",
            table: "bidops_crawl_channel",
            type: "varchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Interval")
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<int>(
            name: "ScanIntervalMinutes",
            table: "bidops_crawl_channel",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DailyScanTime",
            table: "bidops_crawl_channel",
            type: "varchar(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "")
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ScheduleMode", table: "bidops_crawl_channel");
        migrationBuilder.DropColumn(name: "ScanIntervalMinutes", table: "bidops_crawl_channel");
        migrationBuilder.DropColumn(name: "DailyScanTime", table: "bidops_crawl_channel");
    }
}
