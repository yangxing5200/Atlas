using Atlas.Data.Common;
using Atlas.Data.Global;
using Atlas.Data.Global.Seeds;
using Atlas.Data.Tenant;
using Atlas.Data.Tenant.Seeds;
using Atlas.Extensions.DependencyInjection;
using Atlas.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Integration.Tests;

public class IntergrationTests : IntegrationTestBase
{
    protected override void ConfigureServices(IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
    
        services.AddAtlasCore(configuration);
        services.AddScoped<GlobalDataSeeder>();
        services.AddScoped<TenantDataSeeder>();
    }

    [Fact]
    public async Task GlobalDataSeederTest()
    {
        // 1. 获取种子生成器
        var seeder = GetService<GlobalDataSeeder>();

        // 2. 执行种子数据
        await seeder.SeedAsync();

        // 3. 验证
        var context = GetService<AtlasGlobalDbContext>();
        Assert.True(await context.Tenants.AnyAsync());
    }


    [Fact]
    public async Task SimpleTest()
    {

        // 1. 获取种子生成器
        var seeder = GetService<TenantDataSeeder>();

        // 2. 执行种子数据
        await seeder.SeedAsync();

        // 3. 验证
        var context = GetService<AtlasTenantDbContext>();
    }


}