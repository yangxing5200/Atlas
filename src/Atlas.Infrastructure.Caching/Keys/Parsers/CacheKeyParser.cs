// Keys/Parsers/CacheKeyParser.cs
using System;
using System.Linq;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Keys.Conventions;

namespace Atlas.Infrastructure.Caching.Keys.Parsers
{
    public class CacheKeyParser : ICacheKeyParser
    {
        public KeyMetadata Parse(string key)
        {
            if (!TryParse(key, out var metadata) || metadata == null)
            {
                throw new ArgumentException($"Invalid cache key format: {key}", nameof(key));
            }
            return metadata;
        }

        public bool TryParse(string key, out KeyMetadata? metadata)
        {
            metadata = null;

            if (string.IsNullOrWhiteSpace(key))
                return false;

            var firstSeparatorIndex = key.IndexOf(KeySeparators.Scope);
            if (firstSeparatorIndex < 0 || firstSeparatorIndex >= key.Length - 1)
                return false;

            var prefix = key.Substring(0, firstSeparatorIndex);
            var remainder = key.Substring(firstSeparatorIndex + 1);

            var scope = prefix switch
            {
                KeyPrefixes.Global => CacheScope.Global,
                KeyPrefixes.Tenant => CacheScope.Tenant,
                KeyPrefixes.Store => CacheScope.Store,
                KeyPrefixes.User => CacheScope.User,
                _ => (CacheScope?)null
            };

            if (!scope.HasValue)
                return false;

            var segments = remainder.Split(KeySeparators.Segment);

            metadata = new KeyMetadata
            {
                OriginalKey = key,
                Scope = scope.Value,
                Prefix = prefix
            };

            switch (scope.Value)
            {
                case CacheScope.Global:
                    // Global: G:product:123 → baseKey = "product:123"
                    metadata.BaseKey = remainder;
                    break;

                case CacheScope.Tenant:
                    // Tenant: T:tenant1:product:123 → tenantId="tenant1", baseKey="product:123"
                    if (segments.Length < 2) return false;
                    metadata.TenantId = segments[0];
                    metadata.BaseKey = string.Join(KeySeparators.Segment.ToString(), segments.Skip(1));
                    break;

                case CacheScope.Store:
                    // Store: S:tenant1:store1:product:123 
                    //    → tenantId="tenant1", storeId="store1", baseKey="product:123"
                    if (segments.Length < 3) return false;
                    metadata.TenantId = segments[0];
                    metadata.StoreId = segments[1];
                    metadata.BaseKey = string.Join(KeySeparators.Segment.ToString(), segments.Skip(2));
                    break;

                case CacheScope.User:
                    // User: U:tenant1:user1:product:123
                    //    → tenantId="tenant1", userId="user1", baseKey="product:123"
                    if (segments.Length < 3) return false;
                    metadata.TenantId = segments[0];
                    metadata.UserId = segments[1];
                    metadata.BaseKey = string.Join(KeySeparators.Segment.ToString(), segments.Skip(2));
                    break;
            }

            return true;
        }

        public CacheScope ExtractScope(string key)
        {
            var metadata = Parse(key);
            return metadata.Scope;
        }

        public string? ExtractTenantId(string key)
        {
            if (TryParse(key, out var metadata))
            {
                return metadata?.TenantId;
            }
            return null;
        }

        public string? ExtractStoreId(string key)
        {
            if (TryParse(key, out var metadata))
            {
                return metadata?.StoreId;
            }
            return null;
        }

        public string? ExtractUserId(string key)
        {
            if (TryParse(key, out var metadata))
            {
                return metadata?.UserId;
            }
            return null;
        }
    }

    public class KeyMetadata
    {
        public string OriginalKey { get; set; } = string.Empty;
        public CacheScope Scope { get; set; }
        public string Prefix { get; set; } = string.Empty;
        public string BaseKey { get; set; } = string.Empty;
        public string? TenantId { get; set; }
        public string? StoreId { get; set; }
        public string? UserId { get; set; }
    }
}