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
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Microsoft.EntityFrameworkCore;
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
const long BidOpsCrawlReadPermissionId = 320401;
const long BidOpsCrawlManagePermissionId = 320402;
const long BidOpsCrawlImportPermissionId = 320403;
const long BidOpsReviewReadPermissionId = 320404;
const long BidOpsReviewApprovePermissionId = 320405;
const long BidOpsBusinessReadPermissionId = 320406;
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
    case "seed-bidops-state-grid":
        await SeedBidOpsStateGridAsync(globalConnection, tenantConnection);
        break;
    case "bidops-status":
        await PrintBidOpsStatusAsync(globalConnection, tenantConnection);
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
            $"Unknown command '{command}'. Use: init-global, create-tenant-db, seed-demo, seed-local, seed-production, seed-bidops-state-grid, bidops-status, approve-bidops-pending, cancel-bidops-crawl-jobs, reset-demo.");
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
        CreateBidOpsPermission(BidOpsCrawlReadPermissionId, BidOpsPermissionCodes.CrawlRead, "Read BidOps crawl data", BidOpsCapabilities.Crawl, BidOpsDataResources.RawNotice, "read", AtlasPermissionRiskLevel.Low, now),
        CreateBidOpsPermission(BidOpsCrawlManagePermissionId, BidOpsPermissionCodes.CrawlManage, "Manage BidOps crawl sources", BidOpsCapabilities.Crawl, BidOpsDataResources.CrawlSource, "manage", AtlasPermissionRiskLevel.Medium, now),
        CreateBidOpsPermission(BidOpsCrawlImportPermissionId, BidOpsPermissionCodes.CrawlImport, "Import public tender URL", BidOpsCapabilities.Crawl, BidOpsDataResources.RawNotice, "import", AtlasPermissionRiskLevel.Medium, now),
        CreateBidOpsPermission(BidOpsReviewReadPermissionId, BidOpsPermissionCodes.ReviewRead, "Read BidOps review tasks", BidOpsCapabilities.Review, BidOpsDataResources.ReviewTask, "read", AtlasPermissionRiskLevel.Low, now),
        CreateBidOpsPermission(BidOpsReviewApprovePermissionId, BidOpsPermissionCodes.ReviewApprove, "Approve BidOps staging data", BidOpsCapabilities.Review, BidOpsDataResources.ReviewTask, "approve", AtlasPermissionRiskLevel.High, now),
        CreateBidOpsPermission(BidOpsBusinessReadPermissionId, BidOpsPermissionCodes.BusinessRead, "Read BidOps formal tender data", BidOpsCapabilities.Business, BidOpsDataResources.Notice, "read", AtlasPermissionRiskLevel.Low, now));

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
        CreateBidOpsRolePermission(320501, BidOpsCrawlReadPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320502, BidOpsCrawlManagePermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320503, BidOpsCrawlImportPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320504, BidOpsReviewReadPermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320505, BidOpsReviewApprovePermissionId, AtlasDataScopeType.AllTenant, now),
        CreateBidOpsRolePermission(320506, BidOpsBusinessReadPermissionId, AtlasDataScopeType.AllTenant, now));

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
  reset-demo        Drop and recreate local demo databases, then seed demo data.

Options:
  --global <connection-string>  Overrides ATLAS_GLOBAL_CONNECTION.
  --tenant <connection-string>  Overrides ATLAS_TENANT_CONNECTION.
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
