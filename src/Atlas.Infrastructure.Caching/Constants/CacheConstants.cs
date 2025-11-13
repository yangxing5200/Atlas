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
    }
}