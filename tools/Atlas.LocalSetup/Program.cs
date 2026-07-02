using Atlas.Core.Entities.Global;
using Atlas.Core.Authorization;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Entities.Base;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.BackgroundTasks;
using Atlas.Data.Common;
using Atlas.Data.Global;
using Atlas.Data.Tenant.Context;
using Atlas.Modules.BidOps;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

const long TenantId = 100001;
const long DatabaseInstanceId = 200001;

const long HqStoreId = 110001;
const long DirectAStoreId = 110011;
const long DirectBStoreId = 110012;
const long FranchiseStoreId = 110101;

const long HqUserId = 120001;
const long DirectUserId = 120011;
const long FranchiseUserId = 120101;

const long CorePackageEntitlementId = 160001;
const long StandardPackageEntitlementId = 160002;

const long ProductReadPermissionId = 160101;
const long InventoryReadPermissionId = 160102;

const long DirectReaderRoleId = 161001;
const long FranchiseReaderRoleId = 161002;

const long DirectProductRolePermissionId = 162001;
const long DirectInventoryRolePermissionId = 162002;
const long FranchiseProductRolePermissionId = 162011;
const long FranchiseInventoryRolePermissionId = 162012;

const long DirectReaderUserRoleId = 163001;
const long FranchiseReaderUserRoleId = 163002;

const string DemoPassword = "Pass1234!";
const string BidOpsLocalPassword = "Pass1234!";

const long BidOpsTenantId = 300001;
const long BidOpsDatabaseMasterServerId = 310001;
const long BidOpsDatabaseServerConfigId = 310011;
const long BidOpsDatabaseInstanceId = 310101;
const long BidOpsCorePackageEntitlementId = 310201;
const long BidOpsStandardPackageEntitlementId = 310202;
const long BidOpsHqStoreId = 320001;
const long BidOpsAdminUserId = 320101;
const long BidOpsAdminUserStoreId = 320201;
const long BidOpsOperatorRoleId = 320301;
const long BidOpsDashboardReadPermissionId = 320400;
const long BidOpsCrawlReadPermissionId = 320401;
const long BidOpsCrawlManagePermissionId = 320402;
const long BidOpsCrawlImportPermissionId = 320403;
const long BidOpsReviewReadPermissionId = 320404;
const long BidOpsReviewApprovePermissionId = 320405;
const long BidOpsBusinessReadPermissionId = 320406;
const long BidOpsOpportunityReadPermissionId = 320407;
const long BidOpsOpportunityManagePermissionId = 320408;
const long BidOpsOpportunityWatchPermissionId = 320409;
const long BidOpsOpportunityAssessPermissionId = 320410;
const long BidOpsSupplierReadPermissionId = 320411;
const long BidOpsSupplierManagePermissionId = 320412;
const long BidOpsSupplierEvidenceReadPermissionId = 320413;
const long BidOpsSupplierEvidenceManagePermissionId = 320414;
const long BidOpsMatchingReadPermissionId = 320415;
const long BidOpsMatchingRunPermissionId = 320416;
const long BidOpsMatchingDecidePermissionId = 320417;
const long BidOpsPursuitReadPermissionId = 320418;
const long BidOpsPursuitManagePermissionId = 320419;
const long BidOpsPursuitTaskManagePermissionId = 320420;
const long BidOpsPursuitFollowRecordManagePermissionId = 320421;
const long BidOpsStateGridSourceId = 330001;
const long BidOpsStateGridTenderChannelId = 330101;
const long BidOpsStateGridProcurementChannelId = 330102;
const long BidOpsStateGridCandidateChannelId = 330103;
const long BidOpsStateGridAwardChannelId = 330104;

var command = GetCommand(args);
var globalConnection = GetOption(args, "--global")
    ?? Environment.GetEnvironmentVariable("ATLAS_GLOBAL_CONNECTION")
    ?? "Server=localhost;Port=3306;Database=atlas_global;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;";
var tenantConnection = GetOption(args, "--tenant")
    ?? Environment.GetEnvironmentVariable("ATLAS_TENANT_CONNECTION")
    ?? "Server=localhost;Port=3306;Database=atlas;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;";

Console.WriteLine($"Atlas LocalSetup command: {command}");
Console.WriteLine($"Global: {MaskConnectionString(globalConnection)}");
Console.WriteLine($"Tenant: {MaskConnectionString(tenantConnection)}");

switch (command)
{
    case "init-global":
        await InitGlobalAsync(globalConnection);
        break;
    case "create-tenant-db":
        await CreateTenantDatabaseAsync(tenantConnection);
        break;
    case "seed-demo":
    case "seed-local":
        await SeedDemoAsync(globalConnection, tenantConnection);
        break;
    case "seed-production":
        await SeedProductionAsync(globalConnection);
        break;
    case "ensure-background-job-cancellation":
        await EnsureBackgroundJobCancellationAsync(globalConnection);
        break;
    case "seed-bidops-state-grid":
        await SeedBidOpsStateGridAsync(globalConnection, tenantConnection);
        break;
    case "bidops-status":
        await PrintBidOpsStatusAsync(globalConnection, tenantConnection);
        break;
    case "inspect-bidops-sgcc-notices":
        await BidOpsSgccNoticeInspector.RunAsync(args);
        break;
    case "ensure-bidops-opportunities":
        await EnsureBidOpsOpportunityTablesAsync(tenantConnection);
        break;
    case "ensure-bidops-suppliers":
        await EnsureBidOpsSupplierTablesAsync(tenantConnection);
        break;
    case "ensure-bidops-matching":
        await EnsureBidOpsMatchingTablesAsync(tenantConnection);
        break;
    case "ensure-bidops-pursuits":
        await EnsureBidOpsPursuitTablesAsync(tenantConnection);
        break;
    case "ensure-bidops-outcomes":
        await EnsureBidOpsOutcomeTablesAsync(tenantConnection);
        break;
    case "ensure-bidops-procurement-details":
        await EnsureBidOpsProcurementDetailTablesAsync(tenantConnection);
        break;
    case "ensure-bidops-crawl-progress":
        await EnsureBidOpsCrawlProgressTablesAsync(tenantConnection);
        break;
    case "ensure-bidops-ai-settings":
        await EnsureBidOpsAiSettingsTableAsync(tenantConnection);
        break;
    case "ensure-bidops-amount-candidates":
        await EnsureBidOpsAmountCandidateTableAsync(tenantConnection);
        break;
    case "repair-bidops-data-quality":
        await RepairBidOpsDataQualityAsync(tenantConnection, args);
        break;
    case "reset-bidops-derived-data":
        await ResetBidOpsDerivedDataAsync(tenantConnection, args);
        break;
    case "approve-bidops-pending":
        await ApproveBidOpsPendingAsync(tenantConnection);
        break;
    case "cancel-bidops-crawl-jobs":
        await CancelBidOpsCrawlJobsAsync(globalConnection);
        break;
    case "reset-demo":
        await ResetDemoAsync(globalConnection, tenantConnection);
        break;
    case "help":
    case "--help":
    case "-h":
        PrintUsage();
        break;
    default:
        throw new InvalidOperationException(
            $"Unknown command '{command}'. Use: init-global, create-tenant-db, seed-demo, seed-local, seed-production, ensure-background-job-cancellation, seed-bidops-state-grid, bidops-status, inspect-bidops-sgcc-notices, ensure-bidops-opportunities, ensure-bidops-suppliers, ensure-bidops-matching, ensure-bidops-pursuits, ensure-bidops-outcomes, ensure-bidops-procurement-details, ensure-bidops-crawl-progress, ensure-bidops-ai-settings, ensure-bidops-amount-candidates, repair-bidops-data-quality, reset-bidops-derived-data, approve-bidops-pending, cancel-bidops-crawl-jobs, reset-demo.");
}

static async Task InitGlobalAsync(string globalConnection)
{
    await using var globalDb = CreateGlobalDbContext(globalConnection);
    await globalDb.Database.EnsureCreatedAsync();
    Console.WriteLine("Global database is ready.");
}

static async Task CreateTenantDatabaseAsync(string tenantConnection)
{
    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    await tenantDb.Database.EnsureCreatedAsync();
    Console.WriteLine("Tenant database is ready.");
}

static async Task EnsureBackgroundJobCancellationAsync(string globalConnection)
{
    await using var globalDb = CreateGlobalDbContext(globalConnection);
    await AddColumnIfMissingAsync(
        globalDb,
        "BackgroundJobs",
        "CancellationRequestedAt",
        "ALTER TABLE `BackgroundJobs` ADD COLUMN `CancellationRequestedAt` datetime(6) NULL;");
    await AddColumnIfMissingAsync(
        globalDb,
        "BackgroundJobs",
        "CancellationRequestedBy",
        "ALTER TABLE `BackgroundJobs` ADD COLUMN `CancellationRequestedBy` varchar(200) CHARACTER SET utf8mb4 NULL;");
    await AddColumnIfMissingAsync(
        globalDb,
        "BackgroundJobs",
        "CancellationReason",
        "ALTER TABLE `BackgroundJobs` ADD COLUMN `CancellationReason` text CHARACTER SET utf8mb4 NULL;");
    await CreateIndexIfMissingAsync(
        globalDb,
        "BackgroundJobs",
        "IX_BackgroundJobs_CancellationRequested",
        "CREATE INDEX `IX_BackgroundJobs_CancellationRequested` ON `BackgroundJobs` (`Status`, `CancellationRequestedAt`);");
    await AlterColumnIfExistsAsync(
        globalDb,
        "BackgroundJobs",
        "Result",
        "ALTER TABLE `BackgroundJobs` MODIFY COLUMN `Result` mediumtext CHARACTER SET utf8mb4 NULL;");

    Console.WriteLine("BackgroundJobs cancellation columns and diagnostic result storage are ready.");
}

static async Task SeedDemoAsync(string globalConnection, string tenantConnection)
{
    await InitGlobalAsync(globalConnection);
    await CreateTenantDatabaseAsync(tenantConnection);

    await using var globalDb = CreateGlobalDbContext(globalConnection);
    SeedGlobal(globalDb, tenantConnection);
    await globalDb.SaveChangesAsync();

    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    SeedTenant(tenantDb);
    await tenantDb.SaveChangesAsync();

    PrintDemoAccounts();
}

static async Task SeedProductionAsync(string globalConnection)
{
    await InitGlobalAsync(globalConnection);
    Console.WriteLine("Production seed completed. Demo tenants, stores, users, and products are intentionally excluded.");
}

static async Task SeedBidOpsStateGridAsync(string globalConnection, string tenantConnection)
{
    await InitGlobalAsync(globalConnection);
    await CreateTenantDatabaseAsync(tenantConnection);

    await using var globalDb = CreateGlobalDbContext(globalConnection);
    SeedBidOpsGlobal(globalDb, tenantConnection);
    var jobs = EnqueueBidOpsStateGridScans(globalDb);
    await globalDb.SaveChangesAsync();

    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    SeedBidOpsTenant(tenantDb);
    SeedBidOpsStateGridCrawler(tenantDb);
    await tenantDb.SaveChangesAsync();
    await EnsureBidOpsRawNoticeBusinessIdentityAsync(tenantDb);
    await EnsureBidOpsOutcomeTablesAsync(tenantConnection);
    await EnsureBidOpsAiSettingsTableAsync(tenantConnection);

    Console.WriteLine();
    Console.WriteLine("BidOps State Grid local runtime is ready.");
    Console.WriteLine("Login domain: bidops");
    Console.WriteLine("Account: bidops_admin");
    Console.WriteLine($"Password: {BidOpsLocalPassword}");
    Console.WriteLine($"TenantId: {BidOpsTenantId}, StoreId: {BidOpsHqStoreId}");
    Console.WriteLine($"SourceId: {BidOpsStateGridSourceId}");
    Console.WriteLine("Channels:");
    Console.WriteLine($"  {BidOpsStateGridTenderChannelId}: sgcc-tender-announcements");
    Console.WriteLine($"  {BidOpsStateGridProcurementChannelId}: sgcc-procurement-announcements");
    Console.WriteLine($"  {BidOpsStateGridCandidateChannelId}: sgcc-candidate-announcements");
    Console.WriteLine($"  {BidOpsStateGridAwardChannelId}: sgcc-award-announcements");
    Console.WriteLine("Queued jobs:");
    foreach (var job in jobs)
        Console.WriteLine($"  {job.Id}: {job.JobName} ({job.DeduplicationKey})");
}

static async Task PrintBidOpsStatusAsync(string globalConnection, string tenantConnection)
{
    await using var globalDb = CreateGlobalDbContext(globalConnection);
    await using var tenantDb = CreateTenantDbContext(tenantConnection);

    var jobs = await globalDb.BackgroundJobs
        .AsNoTracking()
        .Where(x => x.TenantId == BidOpsTenantId && x.JobType.StartsWith("bidops."))
        .OrderByDescending(x => x.CreatedAt)
        .Take(20)
        .ToListAsync();

    var sources = await tenantDb.Set<CrawlSource>().AsNoTracking().CountAsync();
    var channels = await tenantDb.Set<CrawlChannel>().AsNoTracking().CountAsync();
    var rawNotices = await tenantDb.Set<RawNotice>().AsNoTracking().CountAsync();
    var crawlLogs = await tenantDb.Set<CrawlRunLog>().AsNoTracking().CountAsync();
    var noticeStaging = await tenantDb.Set<NoticeStaging>().AsNoTracking().CountAsync();
    var reviewTasks = await tenantDb.Set<ReviewTask>().AsNoTracking().CountAsync();
    var rawAttachments = await tenantDb.Set<RawAttachment>().AsNoTracking().CountAsync();
    var formalNotices = await tenantDb.Set<Notice>().AsNoTracking().CountAsync();
    var formalPackages = await tenantDb.Set<TenderPackage>().AsNoTracking().CountAsync();
    var formalRequirements = await tenantDb.Set<RequirementItem>().AsNoTracking().CountAsync();
    var outcomeRecords = await TableExistsAsync(tenantDb, "bidops_outcome_supplier_record")
        ? await tenantDb.Set<OutcomeSupplierRecord>().AsNoTracking().CountAsync()
        : 0;
    var latestFormalNotice = await tenantDb.Set<Notice>()
        .AsNoTracking()
        .OrderByDescending(x => x.CreatedAt)
        .Select(x => new
        {
            x.Id,
            x.Title,
            x.ProjectCode,
            x.BuyerName,
            x.AgencyName,
            x.Region,
            x.SignupDeadline,
            x.BidDeadline,
            x.OpenBidTime,
            x.Status
        })
        .FirstOrDefaultAsync();
    var recentRawNotices = await tenantDb.Set<RawNotice>()
        .AsNoTracking()
        .OrderByDescending(x => x.FetchTime)
        .Take(10)
        .Select(x => new
        {
            x.Id,
            x.ChannelId,
            x.Status,
            x.SourceNoticeId,
            x.Title,
            x.DetailUrlHash,
            x.PublishTime,
            x.FetchTime
        })
        .ToListAsync();
    var recentCrawlLogs = await tenantDb.Set<CrawlRunLog>()
        .AsNoTracking()
        .OrderByDescending(x => x.CreatedAt)
        .Take(10)
        .Select(x => new
        {
            x.Id,
            x.ChannelId,
            x.BackgroundJobId,
            x.Operation,
            x.Status,
            x.Message,
            x.DurationMs,
            x.CreatedAt
        })
        .ToListAsync();

    Console.WriteLine($"BidOps status for tenant {BidOpsTenantId}");
    Console.WriteLine($"Sources={sources}, Channels={channels}, RawNotices={rawNotices}, RawAttachments={rawAttachments}, CrawlLogs={crawlLogs}, NoticeStaging={noticeStaging}, ReviewTasks={reviewTasks}");
    Console.WriteLine($"FormalNotices={formalNotices}, FormalPackages={formalPackages}, FormalRequirements={formalRequirements}");
    Console.WriteLine($"OutcomeSupplierRecords={outcomeRecords}");
    if (latestFormalNotice != null)
    {
        Console.WriteLine("Latest formal notice:");
        Console.WriteLine($"  {latestFormalNotice.Id} | {latestFormalNotice.Status} | {latestFormalNotice.ProjectCode} | {latestFormalNotice.Title}");
        Console.WriteLine($"  Buyer={latestFormalNotice.BuyerName}; Agency={latestFormalNotice.AgencyName}; Region={latestFormalNotice.Region}");
        Console.WriteLine($"  SignupDeadline={latestFormalNotice.SignupDeadline:u}; BidDeadline={latestFormalNotice.BidDeadline:u}; OpenBidTime={latestFormalNotice.OpenBidTime:u}");
    }

    Console.WriteLine("Recent raw notices:");
    foreach (var raw in recentRawNotices)
    {
        Console.WriteLine($"  {raw.Id} | channel={raw.ChannelId} | {raw.Status} | {raw.SourceNoticeId} | {raw.Title}");
        Console.WriteLine($"    hash={raw.DetailUrlHash}; publish={raw.PublishTime:u}; fetch={raw.FetchTime:u}");
    }

    Console.WriteLine("Recent crawl logs:");
    foreach (var log in recentCrawlLogs)
    {
        Console.WriteLine($"  {log.Id} | channel={log.ChannelId} | job={log.BackgroundJobId} | {log.Operation} | {log.Status} | duration={log.DurationMs}");
        Console.WriteLine($"    {log.Message}");
    }

    Console.WriteLine("Recent jobs:");
    foreach (var job in jobs)
    {
        Console.WriteLine($"  {job.Id} | {job.JobType} | {job.Queue} | {job.Status} | attempts={job.AttemptCount}/{job.MaxAttempts} | {job.Result ?? job.LastError ?? job.JobName}");
    }
}

