using Atlas.Core.Entities.Global;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Data.Common;
using Atlas.Data.Global;
using Atlas.Data.Tenant.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

const long TenantId = 100001;
const long DatabaseInstanceId = 200001;

const long HqStoreId = 110001;
const long DirectAStoreId = 110011;
const long DirectBStoreId = 110012;
const long FranchiseStoreId = 110101;

const long HqUserId = 120001;
const long DirectUserId = 120011;
const long FranchiseUserId = 120101;

const string DemoPassword = "Pass1234!";

var globalConnection = GetOption(args, "--global")
    ?? "Server=localhost;Port=3306;Database=atlas_global;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;";
var tenantConnection = GetOption(args, "--tenant")
    ?? "Server=localhost;Port=3306;Database=atlas;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;";

Console.WriteLine("Resetting local Atlas databases...");
Console.WriteLine("Global: atlas_global");
Console.WriteLine("Tenant: atlas");

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

Console.WriteLine();
Console.WriteLine("Local database initialized.");
Console.WriteLine("Login domain: demo");
Console.WriteLine($"Password: {DemoPassword}");
Console.WriteLine("Accounts:");
Console.WriteLine($"  hq_admin       defaultStore={HqStoreId} ({StoreType.Headquarters}), accessible={HqStoreId},{DirectAStoreId},{DirectBStoreId},{FranchiseStoreId}");
Console.WriteLine($"  direct_a_mgr   store={DirectAStoreId} ({StoreType.DirectOperated})");
Console.WriteLine($"  franchise_mgr  store={FranchiseStoreId} ({StoreType.Franchised})");

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

    db.DatabaseMasterServers.Add(new DatabaseMasterServer
    {
        Id = 210001,
        Code = "local-master",
        NickName = "Local MySQL",
        CreatedAt = now
    });

    db.DatabaseServerConfigs.Add(new DatabaseServerConfig
    {
        Id = 210011,
        ServerCode = "local-master",
        NetworkEnvCode = NetworkEnvCodes.Default,
        DbType = "MySQL",
        ConnString = tenantConnection,
        CreatedAt = now
    });

    db.DatabaseInstances.Add(new DatabaseInstance
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

    db.Tenants.Add(new Tenant
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
}

static void SeedTenant(AtlasTenantDbContext db)
{
    var now = DateTime.UtcNow;
    var passwordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);

    db.Set<Store>().AddRange(
        CreateStore(HqStoreId, "HQ", "总部", StoreType.Headquarters, null, now),
        CreateStore(DirectAStoreId, "D-A", "直营一店", StoreType.DirectOperated, HqStoreId, now),
        CreateStore(DirectBStoreId, "D-B", "直营二店", StoreType.DirectOperated, HqStoreId, now),
        CreateStore(FranchiseStoreId, "F-A", "加盟一店", StoreType.Franchised, null, now));

    db.Set<User>().AddRange(
        CreateUser(HqUserId, "hq_admin", "总部管理员", HqStoreId, UserType.TenantAdmin, passwordHash, now),
        CreateUser(DirectUserId, "direct_a_mgr", "直营一店店长", DirectAStoreId, UserType.StoreManager, passwordHash, now),
        CreateUser(FranchiseUserId, "franchise_mgr", "加盟店店长", FranchiseStoreId, UserType.StoreManager, passwordHash, now));

    db.Set<UserStore>().AddRange(
        CreateUserStore(130001, HqUserId, HqStoreId, true, now),
        CreateUserStore(130002, HqUserId, DirectAStoreId, false, now),
        CreateUserStore(130003, HqUserId, DirectBStoreId, false, now),
        CreateUserStore(130004, HqUserId, FranchiseStoreId, false, now),
        CreateUserStore(130011, DirectUserId, DirectAStoreId, true, now),
        CreateUserStore(130101, FranchiseUserId, FranchiseStoreId, true, now));

    db.Set<Product>().AddRange(
        CreateProduct(140001, HqStoreId, "总部标准套餐", 199m, "总部维护的标准共享商品，直营范围可见。", null, false, now),
        CreateProduct(140011, DirectAStoreId, "直营一店限定套餐", 129m, "直营一店自定义商品，总部和直营兄弟店可见。", HqStoreId, true, now),
        CreateProduct(140012, DirectBStoreId, "直营二店限定套餐", 139m, "直营二店自定义商品，总部和直营兄弟店可见。", HqStoreId, true, now),
        CreateProduct(140101, FranchiseStoreId, "加盟一店自有套餐", 159m, "加盟店自有商品，只在加盟店范围内可见。", null, true, now));

    db.Set<Inventory>().AddRange(
        CreateInventory(150001, HqStoreId, 140001, 100, now),
        CreateInventory(150011, DirectAStoreId, 140011, 20, now),
        CreateInventory(150012, DirectBStoreId, 140012, 30, now),
        CreateInventory(150101, FranchiseStoreId, 140101, 40, now));
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
