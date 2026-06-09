using Atlas.Core.Authorization;
using Atlas.Core.Enums;
using Atlas.Exporting;
using Atlas.Extensions.DependencyInjection;
using Atlas.ModuleTemplate.BackgroundJobs;
using Atlas.ModuleTemplate.Entities;
using Atlas.ModuleTemplate.Queries;
using Atlas.ModuleTemplate.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.ModuleTemplate;

public static class ModuleTemplatePermissionCodes
{
    public const string TenantRecordsRead = "module-template.tenant-record.read";
    public const string TenantRecordsCreate = "module-template.tenant-record.create";
    public const string TenantRecordsUpdate = "module-template.tenant-record.update";
    public const string TenantRecordsExport = "module-template.tenant-record.export";
}

public static class ModuleTemplateExportTaskTypes
{
    public const string TenantRecordList = "module-template.tenant-record.list";
}

public sealed class ModuleEntry : AtlasModule
{
    public override void AddServices(AtlasModuleContext context)
    {
        context.Services.AddScoped<ITenantRecordService, TenantRecordService>();
        context.Services.AddScoped<ITenantRecordQueryService, TenantRecordQueryService>();
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IExportTaskProvider, TenantRecordListExportProvider>());
    }

    public override void ConfigureAuthorization(AtlasAuthorizationCatalogBuilder builder)
    {
        builder
            .AddPackage("atlas.standard", "Atlas Standard", AtlasPackageType.Edition)
            .AddCapability("module-template.tenant-record", "Tenant records", "ModuleTemplate")
            .AddPermission(
                ModuleTemplatePermissionCodes.TenantRecordsRead,
                "Read tenant records",
                "module-template.tenant-record",
                "ModuleTemplate",
                PermissionScope.Tenant,
                resource: "module-template.tenant-record",
                action: "read")
            .AddPermission(
                ModuleTemplatePermissionCodes.TenantRecordsCreate,
                "Create tenant records",
                "module-template.tenant-record",
                "ModuleTemplate",
                PermissionScope.Tenant,
                resource: "module-template.tenant-record",
                action: "create",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                ModuleTemplatePermissionCodes.TenantRecordsUpdate,
                "Update tenant records",
                "module-template.tenant-record",
                "ModuleTemplate",
                PermissionScope.Tenant,
                resource: "module-template.tenant-record",
                action: "update",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                ModuleTemplatePermissionCodes.TenantRecordsExport,
                "Export tenant records",
                "module-template.tenant-record",
                "ModuleTemplate",
                PermissionScope.Tenant,
                resource: "module-template.tenant-record",
                action: "export",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPackageCapability("atlas.standard", "module-template.tenant-record")
            .AddMenuItem(
                "module-template.tenant-records",
                "Tenant records",
                "/tenant-records",
                visibleWhen: AtlasAuthorizationCondition.RequirePermission(ModuleTemplatePermissionCodes.TenantRecordsRead))
            .AddDataResource(
                "module-template.tenant-record",
                "Tenant record",
                entityType: typeof(TenantRecord).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant });
    }
}