static async Task EnsureBidOpsOpportunityTablesAsync(string tenantConnection)
{
    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    SeedBidOpsTenant(tenantDb);
    await tenantDb.SaveChangesAsync();

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_opportunity` (
  `Id` bigint NOT NULL,
  `NoticeId` bigint NOT NULL,
  `PackageId` bigint NOT NULL,
  `OpportunityNo` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Title` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `Stage` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Status` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `ActiveMarker` varchar(16) CHARACTER SET utf8mb4 NULL,
  `Priority` int NOT NULL,
  `EstimatedAmount` decimal(18,2) NULL,
  `ValueScore` decimal(6,2) NULL,
  `ValueLevel` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Decision` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `OwnerUserId` bigint NULL,
  `NextActionAtUtc` datetime(6) NULL,
  `LastStageChangedAtUtc` datetime(6) NOT NULL,
  `AssessmentSummary` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
  `Remark` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_opportunity_stage_history` (
  `Id` bigint NOT NULL,
  `OpportunityId` bigint NOT NULL,
  `FromStage` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `ToStage` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Reason` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
  `OperatorUserId` bigint NULL,
  `OccurredAtUtc` datetime(6) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_opportunity_watch` (
  `Id` bigint NOT NULL,
  `OpportunityId` bigint NOT NULL,
  `UserId` bigint NOT NULL,
  `Remark` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `Enabled` tinyint(1) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity", "IX_bidops_opportunity_NoticeId", "CREATE INDEX `IX_bidops_opportunity_NoticeId` ON `bidops_opportunity` (`NoticeId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity", "IX_bidops_opportunity_OwnerUserId", "CREATE INDEX `IX_bidops_opportunity_OwnerUserId` ON `bidops_opportunity` (`OwnerUserId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity", "IX_bidops_opportunity_PackageId", "CREATE INDEX `IX_bidops_opportunity_PackageId` ON `bidops_opportunity` (`PackageId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity", "IX_bidops_opportunity_TenantId", "CREATE INDEX `IX_bidops_opportunity_TenantId` ON `bidops_opportunity` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity", "IX_bidops_opportunity_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_opportunity_TenantId_CreatedAt` ON `bidops_opportunity` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity", "IX_bidops_opportunity_TenantId_NextActionAtUtc", "CREATE INDEX `IX_bidops_opportunity_TenantId_NextActionAtUtc` ON `bidops_opportunity` (`TenantId`, `NextActionAtUtc`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity", "IX_bidops_opportunity_TenantId_NoticeId", "CREATE INDEX `IX_bidops_opportunity_TenantId_NoticeId` ON `bidops_opportunity` (`TenantId`, `NoticeId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity", "IX_bidops_opportunity_TenantId_OpportunityNo", "CREATE UNIQUE INDEX `IX_bidops_opportunity_TenantId_OpportunityNo` ON `bidops_opportunity` (`TenantId`, `OpportunityNo`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity", "IX_bidops_opportunity_TenantId_PackageId_ActiveMarker", "CREATE UNIQUE INDEX `IX_bidops_opportunity_TenantId_PackageId_ActiveMarker` ON `bidops_opportunity` (`TenantId`, `PackageId`, `ActiveMarker`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity", "IX_bidops_opportunity_TenantId_Stage_Status_CreatedAt", "CREATE INDEX `IX_bidops_opportunity_TenantId_Stage_Status_CreatedAt` ON `bidops_opportunity` (`TenantId`, `Stage`, `Status`, `CreatedAt`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity_stage_history", "IX_bidops_opportunity_stage_history_OperatorUserId", "CREATE INDEX `IX_bidops_opportunity_stage_history_OperatorUserId` ON `bidops_opportunity_stage_history` (`OperatorUserId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity_stage_history", "IX_bidops_opportunity_stage_history_OpportunityId", "CREATE INDEX `IX_bidops_opportunity_stage_history_OpportunityId` ON `bidops_opportunity_stage_history` (`OpportunityId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity_stage_history", "IX_bidops_opportunity_stage_history_TenantId", "CREATE INDEX `IX_bidops_opportunity_stage_history_TenantId` ON `bidops_opportunity_stage_history` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity_stage_history", "IX_bidops_opportunity_stage_history_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_opportunity_stage_history_TenantId_CreatedAt` ON `bidops_opportunity_stage_history` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity_stage_history", "IX_bidops_opportunity_stage_history_TenantId_OpportunityId_Occu~", "CREATE INDEX `IX_bidops_opportunity_stage_history_TenantId_OpportunityId_Occu~` ON `bidops_opportunity_stage_history` (`TenantId`, `OpportunityId`, `OccurredAtUtc`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity_stage_history", "IX_bidops_opportunity_stage_history_TenantId_ToStage_OccurredAt~", "CREATE INDEX `IX_bidops_opportunity_stage_history_TenantId_ToStage_OccurredAt~` ON `bidops_opportunity_stage_history` (`TenantId`, `ToStage`, `OccurredAtUtc`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity_watch", "IX_bidops_opportunity_watch_OpportunityId", "CREATE INDEX `IX_bidops_opportunity_watch_OpportunityId` ON `bidops_opportunity_watch` (`OpportunityId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity_watch", "IX_bidops_opportunity_watch_TenantId", "CREATE INDEX `IX_bidops_opportunity_watch_TenantId` ON `bidops_opportunity_watch` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity_watch", "IX_bidops_opportunity_watch_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_opportunity_watch_TenantId_CreatedAt` ON `bidops_opportunity_watch` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity_watch", "IX_bidops_opportunity_watch_TenantId_OpportunityId_UserId", "CREATE UNIQUE INDEX `IX_bidops_opportunity_watch_TenantId_OpportunityId_UserId` ON `bidops_opportunity_watch` (`TenantId`, `OpportunityId`, `UserId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity_watch", "IX_bidops_opportunity_watch_TenantId_UserId_Enabled", "CREATE INDEX `IX_bidops_opportunity_watch_TenantId_UserId_Enabled` ON `bidops_opportunity_watch` (`TenantId`, `UserId`, `Enabled`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_opportunity_watch", "IX_bidops_opportunity_watch_UserId", "CREATE INDEX `IX_bidops_opportunity_watch_UserId` ON `bidops_opportunity_watch` (`UserId`);");

    Console.WriteLine("BidOps opportunity local tables and permissions are ready.");
}

static async Task EnsureBidOpsSupplierTablesAsync(string tenantConnection)
{
    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    SeedBidOpsTenant(tenantDb);
    await tenantDb.SaveChangesAsync();

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_supplier` (
  `Id` bigint NOT NULL,
  `SupplierNo` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Name` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `UnifiedSocialCreditCode` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Region` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `Address` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `ContactName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `ContactPhone` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `ContactEmail` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
  `Status` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `QualityScore` decimal(6,2) NULL,
  `Remark` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedFromRawNoticeId` bigint NULL,
  `CreatedFromNoticeId` bigint NULL,
  `CreatedFromNoticeTitle` varchar(500) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
  `CreatedFromSourceUrl` varchar(1500) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
  `LastOutcomeRawNoticeId` bigint NULL,
  `LastOutcomeNoticeId` bigint NULL,
  `LastOutcomeNoticeTitle` varchar(500) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
  `LastOutcomeAtUtc` datetime(6) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await AddColumnIfMissingAsync(tenantDb, "bidops_supplier", "CreatedFromRawNoticeId", "ALTER TABLE `bidops_supplier` ADD COLUMN `CreatedFromRawNoticeId` bigint NULL;");
    await AddColumnIfMissingAsync(tenantDb, "bidops_supplier", "CreatedFromNoticeId", "ALTER TABLE `bidops_supplier` ADD COLUMN `CreatedFromNoticeId` bigint NULL;");
    await AddColumnIfMissingAsync(tenantDb, "bidops_supplier", "CreatedFromNoticeTitle", "ALTER TABLE `bidops_supplier` ADD COLUMN `CreatedFromNoticeTitle` varchar(500) CHARACTER SET utf8mb4 NOT NULL DEFAULT '';");
    await AddColumnIfMissingAsync(tenantDb, "bidops_supplier", "CreatedFromSourceUrl", "ALTER TABLE `bidops_supplier` ADD COLUMN `CreatedFromSourceUrl` varchar(1500) CHARACTER SET utf8mb4 NOT NULL DEFAULT '';");
    await AddColumnIfMissingAsync(tenantDb, "bidops_supplier", "LastOutcomeRawNoticeId", "ALTER TABLE `bidops_supplier` ADD COLUMN `LastOutcomeRawNoticeId` bigint NULL;");
    await AddColumnIfMissingAsync(tenantDb, "bidops_supplier", "LastOutcomeNoticeId", "ALTER TABLE `bidops_supplier` ADD COLUMN `LastOutcomeNoticeId` bigint NULL;");
    await AddColumnIfMissingAsync(tenantDb, "bidops_supplier", "LastOutcomeNoticeTitle", "ALTER TABLE `bidops_supplier` ADD COLUMN `LastOutcomeNoticeTitle` varchar(500) CHARACTER SET utf8mb4 NOT NULL DEFAULT '';");
    await AddColumnIfMissingAsync(tenantDb, "bidops_supplier", "LastOutcomeAtUtc", "ALTER TABLE `bidops_supplier` ADD COLUMN `LastOutcomeAtUtc` datetime(6) NULL;");

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_buyer` (
  `Id` bigint NOT NULL,
  `BuyerNo` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Name` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `NameNormalized` varchar(191) CHARACTER SET utf8mb4 NOT NULL,
  `UnifiedSocialCreditCode` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Region` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `SourceUrl` varchar(1500) CHARACTER SET utf8mb4 NOT NULL,
  `LastProjectCode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `LastProjectName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `LastNoticeTitle` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `LastSeenAtUtc` datetime(6) NULL,
  `Status` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Remark` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_buyer_procurement_record` (
  `Id` bigint NOT NULL,
  `BuyerId` bigint NOT NULL,
  `RawNoticeId` bigint NOT NULL,
  `NoticeId` bigint NULL,
  `SourceUrl` varchar(1500) CHARACTER SET utf8mb4 NOT NULL,
  `NoticeTitle` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `NoticeType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `ProjectName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `ProjectCode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `Region` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `PublishTime` datetime(6) NULL,
  `BudgetAmount` decimal(18,2) NULL,
  `PackageCount` int NOT NULL,
  `SourceHash` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Remark` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_supplier_capability` (
  `Id` bigint NOT NULL,
  `SupplierId` bigint NOT NULL,
  `Category` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `ProductLine` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
  `CapabilityTags` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
  `RegionScope` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `QualificationLevel` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `Remark` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_supplier_contact` (
  `Id` bigint NOT NULL,
  `SupplierId` bigint NOT NULL,
  `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `Role` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `Phone` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Email` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
  `IsPrimary` tinyint(1) NOT NULL,
  `Remark` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_supplier_evidence_document` (
  `Id` bigint NOT NULL,
  `SupplierId` bigint NOT NULL,
  `DocumentName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `DocumentType` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `EvidenceNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `IssuedBy` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `ValidFrom` datetime(6) NULL,
  `ValidTo` datetime(6) NULL,
  `FileName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `FileUrl` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
  `StorageProvider` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `StorageKey` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `Status` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Remark` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier", "IX_bidops_supplier_TenantId", "CREATE INDEX `IX_bidops_supplier_TenantId` ON `bidops_supplier` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier", "IX_bidops_supplier_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_supplier_TenantId_CreatedAt` ON `bidops_supplier` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier", "IX_bidops_supplier_TenantId_Status_CreatedAt", "CREATE INDEX `IX_bidops_supplier_TenantId_Status_CreatedAt` ON `bidops_supplier` (`TenantId`, `Status`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier", "IX_bidops_supplier_TenantId_SupplierNo", "CREATE UNIQUE INDEX `IX_bidops_supplier_TenantId_SupplierNo` ON `bidops_supplier` (`TenantId`, `SupplierNo`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier", "IX_bidops_supplier_TenantId_UnifiedSocialCreditCode", "CREATE INDEX `IX_bidops_supplier_TenantId_UnifiedSocialCreditCode` ON `bidops_supplier` (`TenantId`, `UnifiedSocialCreditCode`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier", "IX_bidops_supplier_TenantId_CreatedFromRawNoticeId", "CREATE INDEX `IX_bidops_supplier_TenantId_CreatedFromRawNoticeId` ON `bidops_supplier` (`TenantId`, `CreatedFromRawNoticeId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier", "IX_bidops_supplier_TenantId_LastOutcomeNoticeId", "CREATE INDEX `IX_bidops_supplier_TenantId_LastOutcomeNoticeId` ON `bidops_supplier` (`TenantId`, `LastOutcomeNoticeId`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_buyer", "IX_bidops_buyer_TenantId", "CREATE INDEX `IX_bidops_buyer_TenantId` ON `bidops_buyer` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_buyer", "IX_bidops_buyer_TenantId_BuyerNo", "CREATE UNIQUE INDEX `IX_bidops_buyer_TenantId_BuyerNo` ON `bidops_buyer` (`TenantId`, `BuyerNo`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_buyer", "IX_bidops_buyer_TenantId_NameNormalized", "CREATE UNIQUE INDEX `IX_bidops_buyer_TenantId_NameNormalized` ON `bidops_buyer` (`TenantId`, `NameNormalized`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_buyer", "IX_bidops_buyer_TenantId_UnifiedSocialCreditCode", "CREATE INDEX `IX_bidops_buyer_TenantId_UnifiedSocialCreditCode` ON `bidops_buyer` (`TenantId`, `UnifiedSocialCreditCode`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_buyer", "IX_bidops_buyer_TenantId_Status_CreatedAt", "CREATE INDEX `IX_bidops_buyer_TenantId_Status_CreatedAt` ON `bidops_buyer` (`TenantId`, `Status`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_buyer", "IX_bidops_buyer_TenantId_LastSeenAtUtc", "CREATE INDEX `IX_bidops_buyer_TenantId_LastSeenAtUtc` ON `bidops_buyer` (`TenantId`, `LastSeenAtUtc`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_buyer_procurement_record", "IX_bidops_buyer_procurement_record_TenantId", "CREATE INDEX `IX_bidops_buyer_procurement_record_TenantId` ON `bidops_buyer_procurement_record` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_buyer_procurement_record", "IX_bidops_buyer_procurement_record_TenantId_BuyerId_PublishTime", "CREATE INDEX `IX_bidops_buyer_procurement_record_TenantId_BuyerId_PublishTime` ON `bidops_buyer_procurement_record` (`TenantId`, `BuyerId`, `PublishTime`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_buyer_procurement_record", "IX_bidops_buyer_procurement_record_TenantId_RawNoticeId", "CREATE INDEX `IX_bidops_buyer_procurement_record_TenantId_RawNoticeId` ON `bidops_buyer_procurement_record` (`TenantId`, `RawNoticeId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_buyer_procurement_record", "IX_bidops_buyer_procurement_record_TenantId_NoticeId", "CREATE INDEX `IX_bidops_buyer_procurement_record_TenantId_NoticeId` ON `bidops_buyer_procurement_record` (`TenantId`, `NoticeId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_buyer_procurement_record", "IX_bidops_buyer_procurement_record_TenantId_ProjectCode", "CREATE INDEX `IX_bidops_buyer_procurement_record_TenantId_ProjectCode` ON `bidops_buyer_procurement_record` (`TenantId`, `ProjectCode`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_buyer_procurement_record", "IX_bidops_buyer_procurement_record_TenantId_SourceHash", "CREATE UNIQUE INDEX `IX_bidops_buyer_procurement_record_TenantId_SourceHash` ON `bidops_buyer_procurement_record` (`TenantId`, `SourceHash`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_capability", "IX_bidops_supplier_capability_SupplierId", "CREATE INDEX `IX_bidops_supplier_capability_SupplierId` ON `bidops_supplier_capability` (`SupplierId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_capability", "IX_bidops_supplier_capability_TenantId", "CREATE INDEX `IX_bidops_supplier_capability_TenantId` ON `bidops_supplier_capability` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_capability", "IX_bidops_supplier_capability_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_supplier_capability_TenantId_CreatedAt` ON `bidops_supplier_capability` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_capability", "IX_bidops_supplier_capability_TenantId_SupplierId_Category", "CREATE INDEX `IX_bidops_supplier_capability_TenantId_SupplierId_Category` ON `bidops_supplier_capability` (`TenantId`, `SupplierId`, `Category`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_contact", "IX_bidops_supplier_contact_SupplierId", "CREATE INDEX `IX_bidops_supplier_contact_SupplierId` ON `bidops_supplier_contact` (`SupplierId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_contact", "IX_bidops_supplier_contact_TenantId", "CREATE INDEX `IX_bidops_supplier_contact_TenantId` ON `bidops_supplier_contact` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_contact", "IX_bidops_supplier_contact_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_supplier_contact_TenantId_CreatedAt` ON `bidops_supplier_contact` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_contact", "IX_bidops_supplier_contact_TenantId_SupplierId_IsPrimary", "CREATE INDEX `IX_bidops_supplier_contact_TenantId_SupplierId_IsPrimary` ON `bidops_supplier_contact` (`TenantId`, `SupplierId`, `IsPrimary`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_contact", "IX_bidops_supplier_contact_TenantId_SupplierId_Name", "CREATE INDEX `IX_bidops_supplier_contact_TenantId_SupplierId_Name` ON `bidops_supplier_contact` (`TenantId`, `SupplierId`, `Name`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_evidence_document", "IX_bidops_supplier_evidence_document_SupplierId", "CREATE INDEX `IX_bidops_supplier_evidence_document_SupplierId` ON `bidops_supplier_evidence_document` (`SupplierId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_evidence_document", "IX_bidops_supplier_evidence_document_TenantId", "CREATE INDEX `IX_bidops_supplier_evidence_document_TenantId` ON `bidops_supplier_evidence_document` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_evidence_document", "IX_bidops_supplier_evidence_document_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_supplier_evidence_document_TenantId_CreatedAt` ON `bidops_supplier_evidence_document` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_evidence_document", "IX_bidops_supplier_evidence_document_TenantId_Status_ValidTo", "CREATE INDEX `IX_bidops_supplier_evidence_document_TenantId_Status_ValidTo` ON `bidops_supplier_evidence_document` (`TenantId`, `Status`, `ValidTo`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_evidence_document", "IX_bidops_supplier_evidence_document_TenantId_SupplierId_Docume~", "CREATE INDEX `IX_bidops_supplier_evidence_document_TenantId_SupplierId_Docume~` ON `bidops_supplier_evidence_document` (`TenantId`, `SupplierId`, `DocumentType`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_evidence_document", "IX_bidops_supplier_evidence_document_TenantId_ValidTo", "CREATE INDEX `IX_bidops_supplier_evidence_document_TenantId_ValidTo` ON `bidops_supplier_evidence_document` (`TenantId`, `ValidTo`);");

    Console.WriteLine("BidOps supplier local tables and permissions are ready.");
}

static async Task EnsureBidOpsMatchingTablesAsync(string tenantConnection)
{
    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    SeedBidOpsTenant(tenantDb);
    await tenantDb.SaveChangesAsync();

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_supplier_match_run` (
  `Id` bigint NOT NULL,
  `PackageId` bigint NOT NULL,
  `BackgroundJobId` bigint NULL,
  `RunNo` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Status` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `RequestedByUserId` bigint NOT NULL,
  `RequestedByUserName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `CriteriaSummary` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
  `MaxSuppliers` int NOT NULL,
  `SupplierCount` int NOT NULL,
  `MatchedCount` int NOT NULL,
  `MissingEvidenceCount` int NOT NULL,
  `StartedAtUtc` datetime(6) NULL,
  `CompletedAtUtc` datetime(6) NULL,
  `ErrorMessage` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_supplier_match_result` (
  `Id` bigint NOT NULL,
  `RunId` bigint NOT NULL,
  `PackageId` bigint NOT NULL,
  `SupplierId` bigint NOT NULL,
  `SupplierNameSnapshot` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `Rank` int NOT NULL,
  `Score` decimal(6,2) NOT NULL,
  `MatchLevel` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Recommendation` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `CategoryMatched` tinyint(1) NOT NULL,
  `RegionMatched` tinyint(1) NOT NULL,
  `EvidenceMatchedCount` int NOT NULL,
  `MissingEvidenceCount` int NOT NULL,
  `RiskFlags` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
  `Explanation` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_missing_evidence_check` (
  `Id` bigint NOT NULL,
  `RunId` bigint NOT NULL,
  `ResultId` bigint NOT NULL,
  `PackageId` bigint NOT NULL,
  `SupplierId` bigint NOT NULL,
  `RequirementId` bigint NULL,
  `MatchedEvidenceDocumentId` bigint NULL,
  `RequiredEvidenceType` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `RequirementText` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
  `Status` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Explanation` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_go_no_go_decision` (
  `Id` bigint NOT NULL,
  `PackageId` bigint NOT NULL,
  `OpportunityId` bigint NULL,
  `MatchRunId` bigint NULL,
  `SupplierMatchResultId` bigint NULL,
  `SupplierId` bigint NULL,
  `Decision` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Reason` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
  `RiskSummary` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
  `DecidedByUserId` bigint NOT NULL,
  `DecidedByUserName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `DecidedAtUtc` datetime(6) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_match_run", "IX_bidops_supplier_match_run_TenantId", "CREATE INDEX `IX_bidops_supplier_match_run_TenantId` ON `bidops_supplier_match_run` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_match_run", "IX_bidops_supplier_match_run_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_supplier_match_run_TenantId_CreatedAt` ON `bidops_supplier_match_run` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_match_run", "IX_bidops_supplier_match_run_TenantId_BackgroundJobId", "CREATE INDEX `IX_bidops_supplier_match_run_TenantId_BackgroundJobId` ON `bidops_supplier_match_run` (`TenantId`, `BackgroundJobId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_match_run", "IX_bidops_supplier_match_run_TenantId_PackageId_CreatedAt", "CREATE INDEX `IX_bidops_supplier_match_run_TenantId_PackageId_CreatedAt` ON `bidops_supplier_match_run` (`TenantId`, `PackageId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_match_run", "IX_bidops_supplier_match_run_TenantId_RunNo", "CREATE UNIQUE INDEX `IX_bidops_supplier_match_run_TenantId_RunNo` ON `bidops_supplier_match_run` (`TenantId`, `RunNo`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_match_run", "IX_bidops_supplier_match_run_TenantId_Status_CreatedAt", "CREATE INDEX `IX_bidops_supplier_match_run_TenantId_Status_CreatedAt` ON `bidops_supplier_match_run` (`TenantId`, `Status`, `CreatedAt`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_match_result", "IX_bidops_supplier_match_result_TenantId", "CREATE INDEX `IX_bidops_supplier_match_result_TenantId` ON `bidops_supplier_match_result` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_match_result", "IX_bidops_supplier_match_result_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_supplier_match_result_TenantId_CreatedAt` ON `bidops_supplier_match_result` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_match_result", "IX_bidops_supplier_match_result_TenantId_PackageId_SupplierId", "CREATE INDEX `IX_bidops_supplier_match_result_TenantId_PackageId_SupplierId` ON `bidops_supplier_match_result` (`TenantId`, `PackageId`, `SupplierId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_match_result", "IX_bidops_supplier_match_result_TenantId_RunId_Rank", "CREATE INDEX `IX_bidops_supplier_match_result_TenantId_RunId_Rank` ON `bidops_supplier_match_result` (`TenantId`, `RunId`, `Rank`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_supplier_match_result", "IX_bidops_supplier_match_result_TenantId_SupplierId_CreatedAt", "CREATE INDEX `IX_bidops_supplier_match_result_TenantId_SupplierId_CreatedAt` ON `bidops_supplier_match_result` (`TenantId`, `SupplierId`, `CreatedAt`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_missing_evidence_check", "IX_bidops_missing_evidence_check_TenantId", "CREATE INDEX `IX_bidops_missing_evidence_check_TenantId` ON `bidops_missing_evidence_check` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_missing_evidence_check", "IX_bidops_missing_evidence_check_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_missing_evidence_check_TenantId_CreatedAt` ON `bidops_missing_evidence_check` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_missing_evidence_check", "IX_bidops_missing_evidence_check_TenantId_ResultId", "CREATE INDEX `IX_bidops_missing_evidence_check_TenantId_ResultId` ON `bidops_missing_evidence_check` (`TenantId`, `ResultId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_missing_evidence_check", "IX_bidops_missing_evidence_check_TenantId_RunId_SupplierId", "CREATE INDEX `IX_bidops_missing_evidence_check_TenantId_RunId_SupplierId` ON `bidops_missing_evidence_check` (`TenantId`, `RunId`, `SupplierId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_missing_evidence_check", "IX_bidops_missing_evidence_check_TenantId_Status_CreatedAt", "CREATE INDEX `IX_bidops_missing_evidence_check_TenantId_Status_CreatedAt` ON `bidops_missing_evidence_check` (`TenantId`, `Status`, `CreatedAt`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_go_no_go_decision", "IX_bidops_go_no_go_decision_TenantId", "CREATE INDEX `IX_bidops_go_no_go_decision_TenantId` ON `bidops_go_no_go_decision` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_go_no_go_decision", "IX_bidops_go_no_go_decision_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_go_no_go_decision_TenantId_CreatedAt` ON `bidops_go_no_go_decision` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_go_no_go_decision", "IX_bidops_go_no_go_decision_TenantId_MatchRunId", "CREATE INDEX `IX_bidops_go_no_go_decision_TenantId_MatchRunId` ON `bidops_go_no_go_decision` (`TenantId`, `MatchRunId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_go_no_go_decision", "IX_bidops_go_no_go_decision_TenantId_PackageId_DecidedAtUtc", "CREATE INDEX `IX_bidops_go_no_go_decision_TenantId_PackageId_DecidedAtUtc` ON `bidops_go_no_go_decision` (`TenantId`, `PackageId`, `DecidedAtUtc`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_go_no_go_decision", "IX_bidops_go_no_go_decision_TenantId_SupplierId", "CREATE INDEX `IX_bidops_go_no_go_decision_TenantId_SupplierId` ON `bidops_go_no_go_decision` (`TenantId`, `SupplierId`);");

    Console.WriteLine("BidOps matching local tables and permissions are ready.");
}

static async Task EnsureBidOpsPursuitTablesAsync(string tenantConnection)
{
    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    SeedBidOpsTenant(tenantDb);
    await tenantDb.SaveChangesAsync();

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_pursuit` (
  `Id` bigint NOT NULL,
  `NoticeId` bigint NOT NULL,
  `PackageId` bigint NOT NULL,
  `OpportunityId` bigint NULL,
  `GoNoGoDecisionId` bigint NULL,
  `SupplierId` bigint NULL,
  `SupplierNameSnapshot` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `PursuitNo` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Title` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `Stage` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Status` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `ActiveMarker` varchar(16) CHARACTER SET utf8mb4 NULL,
  `Priority` int NOT NULL,
  `EstimatedAmount` decimal(18,2) NULL,
  `BidDeadlineAtUtc` datetime(6) NULL,
  `OwnerUserId` bigint NULL,
  `ProgressPercent` int NOT NULL,
  `RiskLevel` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `LastStageChangedAtUtc` datetime(6) NOT NULL,
  `Remark` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_pursuit_task` (
  `Id` bigint NOT NULL,
  `PursuitId` bigint NOT NULL,
  `Title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `TaskType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Status` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Priority` int NOT NULL,
  `OwnerUserId` bigint NULL,
  `DueAtUtc` datetime(6) NULL,
  `CompletedAtUtc` datetime(6) NULL,
  `Description` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
  `ResultNote` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_pursuit_follow_record` (
  `Id` bigint NOT NULL,
  `PursuitId` bigint NOT NULL,
  `FollowType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Content` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
  `NextActionAtUtc` datetime(6) NULL,
  `CreatedByUserId` bigint NULL,
  `CreatedByUserName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_GoNoGoDecisionId", "CREATE INDEX `IX_bidops_pursuit_GoNoGoDecisionId` ON `bidops_pursuit` (`GoNoGoDecisionId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_NoticeId", "CREATE INDEX `IX_bidops_pursuit_NoticeId` ON `bidops_pursuit` (`NoticeId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_OpportunityId", "CREATE INDEX `IX_bidops_pursuit_OpportunityId` ON `bidops_pursuit` (`OpportunityId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_OwnerUserId", "CREATE INDEX `IX_bidops_pursuit_OwnerUserId` ON `bidops_pursuit` (`OwnerUserId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_PackageId", "CREATE INDEX `IX_bidops_pursuit_PackageId` ON `bidops_pursuit` (`PackageId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_SupplierId", "CREATE INDEX `IX_bidops_pursuit_SupplierId` ON `bidops_pursuit` (`SupplierId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_TenantId", "CREATE INDEX `IX_bidops_pursuit_TenantId` ON `bidops_pursuit` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_TenantId_BidDeadlineAtUtc", "CREATE INDEX `IX_bidops_pursuit_TenantId_BidDeadlineAtUtc` ON `bidops_pursuit` (`TenantId`, `BidDeadlineAtUtc`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_pursuit_TenantId_CreatedAt` ON `bidops_pursuit` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_TenantId_NoticeId", "CREATE INDEX `IX_bidops_pursuit_TenantId_NoticeId` ON `bidops_pursuit` (`TenantId`, `NoticeId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_TenantId_OpportunityId", "CREATE INDEX `IX_bidops_pursuit_TenantId_OpportunityId` ON `bidops_pursuit` (`TenantId`, `OpportunityId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_TenantId_OwnerUserId_Status", "CREATE INDEX `IX_bidops_pursuit_TenantId_OwnerUserId_Status` ON `bidops_pursuit` (`TenantId`, `OwnerUserId`, `Status`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_TenantId_PackageId_ActiveMarker", "CREATE UNIQUE INDEX `IX_bidops_pursuit_TenantId_PackageId_ActiveMarker` ON `bidops_pursuit` (`TenantId`, `PackageId`, `ActiveMarker`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_TenantId_PursuitNo", "CREATE UNIQUE INDEX `IX_bidops_pursuit_TenantId_PursuitNo` ON `bidops_pursuit` (`TenantId`, `PursuitNo`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit", "IX_bidops_pursuit_TenantId_Stage_Status_CreatedAt", "CREATE INDEX `IX_bidops_pursuit_TenantId_Stage_Status_CreatedAt` ON `bidops_pursuit` (`TenantId`, `Stage`, `Status`, `CreatedAt`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit_task", "IX_bidops_pursuit_task_OwnerUserId", "CREATE INDEX `IX_bidops_pursuit_task_OwnerUserId` ON `bidops_pursuit_task` (`OwnerUserId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit_task", "IX_bidops_pursuit_task_PursuitId", "CREATE INDEX `IX_bidops_pursuit_task_PursuitId` ON `bidops_pursuit_task` (`PursuitId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit_task", "IX_bidops_pursuit_task_TenantId", "CREATE INDEX `IX_bidops_pursuit_task_TenantId` ON `bidops_pursuit_task` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit_task", "IX_bidops_pursuit_task_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_pursuit_task_TenantId_CreatedAt` ON `bidops_pursuit_task` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit_task", "IX_bidops_pursuit_task_TenantId_DueAtUtc", "CREATE INDEX `IX_bidops_pursuit_task_TenantId_DueAtUtc` ON `bidops_pursuit_task` (`TenantId`, `DueAtUtc`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit_task", "IX_bidops_pursuit_task_TenantId_OwnerUserId_Status_DueAtUtc", "CREATE INDEX `IX_bidops_pursuit_task_TenantId_OwnerUserId_Status_DueAtUtc` ON `bidops_pursuit_task` (`TenantId`, `OwnerUserId`, `Status`, `DueAtUtc`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit_task", "IX_bidops_pursuit_task_TenantId_PursuitId_Status", "CREATE INDEX `IX_bidops_pursuit_task_TenantId_PursuitId_Status` ON `bidops_pursuit_task` (`TenantId`, `PursuitId`, `Status`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit_follow_record", "IX_bidops_pursuit_follow_record_CreatedByUserId", "CREATE INDEX `IX_bidops_pursuit_follow_record_CreatedByUserId` ON `bidops_pursuit_follow_record` (`CreatedByUserId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit_follow_record", "IX_bidops_pursuit_follow_record_PursuitId", "CREATE INDEX `IX_bidops_pursuit_follow_record_PursuitId` ON `bidops_pursuit_follow_record` (`PursuitId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit_follow_record", "IX_bidops_pursuit_follow_record_TenantId", "CREATE INDEX `IX_bidops_pursuit_follow_record_TenantId` ON `bidops_pursuit_follow_record` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit_follow_record", "IX_bidops_pursuit_follow_record_TenantId_CreatedAt", "CREATE INDEX `IX_bidops_pursuit_follow_record_TenantId_CreatedAt` ON `bidops_pursuit_follow_record` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit_follow_record", "IX_bidops_pursuit_follow_record_TenantId_NextActionAtUtc", "CREATE INDEX `IX_bidops_pursuit_follow_record_TenantId_NextActionAtUtc` ON `bidops_pursuit_follow_record` (`TenantId`, `NextActionAtUtc`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_pursuit_follow_record", "IX_bidops_pursuit_follow_record_TenantId_PursuitId_CreatedAt", "CREATE INDEX `IX_bidops_pursuit_follow_record_TenantId_PursuitId_CreatedAt` ON `bidops_pursuit_follow_record` (`TenantId`, `PursuitId`, `CreatedAt`);");

    Console.WriteLine("BidOps pursuit local tables and permissions are ready.");
}

static async Task EnsureBidOpsOutcomeTablesAsync(string tenantConnection)
{
    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    SeedBidOpsTenant(tenantDb);
    await tenantDb.SaveChangesAsync();

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_outcome_supplier_record` (
  `Id` bigint NOT NULL,
  `RawNoticeId` bigint NOT NULL,
  `NoticeId` bigint NULL,
  `TenderPackageId` bigint NULL,
  `BuyerId` bigint NULL,
  `SupplierId` bigint NULL,
  `SourceUrl` varchar(1500) CHARACTER SET utf8mb4 NOT NULL,
  `NoticeTitle` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `NoticeType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `ProjectName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `ProjectCode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `BuyerName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `Region` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `PublishTime` datetime(6) NULL,
  `LotNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `LotName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `PackageNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `PackageName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
  `Category` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `SupplierName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
  `SupplierNameNormalized` varchar(191) CHARACTER SET utf8mb4 NOT NULL,
  `OutcomeType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `Rank` int NULL,
  `AwardAmount` decimal(18,2) NULL,
  `ProcurementAgencyServiceFeeAmount` decimal(18,2) NULL,
  `ExtractionOrder` int NOT NULL DEFAULT 0,
  `Currency` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
  `EvidenceText` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
  `ExtractionConfidence` decimal(5,4) NOT NULL,
  `SourceHash` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `TenantId` bigint NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await AddColumnIfMissingAsync(
        tenantDb,
        "bidops_outcome_supplier_record",
        "BuyerId",
        "ALTER TABLE `bidops_outcome_supplier_record` ADD COLUMN `BuyerId` bigint NULL AFTER `TenderPackageId`;");

    await AddColumnIfMissingAsync(
        tenantDb,
        "bidops_outcome_supplier_record",
        "ProcurementAgencyServiceFeeAmount",
        "ALTER TABLE `bidops_outcome_supplier_record` ADD COLUMN `ProcurementAgencyServiceFeeAmount` decimal(18,2) NULL AFTER `AwardAmount`;");

    var addedExtractionOrderColumn = await AddColumnIfMissingAsync(
        tenantDb,
        "bidops_outcome_supplier_record",
        "ExtractionOrder",
        "ALTER TABLE `bidops_outcome_supplier_record` ADD COLUMN `ExtractionOrder` int NOT NULL DEFAULT 0 AFTER `ProcurementAgencyServiceFeeAmount`;");

    if (addedExtractionOrderColumn)
    {
        var deletedRows = await tenantDb.Database.ExecuteSqlRawAsync("DELETE FROM `bidops_outcome_supplier_record`;");
        Console.WriteLine($"Deleted {deletedRows} existing BidOps outcome supplier records because ExtractionOrder is now required.");
    }

    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_record_Tenant", "CREATE INDEX `IX_bidops_outcome_record_Tenant` ON `bidops_outcome_supplier_record` (`TenantId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_record_Tenant_CreatedAt", "CREATE INDEX `IX_bidops_outcome_record_Tenant_CreatedAt` ON `bidops_outcome_supplier_record` (`TenantId`, `CreatedAt`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_record_Tenant_Category_Pub", "CREATE INDEX `IX_bidops_outcome_record_Tenant_Category_Pub` ON `bidops_outcome_supplier_record` (`TenantId`, `Category`, `PublishTime`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_record_Tenant_Outcome_Pub", "CREATE INDEX `IX_bidops_outcome_record_Tenant_Outcome_Pub` ON `bidops_outcome_supplier_record` (`TenantId`, `OutcomeType`, `PublishTime`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_record_Tenant_PackageNo", "CREATE INDEX `IX_bidops_outcome_record_Tenant_PackageNo` ON `bidops_outcome_supplier_record` (`TenantId`, `PackageNo`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_record_Tenant_ProjectCode", "CREATE INDEX `IX_bidops_outcome_record_Tenant_ProjectCode` ON `bidops_outcome_supplier_record` (`TenantId`, `ProjectCode`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_record_Tenant_RawNotice", "CREATE INDEX `IX_bidops_outcome_record_Tenant_RawNotice` ON `bidops_outcome_supplier_record` (`TenantId`, `RawNoticeId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_record_Tenant_RawNotice_Order", "CREATE INDEX `IX_bidops_outcome_record_Tenant_RawNotice_Order` ON `bidops_outcome_supplier_record` (`TenantId`, `RawNoticeId`, `ExtractionOrder`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_record_Tenant_SourceHash", "CREATE UNIQUE INDEX `IX_bidops_outcome_record_Tenant_SourceHash` ON `bidops_outcome_supplier_record` (`TenantId`, `SourceHash`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_supplier_record_BuyerId", "CREATE INDEX `IX_bidops_outcome_supplier_record_BuyerId` ON `bidops_outcome_supplier_record` (`BuyerId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_record_Tenant_Buyer", "CREATE INDEX `IX_bidops_outcome_record_Tenant_Buyer` ON `bidops_outcome_supplier_record` (`TenantId`, `BuyerId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_record_Tenant_Supplier", "CREATE INDEX `IX_bidops_outcome_record_Tenant_Supplier` ON `bidops_outcome_supplier_record` (`TenantId`, `SupplierId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_record_Tenant_SupplierNorm", "CREATE INDEX `IX_bidops_outcome_record_Tenant_SupplierNorm` ON `bidops_outcome_supplier_record` (`TenantId`, `SupplierNameNormalized`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_outcome_supplier_record", "IX_bidops_outcome_record_Tenant_Package", "CREATE INDEX `IX_bidops_outcome_record_Tenant_Package` ON `bidops_outcome_supplier_record` (`TenantId`, `TenderPackageId`);");

    Console.WriteLine("BidOps outcome supplier local tables and permissions are ready.");
}

static async Task EnsureBidOpsProcurementDetailTablesAsync(string tenantConnection)
{
    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    var migrationPath = FindRepositoryFile(
        Path.Combine(
            "src",
            "Atlas.Data.Tenant.Migrations",
            "Migrations",
            "20260618093000_v0.2.14-bidops-procurement-details.cs"));
    var migrationText = await File.ReadAllTextAsync(migrationPath);
    var matches = Regex.Matches(
        migrationText,
        "migrationBuilder\\.Sql\\(\"\"\"\\s*(?<sql>CREATE TABLE `bidops_(?:procurement_detail_staging|procurement_detail|lifecycle_package_link)`[\\s\\S]*?)\\s*\"\"\"\\);");
    if (matches.Count != 3)
        throw new InvalidOperationException($"Expected 3 BidOps procurement detail CREATE TABLE blocks, found {matches.Count} in {migrationPath}.");

    foreach (Match match in matches)
    {
        var sql = match.Groups["sql"].Value.Trim();
        var tableMatch = Regex.Match(sql, "CREATE TABLE `(?<table>[^`]+)`");
        if (!tableMatch.Success)
            throw new InvalidOperationException("Could not read table name from BidOps procurement detail migration SQL.");

        var tableName = tableMatch.Groups["table"].Value;
        if (await TableExistsAsync(tenantDb, tableName))
        {
            Console.WriteLine($"{tableName} already exists.");
            continue;
        }

        await tenantDb.Database.ExecuteSqlRawAsync(sql);
        Console.WriteLine($"Created {tableName}.");
    }

    await CreateIndexIfMissingAsync(tenantDb, "bidops_procurement_detail_staging", "IX_bidops_proc_detail_stg_Tenant_Notice", "CREATE INDEX `IX_bidops_proc_detail_stg_Tenant_Notice` ON `bidops_procurement_detail_staging` (`TenantId`, `NoticeStagingId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_procurement_detail_staging", "IX_bidops_proc_detail_stg_Tenant_Pkg", "CREATE INDEX `IX_bidops_proc_detail_stg_Tenant_Pkg` ON `bidops_procurement_detail_staging` (`TenantId`, `PackageStagingId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_procurement_detail_staging", "IX_bidops_proc_detail_stg_Tenant_ProjectLotPkg", "CREATE INDEX `IX_bidops_proc_detail_stg_Tenant_ProjectLotPkg` ON `bidops_procurement_detail_staging` (`TenantId`, `ProjectCode`, `LotNo`, `PackageNo`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_procurement_detail_staging", "IX_bidops_proc_detail_stg_Tenant_SourceRow", "CREATE INDEX `IX_bidops_proc_detail_stg_Tenant_SourceRow` ON `bidops_procurement_detail_staging` (`TenantId`, `RawNoticeId`, `RawAttachmentId`, `TableIndex`, `RowIndex`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_procurement_detail_staging", "IX_bidops_proc_detail_stg_Tenant_StatusCreated", "CREATE INDEX `IX_bidops_proc_detail_stg_Tenant_StatusCreated` ON `bidops_procurement_detail_staging` (`TenantId`, `ReviewStatus`, `CreatedAt`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_procurement_detail", "IX_bidops_proc_detail_Tenant_Notice", "CREATE INDEX `IX_bidops_proc_detail_Tenant_Notice` ON `bidops_procurement_detail` (`TenantId`, `NoticeId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_procurement_detail", "IX_bidops_proc_detail_Tenant_Pkg", "CREATE INDEX `IX_bidops_proc_detail_Tenant_Pkg` ON `bidops_procurement_detail` (`TenantId`, `TenderPackageId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_procurement_detail", "IX_bidops_proc_detail_Tenant_ProjectLotPkg", "CREATE INDEX `IX_bidops_proc_detail_Tenant_ProjectLotPkg` ON `bidops_procurement_detail` (`TenantId`, `ProjectCode`, `LotNo`, `PackageNo`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_procurement_detail", "IX_bidops_proc_detail_Tenant_SourceRow", "CREATE INDEX `IX_bidops_proc_detail_Tenant_SourceRow` ON `bidops_procurement_detail` (`TenantId`, `RawNoticeId`, `RawAttachmentId`, `TableIndex`, `RowIndex`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_procurement_detail", "IX_bidops_proc_detail_Tenant_StatusCreated", "CREATE INDEX `IX_bidops_proc_detail_Tenant_StatusCreated` ON `bidops_procurement_detail` (`TenantId`, `Status`, `CreatedAt`);");

    await CreateIndexIfMissingAsync(tenantDb, "bidops_lifecycle_package_link", "IX_bidops_lifecycle_link_Tenant_Award", "CREATE INDEX `IX_bidops_lifecycle_link_Tenant_Award` ON `bidops_lifecycle_package_link` (`TenantId`, `AwardOutcomeRecordId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_lifecycle_package_link", "IX_bidops_lifecycle_link_Tenant_Candidate", "CREATE INDEX `IX_bidops_lifecycle_link_Tenant_Candidate` ON `bidops_lifecycle_package_link` (`TenantId`, `CandidateOutcomeRecordId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_lifecycle_package_link", "IX_bidops_lifecycle_link_Tenant_ProcDetail", "CREATE INDEX `IX_bidops_lifecycle_link_Tenant_ProcDetail` ON `bidops_lifecycle_package_link` (`TenantId`, `ProcurementDetailId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_lifecycle_package_link", "IX_bidops_lifecycle_link_Tenant_ProcStaging", "CREATE INDEX `IX_bidops_lifecycle_link_Tenant_ProcStaging` ON `bidops_lifecycle_package_link` (`TenantId`, `ProcurementDetailStagingId`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_lifecycle_package_link", "IX_bidops_lifecycle_link_Tenant_ProjectLotPkg", "CREATE INDEX `IX_bidops_lifecycle_link_Tenant_ProjectLotPkg` ON `bidops_lifecycle_package_link` (`TenantId`, `ProjectCode`, `LotNo`, `PackageNo`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_lifecycle_package_link", "IX_bidops_lifecycle_link_Tenant_SourceHash", "CREATE UNIQUE INDEX `IX_bidops_lifecycle_link_Tenant_SourceHash` ON `bidops_lifecycle_package_link` (`TenantId`, `SourceHash`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_lifecycle_package_link", "IX_bidops_lifecycle_link_Tenant_StatusReviewScore", "CREATE INDEX `IX_bidops_lifecycle_link_Tenant_StatusReviewScore` ON `bidops_lifecycle_package_link` (`TenantId`, `LinkStatus`, `RequiresManualReview`, `MatchScore`);");
    await CreateIndexIfMissingAsync(tenantDb, "bidops_lifecycle_package_link", "IX_bidops_lifecycle_link_Tenant_TenderPkg", "CREATE INDEX `IX_bidops_lifecycle_link_Tenant_TenderPkg` ON `bidops_lifecycle_package_link` (`TenantId`, `TenderPackageId`);");

    Console.WriteLine("BidOps procurement detail and lifecycle link local tables are ready.");
}

static async Task EnsureBidOpsCrawlProgressTablesAsync(string tenantConnection)
{
    await using var tenantDb = CreateTenantDbContext(tenantConnection);

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_crawl_checkpoint` (
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
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_crawl_run` (
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
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await AddColumnIfMissingAsync(
        tenantDb,
        "bidops_crawl_channel",
        "ScheduleMode",
        "ALTER TABLE `bidops_crawl_channel` ADD COLUMN `ScheduleMode` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Interval';");

    await AddColumnIfMissingAsync(
        tenantDb,
        "bidops_crawl_channel",
        "ScanIntervalMinutes",
        "ALTER TABLE `bidops_crawl_channel` ADD COLUMN `ScanIntervalMinutes` int NULL;");

    await AddColumnIfMissingAsync(
        tenantDb,
        "bidops_crawl_channel",
        "DailyScanTime",
        "ALTER TABLE `bidops_crawl_channel` ADD COLUMN `DailyScanTime` varchar(16) CHARACTER SET utf8mb4 NOT NULL DEFAULT '';");

    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_crawl_checkpoint",
        "IX_bidops_crawl_checkpoint_Tenant_Channel_Mode",
        "CREATE UNIQUE INDEX `IX_bidops_crawl_checkpoint_Tenant_Channel_Mode` ON `bidops_crawl_checkpoint` (`TenantId`, `ChannelId`, `Mode`);");
    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_crawl_checkpoint",
        "IX_bidops_crawl_checkpoint_Tenant_Source_Status_Run",
        "CREATE INDEX `IX_bidops_crawl_checkpoint_Tenant_Source_Status_Run` ON `bidops_crawl_checkpoint` (`TenantId`, `SourceId`, `Status`, `LastRunAt`);");
    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_crawl_run",
        "IX_bidops_crawl_run_Tenant_Channel_Started",
        "CREATE INDEX `IX_bidops_crawl_run_Tenant_Channel_Started` ON `bidops_crawl_run` (`TenantId`, `ChannelId`, `StartedAt`);");
    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_crawl_run",
        "IX_bidops_crawl_run_Tenant_Checkpoint_Started",
        "CREATE INDEX `IX_bidops_crawl_run_Tenant_Checkpoint_Started` ON `bidops_crawl_run` (`TenantId`, `CheckpointId`, `StartedAt`);");
    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_crawl_run",
        "IX_bidops_crawl_run_Tenant_BackgroundJob",
        "CREATE INDEX `IX_bidops_crawl_run_Tenant_BackgroundJob` ON `bidops_crawl_run` (`TenantId`, `BackgroundJobId`);");

    Console.WriteLine("BidOps crawl progress local tables and channel schedule columns are ready.");
}

static async Task EnsureBidOpsAiSettingsTableAsync(string tenantConnection)
{
    await using var tenantDb = CreateTenantDbContext(tenantConnection);

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_runtime_setting` (
  `Id` bigint NOT NULL,
  `SettingKey` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `SettingValue` varchar(512) CHARACTER SET utf8mb4 NOT NULL,
  `UpdatedByUserName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TenantId` bigint NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""");

    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_runtime_setting",
        "IX_bidops_runtime_setting_TenantId",
        "CREATE INDEX `IX_bidops_runtime_setting_TenantId` ON `bidops_runtime_setting` (`TenantId`);");

    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_runtime_setting",
        "IX_bidops_runtime_setting_TenantId_CreatedAt",
        "CREATE INDEX `IX_bidops_runtime_setting_TenantId_CreatedAt` ON `bidops_runtime_setting` (`TenantId`, `CreatedAt`);");

    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_runtime_setting",
        "IX_bidops_runtime_setting_TenantId_SettingKey",
        "CREATE UNIQUE INDEX `IX_bidops_runtime_setting_TenantId_SettingKey` ON `bidops_runtime_setting` (`TenantId`, `SettingKey`);");

    Console.WriteLine("BidOps AI runtime settings local table is ready.");
}

static async Task EnsureBidOpsAmountCandidateTableAsync(string tenantConnection)
{
    await using var tenantDb = CreateTenantDbContext(tenantConnection);

    await tenantDb.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS `bidops_amount_candidate` (
    `Id` bigint NOT NULL,
    `LifecyclePackageLinkId` bigint NULL,
    `RawNoticeId` bigint NOT NULL,
    `ResultRawNoticeId` bigint NULL,
    `RawAttachmentId` bigint NULL,
    `OutcomeSupplierRecordId` bigint NULL,
    `ProcurementDetailStagingId` bigint NULL,
    `TenderPackageId` bigint NULL,
    `SourceKind` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `SourceNoticeType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `SourceTitle` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `SourceFileName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `SourceLocation` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
    `ProjectCode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `ProjectName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `LotNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `LotName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `PackageNo` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `PackageName` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `SupplierName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `AmountType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `AmountRaw` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `AmountValue` decimal(18,6) NULL,
    `AmountUnit` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `Currency` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
    `IsPotentialFinalAmount` tinyint(1) NOT NULL,
    `Confidence` decimal(5,4) NOT NULL,
    `Status` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `RejectReason` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `EvidenceText` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
    `ContextText` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `ManualRemark` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `SelectedBy` bigint NULL,
    `SelectedAt` datetime(6) NULL,
    `RejectedBy` bigint NULL,
    `RejectedAt` datetime(6) NULL,
    `SourceHash` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `TenantId` bigint NOT NULL,
    CONSTRAINT `PK_bidops_amount_candidate` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;
""");

    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_amount_candidate",
        "IX_bidops_amount_candidate_Tenant_CreatedAt",
        "CREATE INDEX `IX_bidops_amount_candidate_Tenant_CreatedAt` ON `bidops_amount_candidate` (`TenantId`, `CreatedAt`);");

    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_amount_candidate",
        "IX_bidops_amount_candidate_Tenant_SourceHash",
        "CREATE UNIQUE INDEX `IX_bidops_amount_candidate_Tenant_SourceHash` ON `bidops_amount_candidate` (`TenantId`, `SourceHash`);");

    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_amount_candidate",
        "IX_bidops_amount_candidate_Tenant_RawNotice_Status",
        "CREATE INDEX `IX_bidops_amount_candidate_Tenant_RawNotice_Status` ON `bidops_amount_candidate` (`TenantId`, `RawNoticeId`, `Status`);");

    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_amount_candidate",
        "IX_bidops_amount_candidate_Tenant_Link_Status",
        "CREATE INDEX `IX_bidops_amount_candidate_Tenant_Link_Status` ON `bidops_amount_candidate` (`TenantId`, `LifecyclePackageLinkId`, `Status`);");

    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_amount_candidate",
        "IX_bidops_amount_candidate_Tenant_Result_Package",
        "CREATE INDEX `IX_bidops_amount_candidate_Tenant_Result_Package` ON `bidops_amount_candidate` (`TenantId`, `ResultRawNoticeId`, `PackageNo`);");

    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_amount_candidate",
        "IX_bidops_amount_candidate_Tenant_OutcomeRecord",
        "CREATE INDEX `IX_bidops_amount_candidate_Tenant_OutcomeRecord` ON `bidops_amount_candidate` (`TenantId`, `OutcomeSupplierRecordId`);");

    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_amount_candidate",
        "IX_bidops_amount_candidate_Tenant_ProcDetail",
        "CREATE INDEX `IX_bidops_amount_candidate_Tenant_ProcDetail` ON `bidops_amount_candidate` (`TenantId`, `ProcurementDetailStagingId`);");

    await CreateIndexIfMissingAsync(
        tenantDb,
        "bidops_amount_candidate",
        "IX_bidops_amount_candidate_Tenant_Attachment",
        "CREATE INDEX `IX_bidops_amount_candidate_Tenant_Attachment` ON `bidops_amount_candidate` (`TenantId`, `RawAttachmentId`);");

    Console.WriteLine("BidOps amount candidate local table is ready.");
}

static async Task CreateIndexIfMissingAsync(DbContext db, string tableName, string indexName, string createSql)
{
    if (await IndexExistsAsync(db, tableName, indexName))
        return;

    await db.Database.ExecuteSqlRawAsync(createSql);
}

static async Task EnsureBidOpsRawNoticeBusinessIdentityAsync(DbContext db)
{
    if (!await TableExistsAsync(db, "bidops_raw_notice"))
        return;

    await db.Database.ExecuteSqlRawAsync("""
UPDATE `bidops_raw_notice`
SET `SourceNoticeId` = CONCAT('url:', `DetailUrlHash`)
WHERE `SourceNoticeId` IS NULL
   OR TRIM(`SourceNoticeId`) = ''
   OR (`SourceNoticeId` NOT LIKE 'code:%' AND `SourceNoticeId` NOT LIKE 'url:%');
""");

    await db.Database.ExecuteSqlRawAsync("""
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

    try
    {
        await CreateIndexIfMissingAsync(
            db,
            "bidops_raw_notice",
            "IX_bidops_raw_notice_TenantId_NoticeType_SourceNoticeId",
            "CREATE UNIQUE INDEX `IX_bidops_raw_notice_TenantId_NoticeType_SourceNoticeId` ON `bidops_raw_notice` (`TenantId`, `NoticeType`, `SourceNoticeId`);");
    }
    catch (Exception ex) when (ex.Message.Contains("Specified key was too long", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Skipped raw notice business identity unique index; current local MySQL key length limit is too low for the existing utf8mb4 columns.");
    }
}

static async Task<bool> AddColumnIfMissingAsync(DbContext db, string tableName, string columnName, string alterSql)
{
    if (!await TableExistsAsync(db, tableName) || await ColumnExistsAsync(db, tableName, columnName))
        return false;

    await db.Database.ExecuteSqlRawAsync(alterSql);
    return true;
}

static async Task<bool> AlterColumnIfExistsAsync(DbContext db, string tableName, string columnName, string alterSql)
{
    if (!await TableExistsAsync(db, tableName) || !await ColumnExistsAsync(db, tableName, columnName))
        return false;

    await db.Database.ExecuteSqlRawAsync(alterSql);
    return true;
}

static async Task<bool> ColumnExistsAsync(DbContext db, string tableName, string columnName)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
        await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = """
SELECT COUNT(1)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @tableName
  AND COLUMN_NAME = @columnName
""";

    var tableParameter = command.CreateParameter();
    tableParameter.ParameterName = "@tableName";
    tableParameter.Value = tableName;
    command.Parameters.Add(tableParameter);

    var columnParameter = command.CreateParameter();
    columnParameter.ParameterName = "@columnName";
    columnParameter.Value = columnName;
    command.Parameters.Add(columnParameter);

    var result = await command.ExecuteScalarAsync();
    return Convert.ToInt32(result) > 0;
}

static async Task<bool> IndexExistsAsync(DbContext db, string tableName, string indexName)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
        await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = """
SELECT COUNT(1)
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @tableName
  AND INDEX_NAME = @indexName
""";

    var tableParameter = command.CreateParameter();
    tableParameter.ParameterName = "@tableName";
    tableParameter.Value = tableName;
    command.Parameters.Add(tableParameter);

    var indexParameter = command.CreateParameter();
    indexParameter.ParameterName = "@indexName";
    indexParameter.Value = indexName;
    command.Parameters.Add(indexParameter);

    var result = await command.ExecuteScalarAsync();
    return Convert.ToInt32(result) > 0;
}

static async Task<bool> TableExistsAsync(DbContext db, string tableName)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
        await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = """
SELECT COUNT(1)
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @tableName
""";

    var tableParameter = command.CreateParameter();
    tableParameter.ParameterName = "@tableName";
    tableParameter.Value = tableName;
    command.Parameters.Add(tableParameter);

    var result = await command.ExecuteScalarAsync();
    return Convert.ToInt32(result) > 0;
}

static async Task<int> ExecuteIfTableExistsAsync(DbContext db, string tableName, string sql)
{
    if (!await TableExistsAsync(db, tableName))
    {
        Console.WriteLine($"Skipped {tableName}; table does not exist.");
        return 0;
    }

    return await db.Database.ExecuteSqlRawAsync(sql);
}

static async Task ResetBidOpsDerivedDataAsync(string tenantConnection, string[] args)
{
    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    var tenantIdText = GetOption(args, "--tenant-id");
    var tenantId = long.TryParse(tenantIdText, out var parsedTenantId) && parsedTenantId > 0
        ? parsedTenantId
        : BidOpsTenantId;
    var confirm = HasFlag(args, "--confirm");
    var dryRun = !confirm || HasFlag(args, "--dry-run");
    var tables = new[]
    {
        "bidops_requirement_staging",
        "bidops_package_staging",
        "bidops_review_task",
        "bidops_notice_staging",
        "bidops_requirement_item",
        "bidops_tender_package",
        "bidops_notice",
        "bidops_outcome_supplier_record",
        "bidops_buyer_procurement_record",
        "bidops_opportunity_watch",
        "bidops_opportunity_stage_history",
        "bidops_opportunity",
        "bidops_missing_evidence_check",
        "bidops_supplier_match_result",
        "bidops_go_no_go_decision",
        "bidops_supplier_match_run",
        "bidops_pursuit_follow_record",
        "bidops_pursuit_task",
        "bidops_pursuit"
    };

    Console.WriteLine($"BidOps derived data reset for tenant {tenantId}.");
    Console.WriteLine("Protected tables: bidops_raw_notice, bidops_raw_attachment, bidops_crawl_source, bidops_crawl_channel, bidops_crawl_run_log, attachment files, buyer and supplier master data.");
    Console.WriteLine(dryRun
        ? "Mode: dry-run. No rows will be deleted. Add --confirm without --dry-run to execute."
        : "Mode: confirmed delete.");

    var total = 0L;
    foreach (var table in tables)
    {
        if (!await TableExistsAsync(tenantDb, table))
        {
            Console.WriteLine($"  {table}: table missing");
            continue;
        }

        var count = await CountTenantRowsAsync(tenantDb, table, tenantId);
        total += count;
        Console.WriteLine($"  {table}: {count}");
    }

    Console.WriteLine($"Total derived rows: {total}");
    if (dryRun)
        return;

    var deleted = 0;
    foreach (var table in tables)
    {
        if (!await TableExistsAsync(tenantDb, table))
            continue;

        deleted += await DeleteTenantRowsAsync(tenantDb, table, tenantId);
    }

    Console.WriteLine($"BidOps derived data reset completed. deletedRows={deleted}");
}

static async Task<int> DeleteTenantRowsAsync(DbContext db, string tableName, long tenantId)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
        await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = $"DELETE FROM `{tableName}` WHERE `TenantId` = @tenantId";

    var tenantParameter = command.CreateParameter();
    tenantParameter.ParameterName = "@tenantId";
    tenantParameter.Value = tenantId;
    command.Parameters.Add(tenantParameter);

    return await command.ExecuteNonQueryAsync();
}

static async Task<long> CountTenantRowsAsync(DbContext db, string tableName, long tenantId)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
        await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = $"SELECT COUNT(1) FROM `{tableName}` WHERE `TenantId` = @tenantId";

    var tenantParameter = command.CreateParameter();
    tenantParameter.ParameterName = "@tenantId";
    tenantParameter.Value = tenantId;
    command.Parameters.Add(tenantParameter);

    var result = await command.ExecuteScalarAsync();
    return Convert.ToInt64(result);
}

static async Task<bool> TablesExistAsync(DbContext db, params string[] tableNames)
{
    foreach (var tableName in tableNames)
    {
        if (!await TableExistsAsync(db, tableName))
        {
            Console.WriteLine($"Skipped repair step; table {tableName} does not exist.");
            return false;
        }
    }

    return true;
}

static async Task<long> CountSqlAsync(DbContext db, string sql)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
        await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    var result = await command.ExecuteScalarAsync();
    return Convert.ToInt64(result);
}

static async Task<int> ExecuteSqlAsync(DbContext db, string sql)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
        await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    return await command.ExecuteNonQueryAsync();
}

static async Task<int> RepairBidOpsOutcomeLotIdentityQualityAsync(DbContext tenantDb, long tenantId, bool dryRun)
{
    const string normalizedEvidenceLine = "REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(TRIM(COALESCE(`EvidenceText`, '')), CHAR(13), ' '), CHAR(10), ' '), CHAR(9), ' '), '　', ' '), '  ', ' '), '  ', ' ')";
    const string leadingLotNoValue = "CASE WHEN " + normalizedEvidenceLine + " REGEXP '^[[:space:]]*[0-9]+(([.、][[:space:]]+)|[[:space:]]+)[A-Za-z0-9]{3,}([-_/][A-Za-z0-9]{2,}){2,}[[:space:]]+' THEN SUBSTRING_INDEX(SUBSTRING_INDEX(" + normalizedEvidenceLine + ", ' ', 2), ' ', -1) ELSE SUBSTRING_INDEX(" + normalizedEvidenceLine + ", ' ', 1) END";
    const string leadingLotNoPredicate = @"
TRIM(COALESCE(`LotNo`, '')) = ''
  AND " + normalizedEvidenceLine + @" REGEXP '^[[:space:]]*([0-9]+([.、]|[[:space:]]+)[[:space:]]*)?[A-Za-z0-9]{3,}([-_/][A-Za-z0-9]{2,}){2,}[[:space:]]+(包[[:space:]]*[A-Za-z0-9一二三四五六七八九十]+|第?[[:space:]]*[A-Za-z0-9一二三四五六七八九十]+[[:space:]]*包)'
";

    var affected = 0;
    var missingLotFromEvidence = 0L;
    var staleOutcomeIssues = 0L;
    var resolvedLifecycleIssues = 0L;
    var staleConflictNowMissingIssues = 0L;
    var resolvedMissingLotIssues = 0L;
    var reviewTaskSummaries = 0L;

    if (await TablesExistAsync(tenantDb, "bidops_outcome_supplier_record"))
    {
        missingLotFromEvidence = await CountSqlAsync(tenantDb, $"""
SELECT COUNT(1)
FROM `bidops_outcome_supplier_record`
WHERE `TenantId` = {tenantId}
  AND {leadingLotNoPredicate};
""");
    }

    if (await TablesExistAsync(tenantDb, "bidops_review_quality_issue", "bidops_outcome_supplier_record"))
    {
        staleOutcomeIssues = await CountSqlAsync(tenantDb, $"""
SELECT COUNT(1)
FROM `bidops_review_quality_issue` q
LEFT JOIN `bidops_outcome_supplier_record` r
  ON r.`Id` = q.`OutcomeSupplierRecordId`
 AND r.`TenantId` = q.`TenantId`
WHERE q.`TenantId` = {tenantId}
  AND q.`OutcomeSupplierRecordId` IS NOT NULL
  AND r.`Id` IS NULL;
""");

        resolvedMissingLotIssues = await CountSqlAsync(tenantDb, $"""
SELECT COUNT(1)
FROM `bidops_review_quality_issue` q
JOIN `bidops_outcome_supplier_record` r
  ON r.`Id` = q.`OutcomeSupplierRecordId`
 AND r.`TenantId` = q.`TenantId`
WHERE q.`TenantId` = {tenantId}
  AND q.`IsResolved` = 0
  AND q.`IssueType` = 'MissingLotOrPackage'
  AND q.`OutcomeSupplierRecordId` IS NOT NULL
  AND (
    TRIM(COALESCE(r.`LotNo`, '')) <> ''
    OR TRIM(COALESCE(r.`PackageNo`, '')) <> ''
  );
""");
    }

    if (await TablesExistAsync(
            tenantDb,
            "bidops_review_quality_issue",
            "bidops_outcome_supplier_record",
            "bidops_review_task",
            "bidops_notice_staging",
            "bidops_package_staging"))
    {
        resolvedLifecycleIssues = await CountSqlAsync(tenantDb, $"""
SELECT COUNT(1)
FROM (
    SELECT q.`Id`
    FROM `bidops_review_quality_issue` q
    JOIN `bidops_outcome_supplier_record` r
      ON r.`Id` = q.`OutcomeSupplierRecordId`
     AND r.`TenantId` = q.`TenantId`
    JOIN `bidops_review_task` t
      ON t.`Id` = q.`ReviewTaskId`
     AND t.`TenantId` = q.`TenantId`
    JOIN `bidops_notice_staging` n
      ON n.`Id` = t.`BizId`
     AND n.`TenantId` = q.`TenantId`
    JOIN `bidops_package_staging` p
      ON p.`NoticeStagingId` = n.`Id`
     AND p.`TenantId` = q.`TenantId`
    WHERE q.`TenantId` = {tenantId}
      AND q.`IsResolved` = 0
      AND q.`IssueType` IN ('LifecycleMatchConflict', 'LifecycleMatchMissing')
      AND q.`OutcomeSupplierRecordId` IS NOT NULL
      AND t.`BizType` = 'NoticeStaging'
      AND TRIM(COALESCE(r.`PackageNo`, '')) <> ''
      AND UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`PackageNo`, '')), ' ', ''), '　', '')) =
          UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`PackageNo`, '')), ' ', ''), '　', ''))
      AND (
        (
          TRIM(COALESCE(r.`LotNo`, '')) <> ''
          AND TRIM(COALESCE(p.`LotNo`, '')) <> ''
          AND UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotNo`, '')), ' ', ''), '　', '')) =
              UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotNo`, '')), ' ', ''), '　', ''))
        )
        OR (
          TRIM(COALESCE(r.`LotName`, '')) <> ''
          AND TRIM(COALESCE(p.`LotName`, '')) <> ''
          AND UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotName`, '')), ' ', ''), '　', '')) =
              UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotName`, '')), ' ', ''), '　', ''))
        )
      )
      AND (
        TRIM(COALESCE(r.`LotNo`, '')) = ''
        OR TRIM(COALESCE(p.`LotNo`, '')) = ''
        OR UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotNo`, '')), ' ', ''), '　', '')) =
           UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotNo`, '')), ' ', ''), '　', ''))
      )
      AND (
        TRIM(COALESCE(r.`LotName`, '')) = ''
        OR TRIM(COALESCE(p.`LotName`, '')) = ''
        OR UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotName`, '')), ' ', ''), '　', '')) =
           UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotName`, '')), ' ', ''), '　', ''))
      )
    GROUP BY q.`Id`
    HAVING COUNT(DISTINCT p.`Id`) = 1
) resolved;
""");

        staleConflictNowMissingIssues = await CountSqlAsync(tenantDb, $"""
SELECT COUNT(1)
FROM `bidops_review_quality_issue` q
JOIN `bidops_outcome_supplier_record` r
  ON r.`Id` = q.`OutcomeSupplierRecordId`
 AND r.`TenantId` = q.`TenantId`
JOIN `bidops_review_task` t
  ON t.`Id` = q.`ReviewTaskId`
 AND t.`TenantId` = q.`TenantId`
JOIN `bidops_notice_staging` n
  ON n.`Id` = t.`BizId`
 AND n.`TenantId` = q.`TenantId`
WHERE q.`TenantId` = {tenantId}
  AND q.`IsResolved` = 0
  AND q.`IssueType` = 'LifecycleMatchConflict'
  AND q.`OutcomeSupplierRecordId` IS NOT NULL
  AND t.`BizType` = 'NoticeStaging'
  AND TRIM(COALESCE(r.`PackageNo`, '')) <> ''
  AND (
    TRIM(COALESCE(r.`LotNo`, '')) <> ''
    OR TRIM(COALESCE(r.`LotName`, '')) <> ''
  )
  AND NOT EXISTS (
    SELECT 1
    FROM `bidops_package_staging` p
    WHERE p.`NoticeStagingId` = n.`Id`
      AND p.`TenantId` = q.`TenantId`
      AND TRIM(COALESCE(p.`PackageNo`, '')) <> ''
      AND UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`PackageNo`, '')), ' ', ''), '　', '')) =
          UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`PackageNo`, '')), ' ', ''), '　', ''))
      AND (
        (
          TRIM(COALESCE(r.`LotNo`, '')) <> ''
          AND TRIM(COALESCE(p.`LotNo`, '')) <> ''
          AND UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotNo`, '')), ' ', ''), '　', '')) =
              UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotNo`, '')), ' ', ''), '　', ''))
        )
        OR (
          TRIM(COALESCE(r.`LotName`, '')) <> ''
          AND TRIM(COALESCE(p.`LotName`, '')) <> ''
          AND UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotName`, '')), ' ', ''), '　', '')) =
              UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotName`, '')), ' ', ''), '　', ''))
        )
      )
      AND (
        TRIM(COALESCE(r.`LotNo`, '')) = ''
        OR TRIM(COALESCE(p.`LotNo`, '')) = ''
        OR UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotNo`, '')), ' ', ''), '　', '')) =
           UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotNo`, '')), ' ', ''), '　', ''))
      )
      AND (
        TRIM(COALESCE(r.`LotName`, '')) = ''
        OR TRIM(COALESCE(p.`LotName`, '')) = ''
        OR UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotName`, '')), ' ', ''), '　', '')) =
           UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotName`, '')), ' ', ''), '　', ''))
      )
  );
""");
    }

    if (await TablesExistAsync(tenantDb, "bidops_review_task"))
    {
        reviewTaskSummaries = await CountSqlAsync(tenantDb, $"""
SELECT COUNT(1)
FROM `bidops_review_task`
WHERE `TenantId` = {tenantId};
""");
    }

    Console.WriteLine("Outcome lot identity repair candidates:");
    Console.WriteLine($"  outcome records missing LotNo but evidence starts with LotNo: {missingLotFromEvidence}");
    Console.WriteLine($"  stale quality issues pointing to deleted outcome records: {staleOutcomeIssues}");
    Console.WriteLine($"  package-match issues now uniquely resolvable by lot/package: {resolvedLifecycleIssues}");
    Console.WriteLine($"  stale conflict issues now missing by lot/package: {staleConflictNowMissingIssues}");
    Console.WriteLine($"  missing-lot/package issues already satisfied by current record: {resolvedMissingLotIssues}");
    Console.WriteLine($"  review task summaries covered by recalculation: {reviewTaskSummaries}");

    if (dryRun)
        return 0;

    if (missingLotFromEvidence > 0)
    {
        affected += await ExecuteSqlAsync(tenantDb, $"""
UPDATE `bidops_outcome_supplier_record`
SET
  `LotNo` = LEFT({leadingLotNoValue}, 128),
  `LotName` = CASE WHEN TRIM(COALESCE(`LotName`, '')) = '' THEN '未分标段' ELSE `LotName` END,
  `UpdatedAt` = NOW(6)
WHERE `TenantId` = {tenantId}
  AND {leadingLotNoPredicate};
""");
    }

    if (staleOutcomeIssues > 0)
    {
        affected += await ExecuteSqlAsync(tenantDb, $"""
DELETE q
FROM `bidops_review_quality_issue` q
LEFT JOIN `bidops_outcome_supplier_record` r
  ON r.`Id` = q.`OutcomeSupplierRecordId`
 AND r.`TenantId` = q.`TenantId`
WHERE q.`TenantId` = {tenantId}
  AND q.`OutcomeSupplierRecordId` IS NOT NULL
  AND r.`Id` IS NULL;
""");
    }

    if (resolvedLifecycleIssues > 0)
    {
        affected += await ExecuteSqlAsync(tenantDb, $"""
DELETE q
FROM `bidops_review_quality_issue` q
JOIN (
    SELECT `Id`
    FROM (
        SELECT q.`Id`
        FROM `bidops_review_quality_issue` q
        JOIN `bidops_outcome_supplier_record` r
          ON r.`Id` = q.`OutcomeSupplierRecordId`
         AND r.`TenantId` = q.`TenantId`
        JOIN `bidops_review_task` t
          ON t.`Id` = q.`ReviewTaskId`
         AND t.`TenantId` = q.`TenantId`
        JOIN `bidops_notice_staging` n
          ON n.`Id` = t.`BizId`
         AND n.`TenantId` = q.`TenantId`
        JOIN `bidops_package_staging` p
          ON p.`NoticeStagingId` = n.`Id`
         AND p.`TenantId` = q.`TenantId`
        WHERE q.`TenantId` = {tenantId}
          AND q.`IsResolved` = 0
          AND q.`IssueType` IN ('LifecycleMatchConflict', 'LifecycleMatchMissing')
          AND q.`OutcomeSupplierRecordId` IS NOT NULL
          AND t.`BizType` = 'NoticeStaging'
          AND TRIM(COALESCE(r.`PackageNo`, '')) <> ''
          AND UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`PackageNo`, '')), ' ', ''), '　', '')) =
              UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`PackageNo`, '')), ' ', ''), '　', ''))
          AND (
            (
              TRIM(COALESCE(r.`LotNo`, '')) <> ''
              AND TRIM(COALESCE(p.`LotNo`, '')) <> ''
              AND UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotNo`, '')), ' ', ''), '　', '')) =
                  UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotNo`, '')), ' ', ''), '　', ''))
            )
            OR (
              TRIM(COALESCE(r.`LotName`, '')) <> ''
              AND TRIM(COALESCE(p.`LotName`, '')) <> ''
              AND UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotName`, '')), ' ', ''), '　', '')) =
                  UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotName`, '')), ' ', ''), '　', ''))
            )
          )
          AND (
            TRIM(COALESCE(r.`LotNo`, '')) = ''
            OR TRIM(COALESCE(p.`LotNo`, '')) = ''
            OR UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotNo`, '')), ' ', ''), '　', '')) =
               UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotNo`, '')), ' ', ''), '　', ''))
          )
          AND (
            TRIM(COALESCE(r.`LotName`, '')) = ''
            OR TRIM(COALESCE(p.`LotName`, '')) = ''
            OR UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotName`, '')), ' ', ''), '　', '')) =
               UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotName`, '')), ' ', ''), '　', ''))
          )
        GROUP BY q.`Id`
        HAVING COUNT(DISTINCT p.`Id`) = 1
    ) resolved_inner
) resolved
  ON resolved.`Id` = q.`Id`;
""");
    }

    if (staleConflictNowMissingIssues > 0)
    {
        affected += await ExecuteSqlAsync(tenantDb, $"""
UPDATE `bidops_review_quality_issue` q
JOIN (
    SELECT `Id`
    FROM (
        SELECT q.`Id`
        FROM `bidops_review_quality_issue` q
        JOIN `bidops_outcome_supplier_record` r
          ON r.`Id` = q.`OutcomeSupplierRecordId`
         AND r.`TenantId` = q.`TenantId`
        JOIN `bidops_review_task` t
          ON t.`Id` = q.`ReviewTaskId`
         AND t.`TenantId` = q.`TenantId`
        JOIN `bidops_notice_staging` n
          ON n.`Id` = t.`BizId`
         AND n.`TenantId` = q.`TenantId`
        WHERE q.`TenantId` = {tenantId}
          AND q.`IsResolved` = 0
          AND q.`IssueType` = 'LifecycleMatchConflict'
          AND q.`OutcomeSupplierRecordId` IS NOT NULL
          AND t.`BizType` = 'NoticeStaging'
          AND TRIM(COALESCE(r.`PackageNo`, '')) <> ''
          AND (
            TRIM(COALESCE(r.`LotNo`, '')) <> ''
            OR TRIM(COALESCE(r.`LotName`, '')) <> ''
          )
          AND NOT EXISTS (
            SELECT 1
            FROM `bidops_package_staging` p
            WHERE p.`NoticeStagingId` = n.`Id`
              AND p.`TenantId` = q.`TenantId`
              AND TRIM(COALESCE(p.`PackageNo`, '')) <> ''
              AND UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`PackageNo`, '')), ' ', ''), '　', '')) =
                  UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`PackageNo`, '')), ' ', ''), '　', ''))
              AND (
                (
                  TRIM(COALESCE(r.`LotNo`, '')) <> ''
                  AND TRIM(COALESCE(p.`LotNo`, '')) <> ''
                  AND UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotNo`, '')), ' ', ''), '　', '')) =
                      UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotNo`, '')), ' ', ''), '　', ''))
                )
                OR (
                  TRIM(COALESCE(r.`LotName`, '')) <> ''
                  AND TRIM(COALESCE(p.`LotName`, '')) <> ''
                  AND UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotName`, '')), ' ', ''), '　', '')) =
                      UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotName`, '')), ' ', ''), '　', ''))
                )
              )
              AND (
                TRIM(COALESCE(r.`LotNo`, '')) = ''
                OR TRIM(COALESCE(p.`LotNo`, '')) = ''
                OR UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotNo`, '')), ' ', ''), '　', '')) =
                   UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotNo`, '')), ' ', ''), '　', ''))
              )
              AND (
                TRIM(COALESCE(r.`LotName`, '')) = ''
                OR TRIM(COALESCE(p.`LotName`, '')) = ''
                OR UPPER(REPLACE(REPLACE(TRIM(COALESCE(p.`LotName`, '')), ' ', ''), '　', '')) =
                   UPPER(REPLACE(REPLACE(TRIM(COALESCE(r.`LotName`, '')), ' ', ''), '　', ''))
              )
          )
    ) stale_inner
) stale
  ON stale.`Id` = q.`Id`
SET
  q.`IssueType` = 'LifecycleMatchMissing',
  q.`Severity` = 1,
  q.`Message` = '中标/候选结果暂未匹配到采购公告包件，闭环分析可能缺少服务内容和资质要求。',
  q.`UpdatedAt` = NOW(6);
""");
    }

    if (resolvedMissingLotIssues > 0)
    {
        affected += await ExecuteSqlAsync(tenantDb, $"""
DELETE q
FROM `bidops_review_quality_issue` q
JOIN `bidops_outcome_supplier_record` r
  ON r.`Id` = q.`OutcomeSupplierRecordId`
 AND r.`TenantId` = q.`TenantId`
WHERE q.`TenantId` = {tenantId}
  AND q.`IsResolved` = 0
  AND q.`IssueType` = 'MissingLotOrPackage'
  AND q.`OutcomeSupplierRecordId` IS NOT NULL
  AND (
    TRIM(COALESCE(r.`LotNo`, '')) <> ''
    OR TRIM(COALESCE(r.`PackageNo`, '')) <> ''
  );
""");
    }

    if (reviewTaskSummaries > 0)
    {
        affected += await ExecuteSqlAsync(tenantDb, $"""
UPDATE `bidops_review_task` t
LEFT JOIN (
    SELECT
      `ReviewTaskId`,
      COUNT(1) AS `IssueCount`,
      SUM(CASE WHEN `Severity` = 2 THEN 1 ELSE 0 END) AS `HighCount`,
      SUM(CASE
        WHEN `Severity` = 2 THEN 30
        WHEN `Severity` = 1 THEN 15
        WHEN `Severity` = 0 THEN 5
        ELSE 0
      END) AS `Penalty`,
      SUM(CASE
        WHEN `IssueType` IN ('MissingLotOrPackage', 'AmbiguousAmountUnit', 'RateOrDiscountInAmountColumn', 'OriginalEvidenceMissing', 'DuplicatePackageIdentity')
        THEN 1 ELSE 0
      END) AS `ReparseCount`
    FROM `bidops_review_quality_issue`
    WHERE `TenantId` = {tenantId}
      AND `IsResolved` = 0
    GROUP BY `ReviewTaskId`
) q
  ON q.`ReviewTaskId` = t.`Id`
SET
  t.`QualityIssueCount` = COALESCE(q.`IssueCount`, 0),
  t.`HighRiskIssueCount` = COALESCE(q.`HighCount`, 0),
  t.`QualityScore` = GREATEST(0, 100 - COALESCE(q.`Penalty`, 0)),
  t.`RiskLevel` = CASE
    WHEN GREATEST(0, 100 - COALESCE(q.`Penalty`, 0)) < 60 OR COALESCE(q.`HighCount`, 0) > 0 THEN 2
    WHEN GREATEST(0, 100 - COALESCE(q.`Penalty`, 0)) < 85 OR COALESCE(q.`IssueCount`, 0) > 0 THEN 1
    ELSE 0
  END,
  t.`ReviewRecommendation` = CASE
    WHEN COALESCE(q.`IssueCount`, 0) = 0 THEN 0
    WHEN (
      GREATEST(0, 100 - COALESCE(q.`Penalty`, 0)) < 60
      OR COALESCE(q.`HighCount`, 0) > 0
    ) AND COALESCE(q.`ReparseCount`, 0) > 0 THEN 2
    ELSE 1
  END,
  t.`UpdatedAt` = NOW(6)
WHERE t.`TenantId` = {tenantId};
""");
    }

    return affected;
}

static async Task RepairBidOpsDataQualityAsync(string tenantConnection, string[] args)
{
    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    var affected = 0;
    var tenantIdText = GetOption(args, "--tenant-id");
    var tenantId = long.TryParse(tenantIdText, out var parsedTenantId) && parsedTenantId > 0
        ? parsedTenantId
        : BidOpsTenantId;
    var confirm = HasFlag(args, "--confirm");
    var dryRun = !confirm || HasFlag(args, "--dry-run");

    Console.WriteLine($"BidOps data quality repair for tenant {tenantId}.");
    Console.WriteLine(dryRun
        ? "Mode: dry-run. No rows will be changed. Add --confirm without --dry-run to execute."
        : "Mode: confirmed repair.");

    affected += await RepairBidOpsOutcomeLotIdentityQualityAsync(tenantDb, tenantId, dryRun);
    if (dryRun)
    {
        Console.WriteLine("BidOps data quality repair dry-run completed.");
        return;
    }

    await EnsureBidOpsRawNoticeBusinessIdentityAsync(tenantDb);

    affected += await ExecuteIfTableExistsAsync(tenantDb, "bidops_package_staging", $"""
UPDATE `bidops_package_staging`
SET
  `PackageNo` = CASE
    WHEN UPPER(TRIM(`PackageNo`)) = 'UNSPECIFIED'
      OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`PackageNo`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
    THEN '' ELSE `PackageNo` END,
  `LotNo` = CASE
    WHEN UPPER(TRIM(`LotNo`)) = 'UNSPECIFIED'
      OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`LotNo`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
    THEN '' ELSE `LotNo` END
WHERE `TenantId` = {tenantId}
  AND (
    UPPER(TRIM(`PackageNo`)) = 'UNSPECIFIED'
    OR UPPER(TRIM(`LotNo`)) = 'UNSPECIFIED'
    OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`PackageNo`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
    OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`LotNo`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
  );
""");

    affected += await ExecuteIfTableExistsAsync(tenantDb, "bidops_tender_package", $"""
UPDATE `bidops_tender_package`
SET
  `PackageNo` = CASE
    WHEN UPPER(TRIM(`PackageNo`)) = 'UNSPECIFIED'
      OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`PackageNo`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
    THEN '' ELSE `PackageNo` END,
  `LotNo` = CASE
    WHEN UPPER(TRIM(`LotNo`)) = 'UNSPECIFIED'
      OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`LotNo`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
    THEN '' ELSE `LotNo` END
WHERE `TenantId` = {tenantId}
  AND (
    UPPER(TRIM(`PackageNo`)) = 'UNSPECIFIED'
    OR UPPER(TRIM(`LotNo`)) = 'UNSPECIFIED'
    OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`PackageNo`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
    OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`LotNo`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
  );
""");

    affected += await ExecuteIfTableExistsAsync(tenantDb, "bidops_package_staging", $"""
UPDATE `bidops_package_staging`
SET
  `PackageNo` = CASE WHEN `PackageNo` REGEXP '^[?？�][?？�][0-9._/-]*$' THEN '' ELSE `PackageNo` END,
  `LotNo` = CASE WHEN `LotNo` REGEXP '^[?？�][?？�][0-9._/-]*$' THEN '' ELSE `LotNo` END
WHERE `TenantId` = {tenantId}
  AND (`PackageNo` REGEXP '^[?？�][?？�][0-9._/-]*$' OR `LotNo` REGEXP '^[?？�][?？�][0-9._/-]*$');
""");

    affected += await ExecuteIfTableExistsAsync(tenantDb, "bidops_tender_package", $"""
UPDATE `bidops_tender_package`
SET
  `PackageNo` = CASE WHEN `PackageNo` REGEXP '^[?？�][?？�][0-9._/-]*$' THEN '' ELSE `PackageNo` END,
  `LotNo` = CASE WHEN `LotNo` REGEXP '^[?？�][?？�][0-9._/-]*$' THEN '' ELSE `LotNo` END
WHERE `TenantId` = {tenantId}
  AND (`PackageNo` REGEXP '^[?？�][?？�][0-9._/-]*$' OR `LotNo` REGEXP '^[?？�][?？�][0-9._/-]*$');
""");

    affected += await ExecuteIfTableExistsAsync(tenantDb, "bidops_supplier", $"""
UPDATE `bidops_supplier`
SET
  `Name` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Name`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN CONCAT('待补录厂家-', RIGHT(CAST(`Id` AS CHAR), 6)) ELSE `Name` END,
  `UnifiedSocialCreditCode` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`UnifiedSocialCreditCode`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `UnifiedSocialCreditCode` END,
  `Region` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Region`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `Region` END,
  `Address` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Address`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `Address` END,
  `ContactName` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`ContactName`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `ContactName` END,
  `ContactPhone` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`ContactPhone`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `ContactPhone` END,
  `ContactEmail` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`ContactEmail`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `ContactEmail` END,
  `Remark` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Remark`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `Remark` END
WHERE `TenantId` = {tenantId}
  AND (
    TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Name`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
    OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`UnifiedSocialCreditCode`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
    OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Region`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
    OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Address`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
    OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`ContactName`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
    OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`ContactPhone`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
    OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`ContactEmail`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
    OR TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Remark`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = ''
  );
""");

    affected += await ExecuteIfTableExistsAsync(tenantDb, "bidops_supplier", $"""
UPDATE `bidops_supplier`
SET `Name` = CONCAT('待补录厂家-', RIGHT(CAST(`Id` AS CHAR), 6))
WHERE `TenantId` = {tenantId}
  AND `Name` REGEXP '^[?？�][?？�][0-9._/-]*$';
""");

    affected += await ExecuteIfTableExistsAsync(tenantDb, "bidops_supplier_contact", $"""
UPDATE `bidops_supplier_contact`
SET
  `Name` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Name`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '待补录联系人' ELSE `Name` END,
  `Role` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Role`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `Role` END,
  `Phone` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Phone`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `Phone` END,
  `Email` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Email`, '?', ''), '？', ''), '-', ''), '�', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `Email` END,
  `Remark` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Remark`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `Remark` END
