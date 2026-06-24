using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Tenant.Migrations.Migrations;

[Migration("20260622143000_v0.2.17-bidops-crawl-progress")]
public partial class v0217bidopscrawlprogress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
CREATE TABLE `bidops_crawl_checkpoint` (
    `Id` bigint NOT NULL,
    `SourceId` bigint NOT NULL,
    `ChannelId` bigint NOT NULL,
    `Mode` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `CursorKind` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `NextCursor` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `LastSuccessfulCursor` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `RangeStartPublishTime` datetime(6) NULL,
    `RangeEndPublishTime` datetime(6) NULL,
    `HighWatermarkPublishTime` datetime(6) NULL,
    `LowWatermarkPublishTime` datetime(6) NULL,
    `TotalRemoteCount` int NULL,
    `ScannedItemCount` int NOT NULL,
    `CreatedCount` int NOT NULL,
    `ChangedCount` int NOT NULL,
    `DuplicateCount` int NOT NULL,
    `FailedItemCount` int NOT NULL,
    `RemainingEstimate` int NULL,
    `StartedAt` datetime(6) NULL,
    `LastRunAt` datetime(6) NULL,
    `CompletedAt` datetime(6) NULL,
    `PausedAt` datetime(6) NULL,
    `PauseReason` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `LastError` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `TenantId` bigint NOT NULL,
    CONSTRAINT `PK_bidops_crawl_checkpoint` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;
""");

        migrationBuilder.Sql("""
CREATE TABLE `bidops_crawl_run` (
    `Id` bigint NOT NULL,
    `SourceId` bigint NOT NULL,
    `ChannelId` bigint NOT NULL,
    `CheckpointId` bigint NULL,
    `BackgroundJobId` bigint NULL,
    `Mode` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `StartCursor` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `EndCursor` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `PageSize` int NOT NULL,
    `PageCount` int NOT NULL,
    `DiscoveredCount` int NOT NULL,
    `CreatedCount` int NOT NULL,
    `ChangedCount` int NOT NULL,
    `DuplicateCount` int NOT NULL,
    `FailedItemCount` int NOT NULL,
    `TotalRemoteCount` int NULL,
    `RemainingEstimate` int NULL,
    `StartedAt` datetime(6) NOT NULL,
    `CompletedAt` datetime(6) NULL,
    `Message` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `TenantId` bigint NOT NULL,
    CONSTRAINT `PK_bidops_crawl_run` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;
""");

        migrationBuilder.CreateIndex(
            name: "IX_bidops_crawl_checkpoint_Tenant_Channel_Mode",
            table: "bidops_crawl_checkpoint",
            columns: new[] { "TenantId", "ChannelId", "Mode" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_bidops_crawl_checkpoint_Tenant_Source_Status_Run",
            table: "bidops_crawl_checkpoint",
            columns: new[] { "TenantId", "SourceId", "Status", "LastRunAt" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_crawl_run_Tenant_Channel_Started",
            table: "bidops_crawl_run",
            columns: new[] { "TenantId", "ChannelId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_crawl_run_Tenant_Checkpoint_Started",
            table: "bidops_crawl_run",
            columns: new[] { "TenantId", "CheckpointId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_bidops_crawl_run_Tenant_BackgroundJob",
            table: "bidops_crawl_run",
            columns: new[] { "TenantId", "BackgroundJobId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "bidops_crawl_run");
        migrationBuilder.DropTable(name: "bidops_crawl_checkpoint");
    }
}
