using System;

namespace Atlas.Infrastructure.Caching.Core.Models.Registry
{
    /// <summary>
    /// Business-related cache key definitions.
    /// These keys are used for caching business entities and operations.
    /// </summary>
    public static class BusinessCacheKeys
    {
        /// <summary>
        /// Category name for registration.
        /// </summary>
        public const string Category = "Business";

        /// <summary>
        /// Product base information cache.
        /// </summary>
        public static readonly CacheKeyDefinition ProductBase = CacheKeyDefinition
            .Create("product:{id}")
            .WithScope(CacheScope.Tenant)
            .WithInstanceKey("id")
            .WithExpiration(TimeSpan.FromMinutes(30))
            .WithTagGenerator((ctx, instance) => new[] { "product", $"product:{instance}" })
            .WithDescription("Product base information cache")
            .Build();

        /// <summary>
        /// Product list cache.
        /// </summary>
        public static readonly CacheKeyDefinition ProductList = CacheKeyDefinition
            .Create("product:list")
            .WithScope(CacheScope.Tenant)
            .WithExpiration(CacheExpirations.OneHour)
            .WithTagGenerator((ctx, _) => new[] { "product" })
            .WithDescription("Product list cache (tenant isolated)")
            .Build();

        /// <summary>
        /// Product stock cache.
        /// </summary>
        public static readonly CacheKeyDefinition ProductStock = CacheKeyDefinition
            .Create("stock:{productId}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("productId")
            .WithExpiration(TimeSpan.FromSeconds(30))
            .WithTagGenerator((ctx, instance) => new[] { "stock", $"stock:{instance}" })
            .WithDescription("Product stock cache (global shared)")
            .Build();

        /// <summary>
        /// Category tree cache.
        /// </summary>
        public static readonly CacheKeyDefinition CategoryTree = CacheKeyDefinition
            .Create("category:tree")
            .WithScope(CacheScope.Global)
            .WithExpiration(TimeSpan.FromHours(24))
            .WithTagGenerator((ctx, _) => new[] { "category" })
            .WithDescription("Product category tree cache")
            .Build();

        /// <summary>
        /// Order information cache.
        /// </summary>
        public static readonly CacheKeyDefinition OrderInfo = CacheKeyDefinition
            .Create("order:{orderId}")
            .WithScope(CacheScope.User)
            .WithInstanceKey("orderId")
            .WithExpiration(TimeSpan.FromMinutes(5))
            .WithTagGenerator((ctx, instance) => new[]
            {
                "order",
                $"order:{instance}",
                $"user-order:{ctx.UserId ?? string.Empty}"
            })
            .WithDescription("Order information cache (user isolated)")
            .Build();

        /// <summary>
        /// User cart cache.
        /// </summary>
        public static readonly CacheKeyDefinition UserCart = CacheKeyDefinition
            .Create("cart:{userId}")
            .WithScope(CacheScope.User)
            .WithInstanceKey("userId")
            .WithExpiration(TimeSpan.FromHours(24))
            .WithTagGenerator((ctx, instance) => new[] { "cart", $"user:{instance}" })
            .WithDescription("User cart cache (user isolated)")
            .Build();

        /// <summary>
        /// Registers all business cache keys with the registry.
        /// Should be called at application startup.
        /// </summary>
        public static void RegisterAll()
        {
            CacheKeyRegistry.Register("Business.ProductBase", ProductBase, Category);
            CacheKeyRegistry.Register("Business.ProductList", ProductList, Category);
            CacheKeyRegistry.Register("Business.ProductStock", ProductStock, Category);
            CacheKeyRegistry.Register("Business.CategoryTree", CategoryTree, Category);
            CacheKeyRegistry.Register("Business.OrderInfo", OrderInfo, Category);
            CacheKeyRegistry.Register("Business.UserCart", UserCart, Category);
        }
    }
}