WHERE `TenantId` = {tenantId};
""");

    affected += await ExecuteIfTableExistsAsync(tenantDb, "bidops_supplier_capability", $"""
UPDATE `bidops_supplier_capability`
SET
  `Category` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Category`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN 'Other' ELSE `Category` END,
  `ProductLine` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`ProductLine`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `ProductLine` END,
  `CapabilityTags` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`CapabilityTags`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `CapabilityTags` END,
  `RegionScope` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`RegionScope`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `RegionScope` END,
  `QualificationLevel` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`QualificationLevel`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `QualificationLevel` END,
  `Remark` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Remark`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `Remark` END
WHERE `TenantId` = {tenantId};
""");

    affected += await ExecuteIfTableExistsAsync(tenantDb, "bidops_supplier_evidence_document", $"""
UPDATE `bidops_supplier_evidence_document`
SET
  `DocumentName` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`DocumentName`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '待补录材料' ELSE `DocumentName` END,
  `DocumentType` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`DocumentType`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN 'Other' ELSE `DocumentType` END,
  `EvidenceNo` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`EvidenceNo`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `EvidenceNo` END,
  `IssuedBy` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`IssuedBy`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `IssuedBy` END,
  `FileName` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`FileName`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `FileName` END,
  `FileUrl` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`FileUrl`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `FileUrl` END,
  `Remark` = CASE WHEN TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`Remark`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '' THEN '' ELSE `Remark` END
