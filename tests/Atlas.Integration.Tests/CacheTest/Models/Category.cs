// Models/Category.cs
using Atlas.Infrastructure.Caching.EntityFramework.Attributes;
using Castle.Core.Resource;

namespace Atlas.Integration.Tests
{
    [Cacheable(Tags = new[] { "entity:Category" })]
    [CacheInvalidate(Tags = new[] { "entity:Category", "list:categories" })]
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    [Cacheable]
    [CacheInvalidate(Tags = new[] { "entity:Order", "list:orders" })]
    public class Order
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending";
    }

    [Cacheable(Tags = new[] { "entity:Customer" })]
    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }

    /// <summary>
    /// 用于缓存的 DTO，避免循环引用
    /// </summary>
    public class CategoryWithProductsDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TenantId { get; set; } = string.Empty;

        // ✅ 使用简化的 Product DTO，不包含 Category 引用
        public List<ProductSummaryDto> Products { get; set; } = new();
    }

    public class ProductSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        // ❌ 不包含 Category 属性，避免循环引用
    }

    /// <summary>
    /// 分类摘要 DTO（不包含产品列表）
    /// </summary>
    public class CategorySummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
    /// <summary>
    /// 产品详情 DTO（包含分类信息，但不包含反向导航）
    /// </summary>
    public class ProductDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // 分类信息（简化版，不包含产品列表）
        public CategorySummaryDto? Category { get; set; }
    }
}