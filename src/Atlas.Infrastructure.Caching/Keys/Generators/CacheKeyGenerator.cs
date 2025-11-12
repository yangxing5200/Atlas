// Keys/Generators/CacheKeyGenerator.cs
using System;
using System.Collections.Generic;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Keys.Conventions;

namespace Atlas.Infrastructure.Caching.Keys.Generators
{
    public class CacheKeyGenerator : ICacheKeyGenerator
    {
        public string GenerateKey(string baseKey, CacheScope scope, IDictionary<string, string>? scopeValues = null)
        {
            ValidateBaseKey(baseKey);

            return scope switch
            {
                CacheScope.Global => GenerateGlobalKey(baseKey),
                CacheScope.Tenant => GenerateTenantKey(baseKey, GetValue(scopeValues, "TenantId")),
                CacheScope.Store => GenerateStoreKey(baseKey, GetValue(scopeValues, "TenantId"), GetValue(scopeValues, "StoreId")),
                CacheScope.User => GenerateUserKey(baseKey, GetValue(scopeValues, "TenantId"), GetValue(scopeValues, "UserId")),
                _ => throw new ArgumentException($"Unknown scope: {scope}")
            };
        }

        public string GenerateGlobalKey(string baseKey)
        {
            ValidateBaseKey(baseKey);
            return $"{KeyPrefixes.Global}{KeySeparators.Scope}{baseKey}";
        }

        public string GenerateTenantKey(string baseKey, string tenantId)
        {
            ValidateBaseKey(baseKey);
            ValidateScopeValue(tenantId, nameof(tenantId));
            return $"{KeyPrefixes.Tenant}{KeySeparators.Scope}{tenantId}{KeySeparators.Segment}{baseKey}";
        }

        public string GenerateStoreKey(string baseKey, string tenantId, string storeId)
        {
            ValidateBaseKey(baseKey);
            ValidateScopeValue(tenantId, nameof(tenantId));
            ValidateScopeValue(storeId, nameof(storeId));
            return $"{KeyPrefixes.Store}{KeySeparators.Scope}{tenantId}{KeySeparators.Segment}{storeId}{KeySeparators.Segment}{baseKey}";
        }

        public string GenerateUserKey(string baseKey, string tenantId, string userId)
        {
            ValidateBaseKey(baseKey);
            ValidateScopeValue(tenantId, nameof(tenantId));
            ValidateScopeValue(userId, nameof(userId));
            return $"{KeyPrefixes.User}{KeySeparators.Scope}{tenantId}{KeySeparators.Segment}{userId}{KeySeparators.Segment}{baseKey}";
        }

        private static string GetValue(IDictionary<string, string>? values, string key)
        {
            if (values == null || !values.TryGetValue(key, out var value))
            {
                throw new ArgumentException($"Required scope value '{key}' not provided");
            }
            return value;
        }

        private static void ValidateBaseKey(string baseKey)
        {
            if (string.IsNullOrWhiteSpace(baseKey))
                throw new ArgumentException("Base key cannot be null or empty", nameof(baseKey));

            // 允许 baseKey 包含冒号
            // baseKey 可以是任意格式，例如 "product:123" 或 "user:settings:theme"
        }

        private static void ValidateScopeValue(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{paramName} cannot be null or empty", paramName);

            // ⚠️ Scope 值（tenantId, storeId, userId）不能包含分隔符
            if (value.Contains(KeySeparators.Scope.ToString()) ||
                value.Contains(KeySeparators.Segment.ToString()))
            {
                throw new ArgumentException(
                    $"{paramName} cannot contain separators '{KeySeparators.Scope}' or '{KeySeparators.Segment}'",
                    paramName);
            }
        }
    }
}