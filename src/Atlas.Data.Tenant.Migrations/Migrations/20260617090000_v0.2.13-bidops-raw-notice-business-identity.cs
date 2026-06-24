using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations;

[Migration("20260617090000_v0.2.13-bidops-raw-notice-business-identity")]
public partial class v0213bidopsrawnoticebusinessidentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
UPDATE `bidops_raw_notice`
SET `SourceNoticeId` = CONCAT('url:', `DetailUrlHash`)
WHERE `SourceNoticeId` IS NULL
   OR TRIM(`SourceNoticeId`) = ''
   OR (`SourceNoticeId` NOT LIKE 'code:%' AND `SourceNoticeId` NOT LIKE 'url:%');
""");

        migrationBuilder.Sql("""
UPDATE `bidops_raw_notice` r
JOIN (
    SELECT `TenantId`, `NoticeType`, `SourceNoticeId`, MIN(`Id`) AS `KeepId`
    FROM `bidops_raw_notice`
    GROUP BY `TenantId`, `NoticeType`, `SourceNoticeId`
    HAVING COUNT(*) > 1
) d
  ON d.`TenantId` = r.`TenantId`
 AND d.`NoticeType` = r.`NoticeType`
 AND d.`SourceNoticeId` = r.`SourceNoticeId`
SET r.`SourceNoticeId` = CONCAT(LEFT(r.`SourceNoticeId`, 96), ':legacy:', r.`Id`)
WHERE r.`Id` <> d.`KeepId`;
""");

        migrationBuilder.CreateIndex(
            name: "IX_bidops_raw_notice_TenantId_NoticeType_SourceNoticeId",
            table: "bidops_raw_notice",
            columns: new[] { "TenantId", "NoticeType", "SourceNoticeId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_bidops_raw_notice_TenantId_NoticeType_SourceNoticeId",
            table: "bidops_raw_notice");
    }
}