WHERE `TenantId` = {tenantId};
""");

    affected += await ExecuteIfTableExistsAsync(tenantDb, "bidops_supplier_match_result", $"""
UPDATE `bidops_supplier_match_result`
SET `SupplierNameSnapshot` = '待补录厂家'
WHERE `TenantId` = {tenantId}
  AND TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`SupplierNameSnapshot`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '';
""");

    affected += await ExecuteIfTableExistsAsync(tenantDb, "bidops_supplier_match_result", $"""
UPDATE `bidops_supplier_match_result`
SET `SupplierNameSnapshot` = '待补录厂家'
WHERE `TenantId` = {tenantId}
  AND `SupplierNameSnapshot` REGEXP '^[?？�][?？�][0-9._/-]*$';
""");

    affected += await ExecuteIfTableExistsAsync(tenantDb, "bidops_pursuit", $"""
UPDATE `bidops_pursuit`
SET `SupplierNameSnapshot` = ''
WHERE `TenantId` = {tenantId}
  AND TRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`SupplierNameSnapshot`, '?', ''), '？', ''), '�', ''), '-', ''), '_', ''), '.', ''), '/', '')) = '';
""");

    affected += await ExecuteIfTableExistsAsync(tenantDb, "bidops_pursuit", $"""
UPDATE `bidops_pursuit`
SET `SupplierNameSnapshot` = ''
WHERE `TenantId` = {tenantId}
  AND `SupplierNameSnapshot` REGEXP '^[?？�][?？�][0-9._/-]*$';
