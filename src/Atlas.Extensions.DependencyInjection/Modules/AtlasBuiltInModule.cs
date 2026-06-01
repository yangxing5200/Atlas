using System.Reflection;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Global.Repositories;
using Atlas.Data.Global.Repositories.Impl;
using Atlas.Data.Global.UnitOfWork;
using Atlas.Data.Tenant;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Identity;
using Atlas.Data.Tenant.Providers;
using Atlas.Data.Tenant.Repositories;
using Atlas.Data.Tenant.Repositories.Impl;
using Atlas.Data.Tenant.Sql;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Common.Tenants;
using Atlas.Infrastructure.Security;
using Atlas.Infrastructure.Security.Permissions;
using Atlas.Services;
using Atlas.Services.Abstractions;
using Atlas.Services.Tenant;
using Atlas.Services.Tenant.Runtime.Messaging;
using Atlas.Services.Tenant.Runtime.Migrations;
using Atlas.Services.Tenant.Runtime.Provisioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Atlas.Extensions.DependencyInjection;

internal sealed class AtlasBuiltInModule : AtlasModule
{
    public override string Name => "Atlas.BuiltIn";

    public override IReadOnlyCollection<Assembly> ControllerAssemblies => Array.Empty<Assembly>();

    public override IReadOnlyCollection<Assembly> ConsumerAssemblies => Array.Empty<Assembly>();

    public override IReadOnlyCollection<Assembly> AutoMapperAssemblies => new[] { typeof(UserService).Assembly };

    public override void AddServices(AtlasModuleContext context)
    {
        var services = context.Services;

        services.AddScoped<ITenantDbConnProvider, TenantDbConnProvider>();
        services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

        services.AddScoped<IDataScope>(sp =>
        {
            var cache = sp.GetRequiredService<ICacheService>();
            var identity = sp.GetRequiredService<ICurrentIdentity>();
            var dbFactory = sp.GetRequiredService<ITenantDbContextFactory>();
            var logger = sp.GetRequiredService<ILogger<DataScope>>();

            return new DataScope(
                new Lazy<ICacheService>(() => cache),
                identity,
                dbFactory,
                logger);
        });
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantSchemaMigrationStateRepository, TenantSchemaMigrationStateRepository>();
        services.AddScoped<IGlobalUnitOfWork, GlobalUnitOfWork>();

        services.AddScoped<IUnitOfWork, TenantUnitOfWork>();
        services.AddScoped<ITenantSqlExecutor, TenantSqlExecutor>();
        services.AddScoped(typeof(IRepository<>), typeof(RepositoryBase<>));
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<IOperationLogRepository, OperationLogRepository>();
        services.TryAddSingleton<ITenantCodeGenerator, TenantCodeGenerator>();
        services.AddScoped<ITenantOutboxStore, TenantOutboxStore>();
        services.AddScoped<ITenantInboxStore, TenantInboxStore>();
        services.AddScoped<ITenantConsumerRuntime, TenantConsumerRuntime>();
        services.AddScoped<ITenantDomainEventOutbox, TenantDomainEventOutbox>();
        services.AddScoped<ITenantSchemaMigrationService, TenantSchemaMigrationService>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<IStoreService, StoreService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserLoginLogService, UserLoginLogService>();
        services.AddScoped<IOperationLogService, OperationLogService>();
        services.AddScoped<IAuditEventService, AuditEventService>();
        services.AddScoped<RbacPermissionService>();
        services.AddScoped<IPermissionChecker>(sp => sp.GetRequiredService<RbacPermissionService>());
        services.AddScoped<IRbacSeedService>(sp => sp.GetRequiredService<RbacPermissionService>());
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
    }
}
