// Models/Product.cs
using Atlas.Infrastructure.Caching.EntityFramework.Attributes;

namespace Atlas.Integration.Tests
{
    [Cacheable(Tags = new[] { "entity:Product" })]
    [CacheInvalidate(Tags = new[] { "entity:Product", "list:products" })]
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}