""");

    Console.WriteLine($"BidOps data quality repair completed. affectedRows={affected}");
}

static async Task ApproveBidOpsPendingAsync(string tenantConnection)
{
    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    var idGenerator = new SnowflakeIdGenerator(workerId: 2, datacenterId: 1);
    await using var transaction = await tenantDb.Database.BeginTransactionAsync();

    var task = await tenantDb.Set<ReviewTask>()
        .Where(x => x.TenantId == BidOpsTenantId && x.Status == Atlas.Modules.BidOps.Entities.ReviewTaskStatus.Pending)
        .OrderBy(x => x.CreatedAt)
        .FirstOrDefaultAsync();
    if (task == null)
    {
        Console.WriteLine("No pending BidOps review task found.");
        return;
    }

    var staging = await tenantDb.Set<NoticeStaging>()
        .FirstAsync(x => x.TenantId == BidOpsTenantId && x.Id == task.BizId);
    var raw = await tenantDb.Set<RawNotice>()
        .FirstAsync(x => x.TenantId == BidOpsTenantId && x.Id == staging.RawNoticeId);

    var notice = await tenantDb.Set<Notice>()
        .FirstOrDefaultAsync(x => x.TenantId == BidOpsTenantId && x.RawNoticeId == raw.Id);
    var now = DateTime.UtcNow;
    if (notice == null)
    {
        notice = new Notice
        {
            Id = idGenerator.NextId(),
            TenantId = BidOpsTenantId,
            CreatedAt = now,
            RawNoticeId = raw.Id,
            NoticeStagingId = staging.Id,
            Title = raw.Title,
            NoticeType = staging.NoticeType,
            ProjectName = staging.ProjectName,
            ProjectCode = staging.ProjectCode,
            BuyerName = staging.BuyerName,
            AgencyName = staging.AgencyName,
            Region = staging.Region,
            BudgetAmount = staging.BudgetAmount,
            PublishTime = staging.PublishTime,
            SignupDeadline = staging.SignupDeadline,
            BidDeadline = staging.BidDeadline,
            OpenBidTime = staging.OpenBidTime,
            Status = "Active"
        };
        tenantDb.Set<Notice>().Add(notice);

        var stagingPackages = await tenantDb.Set<PackageStaging>()
            .Where(x => x.TenantId == BidOpsTenantId && x.NoticeStagingId == staging.Id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
        foreach (var stagingPackage in stagingPackages)
        {
            var package = new TenderPackage
            {
                Id = idGenerator.NextId(),
                TenantId = BidOpsTenantId,
                CreatedAt = now,
                NoticeId = notice.Id,
                PackageStagingId = stagingPackage.Id,
                LotNo = stagingPackage.LotNo,
                LotName = stagingPackage.LotName,
                PackageNo = stagingPackage.PackageNo,
                PackageName = stagingPackage.PackageName,
                Category = stagingPackage.Category,
                Quantity = stagingPackage.Quantity,
                Unit = stagingPackage.Unit,
                BudgetAmount = stagingPackage.BudgetAmount,
                MaxPrice = stagingPackage.MaxPrice,
                DeliveryPlace = stagingPackage.DeliveryPlace,
                DeliveryPeriod = stagingPackage.DeliveryPeriod,
                Status = "New"
            };
            tenantDb.Set<TenderPackage>().Add(package);
            stagingPackage.ReviewStatus = Atlas.Modules.BidOps.Entities.ReviewStatus.Approved;

            var stagingRequirements = await tenantDb.Set<RequirementStaging>()
                .Where(x => x.TenantId == BidOpsTenantId && x.PackageStagingId == stagingPackage.Id)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
            foreach (var stagingRequirement in stagingRequirements)
            {
                tenantDb.Set<RequirementItem>().Add(new RequirementItem
                {
                    Id = idGenerator.NextId(),
                    TenantId = BidOpsTenantId,
                    CreatedAt = now,
                    PackageId = package.Id,
                    RequirementStagingId = stagingRequirement.Id,
                    RequirementType = stagingRequirement.RequirementType,
                    OriginalText = stagingRequirement.OriginalText,
                    SourceFileId = stagingRequirement.SourceFileId,
                    SourcePage = stagingRequirement.SourcePage,
                    IsMandatory = stagingRequirement.IsMandatory,
                    IsRejectRisk = stagingRequirement.IsRejectRisk,
                    RequiredEvidenceType = stagingRequirement.RequiredEvidenceType,
                    RiskLevel = stagingRequirement.RiskLevel,
                    AiExplanation = stagingRequirement.AiExplanation
                });
                stagingRequirement.ReviewStatus = Atlas.Modules.BidOps.Entities.ReviewStatus.Approved;
            }
        }
    }

    task.Status = Atlas.Modules.BidOps.Entities.ReviewTaskStatus.Approved;
    task.Decision = "Approved";
    task.Remark = "Approved by Atlas.LocalSetup approve-bidops-pending.";
    task.ReviewerId = BidOpsAdminUserId;
    task.ReviewedAt = now;
    staging.ReviewStatus = Atlas.Modules.BidOps.Entities.ReviewStatus.Approved;
    staging.ReviewerId = BidOpsAdminUserId;
    staging.ReviewedAt = now;
    raw.Status = Atlas.Modules.BidOps.Entities.RawNoticeStatus.Approved;
    raw.LastError = string.Empty;

    await tenantDb.SaveChangesAsync();
    await transaction.CommitAsync();

    var packageCount = await tenantDb.Set<TenderPackage>()
        .CountAsync(x => x.TenantId == BidOpsTenantId && x.NoticeId == notice.Id);
    var requirementCount = await tenantDb.Set<RequirementItem>()
        .Join(
            tenantDb.Set<TenderPackage>().Where(x => x.TenantId == BidOpsTenantId && x.NoticeId == notice.Id),
            requirement => requirement.PackageId,
            package => package.Id,
            (requirement, _) => requirement)
        .CountAsync();

    Console.WriteLine($"Approved review task {task.Id}.");
    Console.WriteLine($"Formal notice: {notice.Id}");
    Console.WriteLine($"Formal packages: {packageCount}");
    Console.WriteLine($"Formal requirements: {requirementCount}");
}

static async Task CancelBidOpsCrawlJobsAsync(string globalConnection)
{
    await using var globalDb = CreateGlobalDbContext(globalConnection);
    var now = DateTime.UtcNow;
    var jobs = await globalDb.BackgroundJobs
        .Where(x => x.TenantId == BidOpsTenantId &&
                    x.JobType == BidOpsBackgroundJobTypes.StateGridEcpCrawl &&
                    x.CompletedAtUtc == null &&
                    (x.Status == BackgroundJobStatus.Pending ||
                     x.Status == BackgroundJobStatus.Running ||
                     x.Status == BackgroundJobStatus.Failed))
        .ToListAsync();

    foreach (var job in jobs)
    {
        job.Status = BackgroundJobStatus.Canceled;
        job.CompletedAtUtc = now;
        job.LockedAtUtc = null;
        job.LockedBy = null;
        job.NextAttemptAtUtc = null;
        job.Result = "Canceled by Atlas.LocalSetup cancel-bidops-crawl-jobs.";
        job.UpdatedAt = now;
    }

    await globalDb.SaveChangesAsync();
    Console.WriteLine($"Canceled BidOps State Grid crawl jobs: {jobs.Count}");
}

static async Task ResetDemoAsync(string globalConnection, string tenantConnection)
{
    Console.WriteLine("Resetting local Atlas demo databases...");

    await using var globalDb = CreateGlobalDbContext(globalConnection);
    await globalDb.Database.EnsureDeletedAsync();
    await globalDb.Database.EnsureCreatedAsync();
    SeedGlobal(globalDb, tenantConnection);
    await globalDb.SaveChangesAsync();

    await using var tenantDb = CreateTenantDbContext(tenantConnection);
    await tenantDb.Database.EnsureDeletedAsync();
    await tenantDb.Database.EnsureCreatedAsync();
    SeedTenant(tenantDb);
    await tenantDb.SaveChangesAsync();

    PrintDemoAccounts();
}

static AtlasGlobalDbContext CreateGlobalDbContext(string connectionString)
{
    var options = new DbContextOptionsBuilder<AtlasGlobalDbContext>()
        .UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(5, 7, 32)),
            mysql => mysql.MigrationsAssembly("Atlas.Data.Global.Migrations"))
        .Options;

    return new LocalSetupGlobalDbContext(options, SystemIdentity.Migration);
}

static AtlasTenantDbContext CreateTenantDbContext(string connectionString)
{
    var options = new DbContextOptionsBuilder<AtlasTenantDbContext>()
        .UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(5, 7, 32)),
            mysql => mysql.MigrationsAssembly("Atlas.Data.Tenant.Migrations"))
        .Options;

    return new LocalSetupTenantDbContext(options);
}

static void SeedGlobal(AtlasGlobalDbContext db, string tenantConnection)
{
    var now = DateTime.UtcNow;

    UpsertRange(db, new DatabaseMasterServer
    {
        Id = 210001,
        Code = "local-master",
        NickName = "Local MySQL",
        CreatedAt = now
    });

    UpsertRange(db, new DatabaseServerConfig
    {
        Id = 210011,
        ServerCode = "local-master",
        NetworkEnvCode = NetworkEnvCodes.Default,
        DbType = "MySQL",
        ConnString = tenantConnection,
        CreatedAt = now
    });

    UpsertRange(db, new DatabaseInstance
    {
        Id = DatabaseInstanceId,
        Name = "Local Atlas Tenant DB",
        DbType = "MySQL",
        MasterServerCode = "local-master",
        DbName = "atlas",
        Version = "8.0",
        Region = "local",
        ConnectionString = tenantConnection,
        CreatedAt = now
    });

    UpsertRange(db, new Tenant
    {
        Id = TenantId,
        Name = "Atlas 本地演示租户",
        BrandName = "Atlas Demo",
        Address = "Local",
        PhoneNumber = "021-10000000",
        ContactName = "Demo Admin",
        ContactPhoneNumber = "13800000000",
        ContactEmail = "demo@atlas.local",
        Domain = "demo",
        TenantType = TenantType.Enterprise,
        Province = "上海",
        City = "上海",
        Category = Tenant.TenantCategory.Trial,
        Status = TenantStatus.Active,
        BusinessType = BusinessType.Franchise,
        DatabaseInstanceId = DatabaseInstanceId,
        OfficeCount = 4,
        CreatedAt = now
    });

    UpsertRange(db,
        CreateTenantPackageEntitlement(CorePackageEntitlementId, "atlas.core", now),
        CreateTenantPackageEntitlement(StandardPackageEntitlementId, "atlas.standard", now));
}

static void SeedTenant(AtlasTenantDbContext db)
{
    var now = DateTime.UtcNow;
    var passwordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);

    UpsertRange(db,
        CreateStore(HqStoreId, "HQ", "总部", StoreType.Headquarters, null, now),
        CreateStore(DirectAStoreId, "D-A", "直营一店", StoreType.DirectOperated, HqStoreId, now),
        CreateStore(DirectBStoreId, "D-B", "直营二店", StoreType.DirectOperated, HqStoreId, now),
        CreateStore(FranchiseStoreId, "F-A", "加盟一店", StoreType.Franchised, null, now));

    UpsertRange(db,
        CreateUser(HqUserId, "hq_admin", "总部管理员", HqStoreId, UserType.TenantAdmin, passwordHash, now),
        CreateUser(DirectUserId, "direct_a_mgr", "直营一店店长", DirectAStoreId, UserType.StoreManager, passwordHash, now),
        CreateUser(FranchiseUserId, "franchise_mgr", "加盟店店长", FranchiseStoreId, UserType.StoreManager, passwordHash, now));

    UpsertRange(db,
        CreateUserStore(130001, HqUserId, HqStoreId, true, now),
        CreateUserStore(130002, HqUserId, DirectAStoreId, false, now),
        CreateUserStore(130003, HqUserId, DirectBStoreId, false, now),
        CreateUserStore(130004, HqUserId, FranchiseStoreId, false, now),
        CreateUserStore(130011, DirectUserId, DirectAStoreId, true, now),
        CreateUserStore(130101, FranchiseUserId, FranchiseStoreId, true, now));

    UpsertRange(db,
        CreatePermission(
            ProductReadPermissionId,
            "product.read",
            "Read products",
            "product.catalog",
            "Product",
            "product",
            "read",
            now),
        CreatePermission(
            InventoryReadPermissionId,
            "inventory.read",
            "Read inventory",
            "inventory.stock",
            "Inventory",
            "inventory",
            "read",
            now));

    UpsertRange(db,
        CreateRole(
            DirectReaderRoleId,
            "demo-direct-reader",
            "Demo direct store reader",
            DirectAStoreId,
            "Can read shared products and current-store inventory for direct store demos.",
            now),
        CreateRole(
            FranchiseReaderRoleId,
            "demo-franchise-reader",
            "Demo franchise reader",
            FranchiseStoreId,
            "Can read franchise products and current-store inventory for franchise demos.",
            now));

    UpsertRange(db,
        CreateRolePermission(
            DirectProductRolePermissionId,
            DirectReaderRoleId,
            ProductReadPermissionId,
            AtlasDataScopeType.SharedStores,
            now),
        CreateRolePermission(
            DirectInventoryRolePermissionId,
            DirectReaderRoleId,
            InventoryReadPermissionId,
            AtlasDataScopeType.CurrentStore,
            now),
        CreateRolePermission(
            FranchiseProductRolePermissionId,
            FranchiseReaderRoleId,
            ProductReadPermissionId,
            AtlasDataScopeType.SharedStores,
            now),
        CreateRolePermission(
            FranchiseInventoryRolePermissionId,
            FranchiseReaderRoleId,
            InventoryReadPermissionId,
            AtlasDataScopeType.CurrentStore,
            now));

    UpsertRange(db,
        CreateUserRole(DirectReaderUserRoleId, DirectUserId, DirectReaderRoleId, DirectAStoreId, now),
        CreateUserRole(FranchiseReaderUserRoleId, FranchiseUserId, FranchiseReaderRoleId, FranchiseStoreId, now));

    UpsertRange(db,
        CreateProduct(140001, HqStoreId, "总部标准套餐", 199m, "总部维护的标准共享商品，直营范围可见。", null, false, now),
        CreateProduct(140011, DirectAStoreId, "直营一店限定套餐", 129m, "直营一店自定义商品，总部和直营兄弟店可见。", HqStoreId, true, now),
        CreateProduct(140012, DirectBStoreId, "直营二店限定套餐", 139m, "直营二店自定义商品，总部和直营兄弟店可见。", HqStoreId, true, now),
        CreateProduct(140101, FranchiseStoreId, "加盟一店自有套餐", 159m, "加盟店自有商品，只在加盟店范围内可见。", null, true, now));

    UpsertRange(db,
        CreateInventory(150001, HqStoreId, 140001, 100, now),
        CreateInventory(150011, DirectAStoreId, 140011, 20, now),
        CreateInventory(150012, DirectBStoreId, 140012, 30, now),
        CreateInventory(150101, FranchiseStoreId, 140101, 40, now));
}

static void SeedBidOpsGlobal(AtlasGlobalDbContext db, string tenantConnection)
{
    var now = DateTime.UtcNow;

    UpsertRange(db, new DatabaseMasterServer
    {
        Id = BidOpsDatabaseMasterServerId,
        Code = "bidops-local-master",
        NickName = "BidOps Local MySQL",
        CreatedAt = now
    });

    UpsertRange(db, new DatabaseServerConfig
    {
        Id = BidOpsDatabaseServerConfigId,
        ServerCode = "bidops-local-master",
        NetworkEnvCode = NetworkEnvCodes.Default,
        DbType = "MySQL",
        ConnString = tenantConnection,
        CreatedAt = now
    });

    UpsertRange(db, new DatabaseInstance
    {
        Id = BidOpsDatabaseInstanceId,
        Name = "BidOps Local Tenant DB",
        DbType = "MySQL",
        MasterServerCode = "bidops-local-master",
        DbName = ExtractDatabaseName(tenantConnection) ?? "atlas_bidops_runtime",
        Version = "8.0",
        Region = "local",
        ConnectionString = tenantConnection,
        CreatedAt = now
    });

    UpsertRange(db, new Tenant
    {
        Id = BidOpsTenantId,
        Name = "BidOps",
        BrandName = "BidOps",
        Address = "Local",
        PhoneNumber = "010-00000000",
        ContactName = "BidOps Admin",
        ContactPhoneNumber = "13800000000",
        ContactEmail = "admin@bidops.local",
        Domain = "bidops",
        TenantType = TenantType.Enterprise,
        Province = "北京",
        City = "北京",
        Category = "TenderOps",
        Status = TenantStatus.Active,
        BusinessType = BusinessType.Chain,
        DatabaseInstanceId = BidOpsDatabaseInstanceId,
        OfficeCount = 1,
        CreatedAt = now
    });

    UpsertRange(db,
        CreateBidOpsEntitlement(BidOpsCorePackageEntitlementId, "atlas.core", now),
        CreateBidOpsEntitlement(BidOpsStandardPackageEntitlementId, "atlas.standard", now));
}

static List<BackgroundJob> EnqueueBidOpsStateGridScans(AtlasGlobalDbContext db)
{
    var now = DateTime.UtcNow;
    var stamp = now.ToString("yyyyMMddHHmmss");
    var baseId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
    var jobs = new List<BackgroundJob>();
    var channels = new[]
    {
        (Offset: 1L, ChannelId: BidOpsStateGridTenderChannelId, Name: "BidOps State Grid tender announcements scan"),
        (Offset: 2L, ChannelId: BidOpsStateGridProcurementChannelId, Name: "BidOps State Grid procurement announcements scan"),
        (Offset: 3L, ChannelId: BidOpsStateGridCandidateChannelId, Name: "BidOps State Grid candidate announcements scan"),
        (Offset: 4L, ChannelId: BidOpsStateGridAwardChannelId, Name: "BidOps State Grid award announcements scan")
    };

    foreach (var channel in channels)
    {
        var deduplicationKey = $"bidops:state-grid-ecp-seed:{BidOpsTenantId}:{channel.ChannelId}:{stamp}";
        var existing = db.BackgroundJobs
            .AsNoTracking()
            .FirstOrDefault(x => x.TenantId == BidOpsTenantId && x.DeduplicationKey == deduplicationKey);
        if (existing != null)
        {
            jobs.Add(existing);
            continue;
        }

        var payload = new StateGridEcpCrawlJobPayload(
            BidOpsTenantId,
            BidOpsHqStoreId,
            BidOpsAdminUserId,
            "bidops_admin",
            channel.ChannelId);

        var job = new BackgroundJob
        {
            Id = baseId + channel.Offset,
            TenantId = BidOpsTenantId,
            StoreId = BidOpsHqStoreId,
            JobType = BidOpsBackgroundJobTypes.StateGridEcpCrawl,
            Queue = BidOpsBackgroundJobQueues.BidOps,
            JobName = channel.Name,
            DeduplicationKey = deduplicationKey,
            Payload = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Status = BackgroundJobStatus.Pending,
            Priority = 0,
            AvailableAtUtc = now,
            MaxAttempts = 3,
            CreatedAt = now
        };
        db.BackgroundJobs.Add(job);
        jobs.Add(job);
    }

    return jobs;
}

static void SeedBidOpsTenant(AtlasTenantDbContext db)
{
    var now = DateTime.UtcNow;
    var passwordHash = BCrypt.Net.BCrypt.HashPassword(BidOpsLocalPassword);

    UpsertRange(db, new Store
    {
        Id = BidOpsHqStoreId,
        TenantId = BidOpsTenantId,
        Code = "BIDOPS-HQ",
        Name = "BidOps Headquarters",
        Type = StoreType.Headquarters,
        IsActive = true,
        Address = "Local",
        ContactPhone = "010-00000000",
        ContactPerson = "BidOps Admin",
        Province = "北京",
        City = "北京",
        District = "海淀区",
        Status = StoreStatus.Active,
        Version = 1,
        CreatedAt = now
    });

    UpsertRange(db, new User
    {
        Id = BidOpsAdminUserId,
        TenantId = BidOpsTenantId,
        UserName = "bidops_admin",
        PasswordHash = passwordHash,
        RealName = "BidOps Admin",
        NickName = "BidOps Admin",
        Phone = "13800000000",
        Email = "admin@bidops.local",
        Gender = Gender.Unknown,
        TokenVersion = 1,
        Type = UserType.TenantAdmin,
        Status = UserStatus.Active,
        IsActivated = true,
        DefaultStoreId = BidOpsHqStoreId,
        EmployeeNo = "BIDOPS-ADMIN",
        Position = "BidOps Operator",
        MustChangePassword = false,
        Version = 1,
        CreatedAt = now
    });

    UpsertRange(db, new UserStore
    {
        Id = BidOpsAdminUserStoreId,
        TenantId = BidOpsTenantId,
        UserId = BidOpsAdminUserId,
        StoreId = BidOpsHqStoreId,
        IsPrimary = true,
        Permission = "Admin",
        EffectiveFrom = now.AddDays(-1),
        CreatedAt = now
    });

    UpsertRange(db,
        CreateBidOpsPermission(BidOpsDashboardReadPermissionId, BidOpsPermissionCodes.DashboardRead, "Read BidOps dashboard", BidOpsCapabilities.Dashboard, BidOpsDataResources.Dashboard, "read", AtlasPermissionRiskLevel.Low, now),
        CreateBidOpsPermission(BidOpsCrawlReadPermissionId, BidOpsPermissionCodes.CrawlRead, "Read BidOps crawl data", BidOpsCapabilities.Crawl, BidOpsDataResources.RawNotice, "read", AtlasPermissionRiskLevel.Low, now),
        CreateBidOpsPermission(BidOpsCrawlManagePermissionId, BidOpsPermissionCodes.CrawlManage, "Manage BidOps crawl sources", BidOpsCapabilities.Crawl, BidOpsDataResources.CrawlSource, "manage", AtlasPermissionRiskLevel.Medium, now),
        CreateBidOpsPermission(BidOpsCrawlImportPermissionId, BidOpsPermissionCodes.CrawlImport, "Import public tender URL", BidOpsCapabilities.Crawl, BidOpsDataResources.RawNotice, "import", AtlasPermissionRiskLevel.Medium, now),
        CreateBidOpsPermission(BidOpsReviewReadPermissionId, BidOpsPermissionCodes.ReviewRead, "Read BidOps review tasks", BidOpsCapabilities.Review, BidOpsDataResources.ReviewTask, "read", AtlasPermissionRiskLevel.Low, now),
        CreateBidOpsPermission(BidOpsReviewApprovePermissionId, BidOpsPermissionCodes.ReviewApprove, "Approve BidOps staging data", BidOpsCapabilities.Review, BidOpsDataResources.ReviewTask, "approve", AtlasPermissionRiskLevel.High, now),
        CreateBidOpsPermission(BidOpsBusinessReadPermissionId, BidOpsPermissionCodes.BusinessRead, "Read BidOps formal tender data", BidOpsCapabilities.Business, BidOpsDataResources.Notice, "read", AtlasPermissionRiskLevel.Low, now),
        CreateBidOpsPermission(BidOpsOpportunityReadPermissionId, BidOpsPermissionCodes.OpportunityRead, "Read BidOps opportunities", BidOpsCapabilities.Opportunity, BidOpsDataResources.Opportunity, "read", AtlasPermissionRiskLevel.Low, now),
        CreateBidOpsPermission(BidOpsOpportunityManagePermissionId, BidOpsPermissionCodes.OpportunityManage, "Manage BidOps opportunities", BidOpsCapabilities.Opportunity, BidOpsDataResources.Opportunity, "manage", AtlasPermissionRiskLevel.Medium, now),
        CreateBidOpsPermission(BidOpsOpportunityWatchPermissionId, BidOpsPermissionCodes.OpportunityWatch, "Watch BidOps opportunities", BidOpsCapabilities.Opportunity, BidOpsDataResources.Opportunity, "watch", AtlasPermissionRiskLevel.Low, now),
        CreateBidOpsPermission(BidOpsOpportunityAssessPermissionId, BidOpsPermissionCodes.OpportunityAssess, "Assess BidOps opportunities", BidOpsCapabilities.Opportunity, BidOpsDataResources.Opportunity, "assess", AtlasPermissionRiskLevel.Medium, now),
        CreateBidOpsPermission(BidOpsSupplierReadPermissionId, BidOpsPermissionCodes.SupplierRead, "Read BidOps suppliers", BidOpsCapabilities.Supplier, BidOpsDataResources.Supplier, "read", AtlasPermissionRiskLevel.Low, now),
        CreateBidOpsPermission(BidOpsSupplierManagePermissionId, BidOpsPermissionCodes.SupplierManage, "Manage BidOps suppliers", BidOpsCapabilities.Supplier, BidOpsDataResources.Supplier, "manage", AtlasPermissionRiskLevel.Medium, now),
        CreateBidOpsPermission(BidOpsSupplierEvidenceReadPermissionId, BidOpsPermissionCodes.SupplierEvidenceRead, "Read BidOps supplier evidence", BidOpsCapabilities.Supplier, BidOpsDataResources.SupplierEvidence, "read", AtlasPermissionRiskLevel.Low, now),
        CreateBidOpsPermission(BidOpsSupplierEvidenceManagePermissionId, BidOpsPermissionCodes.SupplierEvidenceManage, "Manage BidOps supplier evidence", BidOpsCapabilities.Supplier, BidOpsDataResources.SupplierEvidence, "manage", AtlasPermissionRiskLevel.Medium, now),
        CreateBidOpsPermission(BidOpsMatchingReadPermissionId, BidOpsPermissionCodes.MatchingRead, "Read BidOps matching runs", BidOpsCapabilities.Matching, BidOpsDataResources.Matching, "read", AtlasPermissionRiskLevel.Low, now),
        CreateBidOpsPermission(BidOpsMatchingRunPermissionId, BidOpsPermissionCodes.MatchingRun, "Run BidOps supplier matching", BidOpsCapabilities.Matching, BidOpsDataResources.Matching, "run", AtlasPermissionRiskLevel.Medium, now),
        CreateBidOpsPermission(BidOpsMatchingDecidePermissionId, BidOpsPermissionCodes.MatchingDecide, "Record BidOps go/no-go decisions", BidOpsCapabilities.Matching, BidOpsDataResources.GoNoGoDecision, "decide", AtlasPermissionRiskLevel.High, now),
        CreateBidOpsPermission(BidOpsPursuitReadPermissionId, BidOpsPermissionCodes.PursuitRead, "Read BidOps pursuits", BidOpsCapabilities.Pursuit, BidOpsDataResources.Pursuit, "read", AtlasPermissionRiskLevel.Low, now),
        CreateBidOpsPermission(BidOpsPursuitManagePermissionId, BidOpsPermissionCodes.PursuitManage, "Manage BidOps pursuits", BidOpsCapabilities.Pursuit, BidOpsDataResources.Pursuit, "manage", AtlasPermissionRiskLevel.Medium, now),
        CreateBidOpsPermission(BidOpsPursuitTaskManagePermissionId, BidOpsPermissionCodes.PursuitTaskManage, "Manage BidOps pursuit tasks", BidOpsCapabilities.Pursuit, BidOpsDataResources.PursuitTask, "manage", AtlasPermissionRiskLevel.Medium, now),
        CreateBidOpsPermission(BidOpsPursuitFollowRecordManagePermissionId, BidOpsPermissionCodes.PursuitFollowRecordManage, "Manage BidOps pursuit follow records", BidOpsCapabilities.Pursuit, BidOpsDataResources.Pursuit, "follow", AtlasPermissionRiskLevel.Medium, now));

    UpsertRange(db, new Role
    {
        Id = BidOpsOperatorRoleId,
        TenantId = BidOpsTenantId,
        Code = "bidops-operator",
        Name = "BidOps Operator",
        Description = "Can manage BidOps public crawl, review staging data, and read formal notices.",
        Scope = PermissionScope.Tenant,
        StoreId = null,
        IsSystem = false,
        IsEnabled = true,
        CreatedAt = now
    });

    UpsertRange(db,
        CreateBidOpsRolePermission(320500, BidOpsDashboardReadPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320501, BidOpsCrawlReadPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320502, BidOpsCrawlManagePermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320503, BidOpsCrawlImportPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320504, BidOpsReviewReadPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320505, BidOpsReviewApprovePermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320506, BidOpsBusinessReadPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320507, BidOpsOpportunityReadPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320508, BidOpsOpportunityManagePermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320509, BidOpsOpportunityWatchPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320510, BidOpsOpportunityAssessPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320511, BidOpsSupplierReadPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320512, BidOpsSupplierManagePermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320513, BidOpsSupplierEvidenceReadPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320514, BidOpsSupplierEvidenceManagePermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320515, BidOpsMatchingReadPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320516, BidOpsMatchingRunPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320517, BidOpsMatchingDecidePermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320518, BidOpsPursuitReadPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320519, BidOpsPursuitManagePermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320520, BidOpsPursuitTaskManagePermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320521, BidOpsPursuitFollowRecordManagePermissionId, AtlasDataScopeType.AllTenant, now));

    UpsertRange(db, new UserRole
    {
        Id = 320601,
        TenantId = BidOpsTenantId,
        UserId = BidOpsAdminUserId,
        RoleId = BidOpsOperatorRoleId,
        StoreId = BidOpsHqStoreId,
        GrantedAt = now,
        GrantedBy = BidOpsAdminUserId,
        CreatedAt = now
    });
}

static void SeedBidOpsStateGridCrawler(AtlasTenantDbContext db)
{
    var now = DateTime.UtcNow;

    UpsertRange(db, new CrawlSource
    {
        Id = BidOpsStateGridSourceId,
        TenantId = BidOpsTenantId,
        Code = BidOpsSystemValues.StateGridEcpSourceCode,
        Name = "国家电网新一代电子商务平台",
        SourceType = BidOpsCrawlSourceTypes.StateGridEcp,
        BaseUrl = "https://ecp.sgcc.com.cn/ecp2.0/portal/",
        Enabled = true,
        Priority = 10,
        RateLimitPerMinute = 30,
        CrawlIntervalMinutes = 60,
        MaxRetryCount = 3,
        NeedJsRender = false,
        NeedLogin = false,
        RespectRobots = true,
        UserAgent = "AtlasBidOps/0.1 (+public procurement crawler; no-login)",
        RobotsPolicyNote = "Public State Grid ECP portal data only. No login, captcha, bypass, or private data access.",
        Remark = "Seeded by Atlas.LocalSetup seed-bidops-state-grid.",
        CreatedAt = now
    });

    UpsertRange(db,
        CreateStateGridChannel(BidOpsStateGridTenderChannelId, "sgcc-tender-announcements", "国家电网招标公告及投标邀请书", "TenderAnnouncement", "sgcc-menu:2018032700291334", now),
        CreateStateGridChannel(BidOpsStateGridProcurementChannelId, "sgcc-procurement-announcements", "国家电网采购公告", "ProcurementAnnouncement", "sgcc-menu:2018032900295987", now),
        CreateStateGridChannel(BidOpsStateGridCandidateChannelId, "sgcc-candidate-announcements", "国家电网推荐中标候选人公示", "CandidateAnnouncement", "sgcc-menu:2018060501171107", now),
        CreateStateGridChannel(BidOpsStateGridAwardChannelId, "sgcc-award-announcements", "国家电网中标（成交）结果公告", "AwardAnnouncement", "sgcc-menu:2018060501171111", now));
}

static TenantEntitlement CreateTenantPackageEntitlement(long id, string packageCode, DateTime now)
{
    return new TenantEntitlement
    {
        Id = id,
        TenantId = TenantId,
        SubjectType = AtlasEntitlementSubjectType.Tenant,
        SubjectId = TenantId,
        PackageCode = packageCode,
        CapabilityCode = null,
        Source = AtlasEntitlementSource.System,
        StartAtUtc = now.AddDays(-1),
        EndAtUtc = null,
        Status = AtlasEntitlementStatus.Active,
        CreatedAt = now
    };
}

static TenantEntitlement CreateBidOpsEntitlement(long id, string packageCode, DateTime now)
{
    return new TenantEntitlement
    {
        Id = id,
        TenantId = BidOpsTenantId,
        SubjectType = AtlasEntitlementSubjectType.Tenant,
        SubjectId = BidOpsTenantId,
        PackageCode = packageCode,
        CapabilityCode = null,
        Source = AtlasEntitlementSource.System,
        StartAtUtc = now.AddDays(-1),
        EndAtUtc = null,
        Status = AtlasEntitlementStatus.Active,
        CreatedAt = now
    };
}

static Permission CreateBidOpsPermission(
    long id,
    string code,
    string name,
    string capabilityCode,
    string resource,
    string action,
    AtlasPermissionRiskLevel riskLevel,
    DateTime now)
{
    return new Permission
    {
        Id = id,
        TenantId = BidOpsTenantId,
        Code = code,
        Name = name,
        CapabilityCode = capabilityCode,
        Module = BidOpsSystemValues.ModuleName,
        Scope = PermissionScope.Tenant,
        Resource = resource,
        Action = action,
        IsAssignable = true,
        IsSystem = false,
        RiskLevel = riskLevel,
        IsBuiltIn = true,
        IsEnabled = true,
        CreatedAt = now
    };
}

static RolePermission CreateBidOpsRolePermission(
    long id,
    long permissionId,
    AtlasDataScopeType dataScopeType,
    DateTime now)
{
    return new RolePermission
    {
        Id = id,
        TenantId = BidOpsTenantId,
        RoleId = BidOpsOperatorRoleId,
        PermissionId = permissionId,
        Effect = RolePermissionEffect.Allow,
        DataScopeType = dataScopeType,
        DataScopeJson = null,
        GrantedAt = now,
        GrantedBy = BidOpsAdminUserId,
        CreatedAt = now
    };
}

static CrawlChannel CreateStateGridChannel(
    long id,
    string code,
    string name,
    string noticeType,
    string listUrl,
    DateTime now)
{
    return new CrawlChannel
    {
        Id = id,
        TenantId = BidOpsTenantId,
        SourceId = BidOpsStateGridSourceId,
        Code = code,
        Name = name,
        NoticeType = noticeType,
        ListUrl = listUrl,
        Region = "CN",
        Industry = "Power",
        Enabled = true,
        CreatedAt = now
    };
}

static Store CreateStore(long id, string code, string name, StoreType type, long? parentStoreId, DateTime now)
{
    return new Store
    {
        Id = id,
        TenantId = TenantId,
        Code = code,
        Name = name,
        Type = type,
        ParentStoreId = parentStoreId,
        IsActive = true,
        Address = $"{name} 地址",
        ContactPhone = "021-10000000",
        ContactPerson = $"{name}联系人",
        Province = "上海",
        City = "上海",
        District = "浦东新区",
        Status = StoreStatus.Active,
        Version = 1,
        CreatedAt = now
    };
}

static User CreateUser(
    long id,
    string userName,
    string realName,
    long defaultStoreId,
    UserType type,
    string passwordHash,
    DateTime now)
{
    return new User
    {
        Id = id,
        TenantId = TenantId,
        UserName = userName,
        PasswordHash = passwordHash,
        RealName = realName,
        NickName = realName,
        Phone = $"138{id % 100000000:00000000}",
        Email = $"{userName}@atlas.local",
        Gender = Gender.Unknown,
        TokenVersion = 1,
        Type = type,
        Status = UserStatus.Active,
        IsActivated = true,
        DefaultStoreId = defaultStoreId,
        EmployeeNo = userName.ToUpperInvariant(),
        Position = type == UserType.TenantAdmin ? "租户管理员" : "店长",
        MustChangePassword = false,
        Version = 1,
        CreatedAt = now
    };
}

static UserStore CreateUserStore(long id, long userId, long storeId, bool isPrimary, DateTime now)
{
    return new UserStore
    {
        Id = id,
        TenantId = TenantId,
        UserId = userId,
        StoreId = storeId,
        IsPrimary = isPrimary,
        Permission = "Admin",
        EffectiveFrom = now.AddDays(-1),
        CreatedAt = now
    };
}

static Permission CreatePermission(
    long id,
    string code,
    string name,
    string capabilityCode,
    string module,
    string resource,
    string action,
    DateTime now)
{
    return new Permission
    {
        Id = id,
        TenantId = TenantId,
        Code = code,
        Name = name,
        CapabilityCode = capabilityCode,
        Module = module,
        Scope = PermissionScope.Store,
        Resource = resource,
        Action = action,
        IsAssignable = true,
        IsSystem = false,
        RiskLevel = AtlasPermissionRiskLevel.Low,
        IsBuiltIn = true,
        IsEnabled = true,
        CreatedAt = now
    };
}

static Role CreateRole(
    long id,
    string code,
    string name,
    long storeId,
    string description,
    DateTime now)
{
    return new Role
    {
        Id = id,
        TenantId = TenantId,
        Code = code,
        Name = name,
        Description = description,
        Scope = PermissionScope.Store,
        StoreId = storeId,
        IsSystem = false,
        IsEnabled = true,
        CreatedAt = now
    };
}

static RolePermission CreateRolePermission(
    long id,
    long roleId,
    long permissionId,
    AtlasDataScopeType dataScopeType,
    DateTime now)
{
    return new RolePermission
    {
        Id = id,
        TenantId = TenantId,
        RoleId = roleId,
        PermissionId = permissionId,
        Effect = RolePermissionEffect.Allow,
        DataScopeType = dataScopeType,
        DataScopeJson = null,
        GrantedAt = now,
        GrantedBy = HqUserId,
        CreatedAt = now
    };
}

static UserRole CreateUserRole(long id, long userId, long roleId, long storeId, DateTime now)
{
    return new UserRole
    {
        Id = id,
        TenantId = TenantId,
        UserId = userId,
        RoleId = roleId,
        StoreId = storeId,
        GrantedAt = now,
        GrantedBy = HqUserId,
        CreatedAt = now
    };
}

static Product CreateProduct(
    long id,
    long storeId,
    string name,
    decimal price,
    string description,
    long? sourceStoreId,
    bool isCustomized,
    DateTime now)
{
    return new Product
    {
        Id = id,
        TenantId = TenantId,
        StoreId = storeId,
        Name = name,
        Price = price,
        Description = description,
        SourceStoreId = sourceStoreId,
        IsCustomized = isCustomized,
        Version = 1,
        CreatedAt = now
    };
}

static Inventory CreateInventory(long id, long storeId, long productId, int quantity, DateTime now)
{
    return new Inventory
    {
        Id = id,
        TenantId = TenantId,
        StoreId = storeId,
        ProductId = productId,
        Quantity = quantity,
        SafetyStock = 5,
        CreatedAt = now
    };
}

static string? GetOption(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static bool HasFlag(string[] args, string name)
{
    return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
}

static string? ExtractDatabaseName(string connectionString)
{
    foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            (string.Equals(parts[0], "Database", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(parts[0], "Initial Catalog", StringComparison.OrdinalIgnoreCase)))
        {
            return parts[1];
        }
    }

    return null;
}

static string GetCommand(string[] args)
{
    return args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal))
        ?.Trim()
        .ToLowerInvariant() ?? "reset-demo";
}

static string FindRepositoryFile(string relativePath)
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current != null)
    {
        var candidate = Path.Combine(current.FullName, relativePath);
        if (File.Exists(candidate))
            return candidate;

        current = current.Parent;
    }

    current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current != null)
    {
        var candidate = Path.Combine(current.FullName, relativePath);
        if (File.Exists(candidate))
            return candidate;

        current = current.Parent;
    }

    throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
}

static void UpsertRange<TEntity>(DbContext db, params TEntity[] entities)
    where TEntity : BaseEntity
{
    foreach (var entity in entities)
    {
        var existing = db.Set<TEntity>().Find(entity.Id);
        if (existing == null)
        {
            db.Set<TEntity>().Add(entity);
            continue;
        }

        entity.CreatedAt = existing.CreatedAt == default ? entity.CreatedAt : existing.CreatedAt;
        entity.UpdatedAt = DateTime.UtcNow;
        db.Entry(existing).CurrentValues.SetValues(entity);
    }
}

static string MaskConnectionString(string connectionString)
{
    return Regex.Replace(
        connectionString,
        "(Password|Pwd)\\s*=\\s*[^;]*",
        "$1=***",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}

static void PrintDemoAccounts()
{
    Console.WriteLine();
    Console.WriteLine("Demo data initialized.");
    Console.WriteLine("Login domain: demo");
    Console.WriteLine($"Password: {DemoPassword}");
    Console.WriteLine("Accounts:");
    Console.WriteLine($"  hq_admin       defaultStore={HqStoreId} ({StoreType.Headquarters}), accessible={HqStoreId},{DirectAStoreId},{DirectBStoreId},{FranchiseStoreId}");
    Console.WriteLine($"  direct_a_mgr   store={DirectAStoreId} ({StoreType.DirectOperated}), product.read=SharedStores, inventory.read=CurrentStore");
    Console.WriteLine($"  franchise_mgr  store={FranchiseStoreId} ({StoreType.Franchised}), product.read=SharedStores, inventory.read=CurrentStore");
    Console.WriteLine("Entitlements: atlas.core, atlas.standard");
}

static void PrintUsage()
{
    Console.WriteLine("""
