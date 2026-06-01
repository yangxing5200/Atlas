using System.Reflection;
using Atlas.Extensions.DependencyInjection;
using Atlas.Services;
using Atlas.Services.Abstractions;
using Atlas.Services.Abstractions.Queries;
using Atlas.Services.Queries;
using Atlas.Services.Tenant;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Sample.ECommerce;

public sealed class SampleECommerceModule : AtlasModule
{
    public override string Name => "Atlas.Sample.ECommerce";

    public override IReadOnlyCollection<Assembly> ControllerAssemblies => Array.Empty<Assembly>();

    public override IReadOnlyCollection<Assembly> ConsumerAssemblies => Array.Empty<Assembly>();

    public override IReadOnlyCollection<Assembly> AutoMapperAssemblies => new[] { typeof(ProductService).Assembly };

    public override void AddServices(AtlasModuleContext context)
    {
        var services = context.Services;

        services.AddScoped<IProductQueryService, ProductQueryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderCommandService, OrderCommandService>();
    }
}
