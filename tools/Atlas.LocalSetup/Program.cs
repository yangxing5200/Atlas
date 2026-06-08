using Atlas.Core.Entities.Global;
using Atlas.Core.Authorization;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Entities.Base;
using Atlas.Core.Enums;
using Atlas.Data.Common;
using Atlas.Data.Global;
using Atlas.Data.Tenant.Context;
using Microsoft.EntityFrameworkCore;
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
            $"Unknown command '{command}'. Use: init-global, create-tenant-db, seed-demo, seed-local, seed-production, reset-demo.");
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
        : base(options)
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
