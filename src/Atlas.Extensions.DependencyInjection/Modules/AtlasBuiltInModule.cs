using System.Reflection;
using Atlas.Core.Authorization;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
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
using Atlas.Infrastructure.Security.DataMasking;
using Atlas.Services;
using Atlas.Services.Abstractions;
using Atlas.Services.Tenant;
using Atlas.Services.Tenant.Runtime.Messaging;
using Atlas.Services.Tenant.Runtime.Authorization;
using Atlas.Services.Tenant.Runtime.Migrations;
using Atlas.Services.Tenant.Runtime.Provisioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Atlas.Extensions.DependencyInjection;

internal sealed class AtlasBuiltInModule : AtlasModule
{
    public override string Name => "Atlas.BuiltIn";

    public override IReadOnlyCollection<Assembly> ControllerAssemblies => new[] { typeof(AtlasBuiltInModule).Assembly };

    public override IReadOnlyCollection<Assembly> ConsumerAssemblies => Array.Empty<Assembly>();

    public override IReadOnlyCollection<Assembly> AutoMapperAssemblies => new[] { typeof(UserService).Assembly };

    public override void AddServices(AtlasModuleContext context)
    {
        var services = context.Services;

        services.TryAddScoped<Atlas.Core.Context.ITenantExecutionContext>(sp => sp.GetRequiredService<ICurrentIdentity>());
        services.AddScoped<ITenantConnectionDirectory, TenantConnectionDirectory>();
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
        services.AddScoped<IUserStoreAccessService, UserStoreAccessService>();
        services.AddScoped<IUserSecurityAuditWriter, UserSecurityAuditWriter>();
        services.AddScoped<IUserLoginAuditWriter, UserLoginAuditWriter>();
        services.AddScoped<IUserPasswordService, UserPasswordService>();
        services.AddScoped<IUserAuthService, UserAuthService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IUserAssignmentService, UserAssignmentService>();
        services.AddScoped<IUserSessionService, UserSessionService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserSensitiveDataRevealService, UserSensitiveDataRevealService>();
        services.AddScoped<IUserLoginLogService, UserLoginLogService>();
        services.AddScoped<IOperationLogService, OperationLogService>();
        services.AddScoped<IAuditEventService, AuditEventService>();
        services.AddScoped<ISensitiveDataAccessAuditService, SensitiveDataAccessAuditService>();
        services.AddScoped<IAuthorizationCatalogSyncService, AuthorizationCatalogSyncService>();
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddSingleton<IAtlasAuthorizationConditionEvaluator, AtlasAuthorizationConditionEvaluator>();
        services.AddScoped<IAtlasDataAccessEvaluator, AtlasDataAccessEvaluator>();
        services.AddScoped<IAtlasDataScopePredicateBuilder, AtlasDataScopePredicateBuilder>();
        services.AddScoped<AuthorizationRuntimeService>();
        services.AddScoped<IAtlasAuthorizationContextService>(sp => sp.GetRequiredService<AuthorizationRuntimeService>());
        services.AddScoped<IAtlasAuthorizationManagementService>(sp => sp.GetRequiredService<AuthorizationRuntimeService>());
        services.AddScoped<RbacPermissionService>();
        services.AddScoped<IPermissionChecker>(sp => sp.GetRequiredService<RbacPermissionService>());
        services.AddScoped<IRbacSeedService>(sp => sp.GetRequiredService<RbacPermissionService>());
        services.AddScoped<IAtlasPermissionCacheInvalidator>(sp => sp.GetRequiredService<RbacPermissionService>());
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
    }

