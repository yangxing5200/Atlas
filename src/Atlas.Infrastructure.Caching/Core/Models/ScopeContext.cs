namespace Atlas.Infrastructure.Caching.Core.Models
{
    /// <summary>
    /// 作用域上下文
    /// </summary>
    public class ScopeContext
    {
        public string? TenantId { get; set; }
        public string? StoreId { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }

        public bool HasTenant => !string.IsNullOrEmpty(TenantId);
        public bool HasStore => !string.IsNullOrEmpty(StoreId);
        public bool HasUser => !string.IsNullOrEmpty(UserId);

        public static ScopeContext Empty => new();
    }
}