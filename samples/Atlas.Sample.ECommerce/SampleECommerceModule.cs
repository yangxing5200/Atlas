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
    public const string InventoriesRead = "inventory.read";
    public const string OrdersPlace = "order.place";
}

public static class SampleECommerceExportTaskTypes
{
    public const string ProductList = "sample.ecommerce.product.list";
}

public static class SampleECommerceAuthorizationCodes
{
    public const string StandardPackage = "atlas.standard";
    public const string ProductCatalogCapability = "product.catalog";
    public const string InventoryStockCapability = "inventory.stock";
    public const string OrderSalesCapability = "order.sales";
    public const string ProductResource = "product";
    public const string InventoryResource = "inventory";
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
            .AddPackage(SampleECommerceAuthorizationCodes.StandardPackage, "Atlas Standard", AtlasPackageType.Edition)
            .AddCapability(SampleECommerceAuthorizationCodes.ProductCatalogCapability, "Product catalog", "ECommerce")
            .AddCapability(SampleECommerceAuthorizationCodes.InventoryStockCapability, "Inventory stock", "ECommerce")
            .AddCapability(SampleECommerceAuthorizationCodes.OrderSalesCapability, "Sales orders", "ECommerce")
            .AddPermission(
                SampleECommercePermissionCodes.ProductsRead,
                "Read products",
                SampleECommerceAuthorizationCodes.ProductCatalogCapability,
                "Product",
                PermissionScope.Store,
                resource: SampleECommerceAuthorizationCodes.ProductResource,
                action: "read")
            .AddPermission(
                SampleECommercePermissionCodes.ProductsCreate,
                "Create products",
                SampleECommerceAuthorizationCodes.ProductCatalogCapability,
                "Product",
                PermissionScope.Store,
                resource: SampleECommerceAuthorizationCodes.ProductResource,
                action: "create",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                SampleECommercePermissionCodes.ProductsUpdate,
                "Update products",
                SampleECommerceAuthorizationCodes.ProductCatalogCapability,
                "Product",
                PermissionScope.Store,
                resource: SampleECommerceAuthorizationCodes.ProductResource,
                action: "update",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                SampleECommercePermissionCodes.ProductsDelete,
                "Delete products",
                SampleECommerceAuthorizationCodes.ProductCatalogCapability,
                "Product",
                PermissionScope.Store,
                resource: SampleECommerceAuthorizationCodes.ProductResource,
                action: "delete",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPermission(
                SampleECommercePermissionCodes.ProductsExport,
                "Export products",
                SampleECommerceAuthorizationCodes.ProductCatalogCapability,
                "Product",
                PermissionScope.Store,
                resource: SampleECommerceAuthorizationCodes.ProductResource,
                action: "export",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                SampleECommercePermissionCodes.InventoriesRead,
                "Read inventory",
                SampleECommerceAuthorizationCodes.InventoryStockCapability,
                "Inventory",
                PermissionScope.Store,
                resource: SampleECommerceAuthorizationCodes.InventoryResource,
                action: "read")
            .AddPermission(
                SampleECommercePermissionCodes.OrdersPlace,
                "Place orders",
                SampleECommerceAuthorizationCodes.OrderSalesCapability,
                "Order",
                PermissionScope.Store,
                resource: "order",
                action: "place",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPackageCapability(
                SampleECommerceAuthorizationCodes.StandardPackage,
                SampleECommerceAuthorizationCodes.ProductCatalogCapability)
            .AddPackageCapability(
                SampleECommerceAuthorizationCodes.StandardPackage,
                SampleECommerceAuthorizationCodes.InventoryStockCapability)
            .AddPackageCapability(
                SampleECommerceAuthorizationCodes.StandardPackage,
                SampleECommerceAuthorizationCodes.OrderSalesCapability)
            .AddMenuItem(
                "ecommerce.products",
                "Products",
                "/products",
                visibleWhen: AtlasAuthorizationCondition.RequirePermission(SampleECommercePermissionCodes.ProductsRead),
                sortOrder: 200)
            .AddDataResource(
                SampleECommerceAuthorizationCodes.ProductResource,
                "Product",
                entityType: typeof(Product).FullName,
                storeField: "StoreId",
                supportedScopes: new[]
                {
                    AtlasDataScopeType.CurrentStore,
                    AtlasDataScopeType.SharedStores
                })
            .AddDataResource(
                SampleECommerceAuthorizationCodes.InventoryResource,
                "Inventory",
                entityType: typeof(Inventory).FullName,
                storeField: "StoreId",
                supportedScopes: new[] { AtlasDataScopeType.CurrentStore });
    }
}
