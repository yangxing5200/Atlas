using System.Reflection;
using Atlas.Core.Authorization;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Extensions.DependencyInjection;
using Atlas.Exporting;
using Atlas.Sample.ECommerce.BackgroundJobs;
using Atlas.Services;
using Atlas.Services.Abstractions;
using Atlas.Services.Abstractions.Queries;
using Atlas.Services.Queries;
using Atlas.Services.Tenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Sample.ECommerce;

public static class SampleECommercePermissionCodes
{
    public const string ProductsRead = "product.read";
    public const string ProductsCreate = "product.create";
    public const string ProductsUpdate = "product.update";
    public const string ProductsDelete = "product.delete";
    public const string ProductsExport = "product.export";
    public const string OrdersPlace = "order.place";
}

public static class SampleECommerceExportTaskTypes
{
    public const string ProductList = "sample.ecommerce.product.list";
}

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
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IExportTaskProvider, ProductListExportProvider>());
    }

    public override void ConfigureAuthorization(AtlasAuthorizationCatalogBuilder builder)
    {
        builder
            .AddPackage("atlas.standard", "Atlas Standard", AtlasPackageType.Edition)
            .AddCapability("product.catalog", "Product catalog", "ECommerce")
            .AddCapability("order.sales", "Sales orders", "ECommerce")
            .AddPermission(
                SampleECommercePermissionCodes.ProductsRead,
                "Read products",
                "product.catalog",
                "Product",
                PermissionScope.Store,
                resource: "product",
                action: "read")
            .AddPermission(
                SampleECommercePermissionCodes.ProductsCreate,
                "Create products",
                "product.catalog",
                "Product",
                PermissionScope.Store,
                resource: "product",
                action: "create",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                SampleECommercePermissionCodes.ProductsUpdate,
                "Update products",
                "product.catalog",
                "Product",
                PermissionScope.Store,
                resource: "product",
                action: "update",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                SampleECommercePermissionCodes.ProductsDelete,
                "Delete products",
                "product.catalog",
                "Product",
                PermissionScope.Store,
                resource: "product",
                action: "delete",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPermission(
                SampleECommercePermissionCodes.ProductsExport,
                "Export products",
                "product.catalog",
                "Product",
                PermissionScope.Store,
                resource: "product",
                action: "export",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                SampleECommercePermissionCodes.OrdersPlace,
                "Place orders",
                "order.sales",
                "Order",
                PermissionScope.Store,
                resource: "order",
                action: "place",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPackageCapability("atlas.standard", "product.catalog")
            .AddPackageCapability("atlas.standard", "order.sales")
            .AddMenuItem(
                "ecommerce.products",
                "Products",
                "/products",
                visibleWhen: AtlasAuthorizationCondition.RequirePermission(SampleECommercePermissionCodes.ProductsRead),
                sortOrder: 200)
            .AddDataResource(
                "product",
                "Product",
                entityType: typeof(Product).FullName,
                storeField: "StoreId",
                supportedScopes: new[]
                {
                    AtlasDataScopeType.CurrentStore,
                    AtlasDataScopeType.SharedStores
                })
            .AddDataResource(
                "inventory",
                "Inventory",
                entityType: typeof(Inventory).FullName,
                storeField: "StoreId",
                supportedScopes: new[] { AtlasDataScopeType.CurrentStore });
    }
}