Atlas.LocalSetup commands:
  init-global       Create the local Global database if needed.
  create-tenant-db  Create the local tenant database if needed.
  seed-demo         Idempotently seed demo tenant, stores, users, products, and inventory.
  seed-local        Alias of seed-demo.
  seed-production   Create schema only; demo data is intentionally excluded.
  ensure-background-job-cancellation Ensure local BackgroundJobs termination columns and diagnostic result storage exist.
  seed-bidops-state-grid Idempotently seed BidOps local runtime and enqueue public State Grid scan jobs.
  bidops-status     Print BidOps local job and data counts.
  inspect-bidops-sgcc-notices Fetch public SGCC procurement detail URLs and print collectable fields as JSON.
  ensure-bidops-opportunities Ensure local BidOps opportunity tables exist for smoke testing.
  ensure-bidops-suppliers Ensure local BidOps supplier tables exist for smoke testing.
  ensure-bidops-matching Ensure local BidOps matching tables exist for smoke testing.
  ensure-bidops-pursuits Ensure local BidOps pursuit tables exist for smoke testing.
  ensure-bidops-outcomes Ensure local BidOps outcome supplier tables exist for smoke testing.
  ensure-bidops-procurement-details Ensure local BidOps procurement detail and lifecycle link tables exist.
  ensure-bidops-crawl-progress Ensure local BidOps crawl progress tables and channel schedule columns exist.
  ensure-bidops-ai-settings Ensure local BidOps AI runtime setting table exists.
  ensure-bidops-amount-candidates Ensure local BidOps amount candidate table exists.
  repair-bidops-data-quality Dry-run BidOps quality repair; add --confirm to update derived quality data.
  reset-bidops-derived-data Dry-run BidOps derived data cleanup; add --confirm to delete in dev DB.
  approve-bidops-pending Approve one pending BidOps review task in the local tenant.
  cancel-bidops-crawl-jobs Cancel pending/running local BidOps crawl jobs.
  reset-demo        Drop and recreate local demo databases, then seed demo data.

