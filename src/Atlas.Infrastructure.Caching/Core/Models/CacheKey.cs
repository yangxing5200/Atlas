// Core/Models/CacheKey.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Atlas.Infrastructure.Caching.Core.Models
{
    /// <summary>
    /// 缓存Key封装类
    /// </summary>
    public class CacheKey : IEquatable<CacheKey>
    {
        public string BaseKey { get; }
        public CacheScope Scope { get; }
        public IDictionary<string, string> ScopeValues { get; }
        public string FullKey { get; }

        public CacheKey(string baseKey, CacheScope scope = CacheScope.Global, IDictionary<string, string>? scopeValues = null)
        {
            BaseKey = baseKey ?? throw new ArgumentNullException(nameof(baseKey));
            Scope = scope;
            ScopeValues = scopeValues ?? new Dictionary<string, string>();
            FullKey = BuildFullKey();
        }

        private string BuildFullKey()
        {
            var parts = new List<string>();

            switch (Scope)
            {
                case CacheScope.Global:
                    parts.Add("G");
                    break;
                case CacheScope.Tenant:
                    parts.Add("T");
                    if (ScopeValues.TryGetValue("TenantId", out var tenantId))
                        parts.Add(tenantId);
                    break;
                case CacheScope.Store:
                    parts.Add("S");
                    if (ScopeValues.TryGetValue("TenantId", out var tid))
                        parts.Add(tid);
                    if (ScopeValues.TryGetValue("StoreId", out var storeId))
                        parts.Add(storeId);
                    break;
                case CacheScope.User:
                    parts.Add("U");
                    if (ScopeValues.TryGetValue("TenantId", out var t))
                        parts.Add(t);
                    if (ScopeValues.TryGetValue("UserId", out var userId))
                        parts.Add(userId);
                    break;
            }

            parts.Add(BaseKey);
            return string.Join(":", parts);
        }

        public bool Equals(CacheKey? other)
        {
            if (other is null) return false;
            return FullKey == other.FullKey;
        }

        public override bool Equals(object? obj) => Equals(obj as CacheKey);
        public override int GetHashCode() => FullKey.GetHashCode();
        public override string ToString() => FullKey;

        public static implicit operator string(CacheKey key) => key.FullKey;
    }
}