    public override void ConfigureAuthorization(AtlasAuthorizationCatalogBuilder builder)
    {
        builder
            .AddPackage("atlas.core", "Atlas Core", AtlasPackageType.Edition, "Atlas built-in platform capabilities")
            .AddCapability("tenant.management", "Tenant management", "Platform")
            .AddCapability("identity.users", "User management", "Security")
            .AddCapability("security.roles", "Role management", "Security")
            .AddCapability("store.management", "Store management", "Organization")
            .AddCapability("audit.security", "Security audit", "Security")
            .AddCapability("sensitive.data", "Sensitive data governance", "Security")
            .AddCapability("authorization.governance", "Authorization governance", "Security")
            .AddPermission(
                AtlasPermissionCodes.TenantAdmin,
                "Tenant administration",
                "tenant.management",
                "Tenant",
                PermissionScope.Tenant,
                resource: "tenant",
                action: "admin",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPermission(
                AtlasPermissionCodes.IdentitySelf,
                "Manage own identity session",
                "identity.users",
                "User",
                PermissionScope.Tenant,
                resource: "identity",
                action: "self",
                isAssignable: false)
            .AddPermission(
                AtlasPermissionCodes.TenantProvisioning,
                "Provision tenants",
                "tenant.management",
                "Tenant",
                PermissionScope.Platform,
                resource: "tenant",
                action: "provision",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPermission(
                AtlasPermissionCodes.UsersRead,
                "Read users",
                "identity.users",
                "User",
                PermissionScope.Tenant,
                resource: "user",
                action: "read")
            .AddPermission(
                AtlasPermissionCodes.UsersManage,
                "Manage users",
                "identity.users",
                "User",
                PermissionScope.Tenant,
                resource: "user",
                action: "manage",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPermission(
                AtlasPermissionCodes.RolesManage,
                "Manage roles and permissions",
                "security.roles",
                "Security",
                PermissionScope.Tenant,
                resource: "role",
                action: "manage",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPermission(
                AtlasPermissionCodes.StoresRead,
                "Read stores",
                "store.management",
                "Store",
                PermissionScope.Store,
                resource: "store",
                action: "read")
            .AddPermission(
                AtlasPermissionCodes.StoresManage,
                "Manage stores",
                "store.management",
                "Store",
                PermissionScope.Store,
                resource: "store",
                action: "manage",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPermission(
                AtlasPermissionCodes.AuditRead,
                "Read audit events",
                "audit.security",
                "Audit",
                PermissionScope.Tenant,
                resource: "audit-event",
                action: "read",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                AtlasPermissionCodes.UsersSensitiveReveal,
                "Reveal user sensitive data",
                "sensitive.data",
                "User",
                PermissionScope.Tenant,
                resource: "user",
                action: "sensitive.reveal",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPermission(
                AtlasPermissionCodes.AuditSensitiveReveal,
                "Reveal audit sensitive data",
                "sensitive.data",
                "Audit",
                PermissionScope.Tenant,
                resource: "audit-event",
                action: "sensitive.reveal",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPermission(
                AtlasPermissionCodes.SensitiveDataExport,
                "Export sensitive data",
                "sensitive.data",
                "Security",
                PermissionScope.Tenant,
                resource: "sensitive-data",
                action: "export",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPermission(
                AtlasPermissionCodes.AuthorizationRead,
                "Read authorization catalog and diagnostics",
                "authorization.governance",
                "Security",
                PermissionScope.Tenant,
                resource: "authorization",
                action: "read",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                AtlasPermissionCodes.AuthorizationManage,
                "Manage entitlements and role permissions",
                "authorization.governance",
                "Security",
                PermissionScope.Tenant,
                resource: "authorization",
                action: "manage",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPackageCapability("atlas.core", "tenant.management")
            .AddPackageCapability("atlas.core", "identity.users")
            .AddPackageCapability("atlas.core", "security.roles")
            .AddPackageCapability("atlas.core", "store.management")
            .AddPackageCapability("atlas.core", "audit.security")
            .AddPackageCapability("atlas.core", "sensitive.data")
            .AddPackageCapability("atlas.core", "authorization.governance")
            .AddMenuItem(
                "admin.users",
                "Users",
                "/admin/users",
                visibleWhen: AtlasAuthorizationCondition.RequirePermission(AtlasPermissionCodes.UsersRead),
                sortOrder: 100)
            .AddMenuItem(
                "admin.stores",
                "Stores",
                "/admin/stores",
                visibleWhen: AtlasAuthorizationCondition.RequirePermission(AtlasPermissionCodes.StoresRead),
                sortOrder: 110)
            .AddDataResource(
                "user",
                "User",
                entityType: typeof(User).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant, AtlasDataScopeType.AssignedStores })
            .AddDataResource(
                "store",
                "Store",
                entityType: typeof(Store).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant, AtlasDataScopeType.AssignedStores })
            .AddDataResource(
                "audit-event",
                "Audit event",
                entityType: typeof(AuditEvent).FullName,
                storeField: "StoreId",
                ownerField: "UserId",
                supportedScopes: new[]
                {
                    AtlasDataScopeType.AllTenant,
                    AtlasDataScopeType.CurrentStore,
                    AtlasDataScopeType.SharedStores,
                    AtlasDataScopeType.Own
                });
    }
}