Options:
  --global <connection-string>  Overrides ATLAS_GLOBAL_CONNECTION.
  --tenant <connection-string>  Overrides ATLAS_TENANT_CONNECTION.
  --tenant-id <id>              Tenant id for BidOps repair/reset commands; defaults to BidOps local tenant.
  --url <detail-url>            Detail URL for inspect-bidops-sgcc-notices; may be repeated.
  --dry-run                     Print BidOps repair/reset counts without changing data.
  --confirm                     Required to update/delete BidOps derived quality data.
""");
}

sealed class LocalSetupGlobalDbContext : AtlasGlobalDbContext
{
    public LocalSetupGlobalDbContext(
        DbContextOptions<AtlasGlobalDbContext> options,
        SystemIdentity identity)
        : base(options, identity)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        LocalSetupModel.RemoveIndexes(modelBuilder);
    }
}

sealed class LocalSetupTenantDbContext : AtlasTenantDbContext
{
    public LocalSetupTenantDbContext(DbContextOptions<AtlasTenantDbContext> options)
        : base(options, new[] { typeof(BidOpsModule).Assembly })
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        LocalSetupModel.RemoveIndexes(modelBuilder);
    }
}

static class LocalSetupModel
{
    public static void RemoveIndexes(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var index in entityType.GetIndexes().ToList())
            {
                entityType.RemoveIndex(index.Properties);
            }
        }
    }
}
