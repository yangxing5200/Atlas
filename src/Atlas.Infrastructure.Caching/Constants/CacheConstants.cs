// Constants/CacheConstants.cs
using System;

namespace Atlas.Infrastructure.Caching.Constants
{
    public static class CacheConstants
    {
        public static class DefaultExpirations
        {
            public static readonly TimeSpan Short = TimeSpan.FromMinutes(5);
            public static readonly TimeSpan Medium = TimeSpan.FromMinutes(30);
            public static readonly TimeSpan Long = TimeSpan.FromHours(2);
            public static readonly TimeSpan VeryLong = TimeSpan.FromHours(24);
        }

        public static class Headers
        {
            public const string TenantId = "X-Tenant-Id";
            public const string StoreId = "X-Store-Id";
            public const string UserId = "X-User-Id";
        }

        public static class ClaimTypes
        {
            public const string TenantId = "tenant_id";
            public const string StoreId = "store_id";
            public const string UserId = "user_id";
        }
    }
}