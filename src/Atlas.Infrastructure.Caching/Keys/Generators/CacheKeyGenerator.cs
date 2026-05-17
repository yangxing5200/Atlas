// Keys/Generators/CacheKeyGenerator.cs
using System;
using System.Collections.Generic;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Keys.Conventions;

namespace Atlas.Infrastructure.Caching.Keys.Generators
{
    /// <summary>
    /// 按缓存作用域生成稳定的物理缓存键。
    /// </summary>
    /// <remarks>
    /// 生成结果必须与失效器中的通配符规则保持一致，例如 T:{tenant}:*、S:{tenant}:{store}:*。
    /// Scope 值禁止包含分隔符，防止不同作用域之间发生键空间串扰。
    /// </remarks>
    public class CacheKeyGenerator : ICacheKeyGenerator
    {
        /// <summary>
        /// 根据作用域路由到对应的键格式。
        /// </summary>
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

            // baseKey 属于业务键，允许包含冒号，例如 "product:123" 或 "user:settings:theme"。
        }

        private static void ValidateScopeValue(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{paramName} cannot be null or empty", paramName);

            // Scope 值参与键空间分段，不能包含分隔符，否则会破坏失效匹配边界。
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
