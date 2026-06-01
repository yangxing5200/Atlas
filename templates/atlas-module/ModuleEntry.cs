using Atlas.Extensions.DependencyInjection;
using Atlas.ModuleTemplate.Queries;
using Atlas.ModuleTemplate.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.ModuleTemplate;

public sealed class ModuleEntry : AtlasModule
{
    public override void AddServices(AtlasModuleContext context)
    {
        context.Services.AddScoped<ITenantRecordService, TenantRecordService>();
        context.Services.AddScoped<ITenantRecordQueryService, TenantRecordQueryService>();
    }
}
