// Invalidation/CacheInvalidator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.Invalidation
{
    /// <summary>
    /// 统一缓存失效器，封装按键、标签、作用域和模式的失效策略。
    /// </summary>
    /// <remarks>
    /// 按键/模式失效会删除物理键并发布跨实例通知；按标签失效通过 TagManager 提升版本实现逻辑失效。
    /// </remarks>
    public class CacheInvalidator : ICacheInvalidator
    {
        private readonly ICacheProvider _provider;
        private readonly ITagManager _tagManager;
        private readonly ICacheInvalidationBus? _invalidationBus;

        public CacheInvalidator(
            ICacheProvider provider,
            ITagManager tagManager,
            ICacheInvalidationBus? invalidationBus = null)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _tagManager = tagManager ?? throw new ArgumentNullException(nameof(tagManager));
            _invalidationBus = invalidationBus;
        }

        public async Task InvalidateByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            await _provider.RemoveAsync(key, cancellationToken);
            await PublishInvalidationsAsync(new[] { key });
        }

        public async Task InvalidateByKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            var keyList = keys.Distinct().ToList();
            if (!keyList.Any())
                return;

            await _provider.RemoveManyAsync(keyList, cancellationToken);
            await PublishInvalidationsAsync(keyList);
        }

        public async Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            await _tagManager.InvalidateTagAsync(tag, cancellationToken);
        }

        public async Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        {
            await _tagManager.InvalidateTagsAsync(tags, cancellationToken);
        }

        public async Task InvalidateByScopeAsync(CacheScope scope, string? scopeId = null, CancellationToken cancellationToken = default)
        {
            await InvalidateByPatternAsync(BuildScopePattern(scope, scopeId), cancellationToken);
        }

        public async Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            // 模式失效依赖底层 Provider 支持按前缀/通配符枚举键；大规模场景需谨慎使用。
            var keys = (await _provider.GetKeysByPatternAsync(pattern, cancellationToken))
                .Distinct()
                .ToList();

            if (keys.Any())
            {
                await _provider.RemoveManyAsync(keys, cancellationToken);
                await PublishInvalidationsAsync(keys);
            }
        }

        private async Task PublishInvalidationsAsync(IEnumerable<string> keys)
        {
            if (_invalidationBus == null)
                return;

            foreach (var key in keys)
            {
                await _invalidationBus.PublishInvalidationAsync(key);
            }
        }

        /// <summary>
        /// 将缓存作用域转换为物理键前缀匹配模式。
        /// </summary>
        private static string BuildScopePattern(CacheScope scope, string? scopeId)
        {
            return scope switch
            {
                CacheScope.Global => "G:*",
                CacheScope.Tenant => $"T:{RequireSingleScopeId(scopeId, "tenant")}:*",
                CacheScope.Store => BuildCompositeScopePattern("S", scopeId, "store"),
                CacheScope.User => BuildCompositeScopePattern("U", scopeId, "user"),
                _ => throw new ArgumentException($"Unknown scope: {scope}", nameof(scope))
            };
        }

        private static string BuildCompositeScopePattern(string prefix, string? scopeId, string scopeName)
        {
            var parts = RequireCompositeScopeId(scopeId, scopeName);
            return $"{prefix}:{parts[0]}:{parts[1]}:*";
        }

        private static string RequireSingleScopeId(string? scopeId, string scopeName)
        {
            if (string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException($"{scopeName} scope invalidation requires a scope id.", nameof(scopeId));

            if (scopeId.Contains(':'))
                throw new ArgumentException($"{scopeName} scope id cannot contain ':'.", nameof(scopeId));

            return scopeId;
        }

        private static string[] RequireCompositeScopeId(string? scopeId, string scopeName)
        {
            if (string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException($"{scopeName} scope invalidation requires a scope id.", nameof(scopeId));

            var parts = scopeId.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                throw new ArgumentException($"{scopeName} scope id must use the 'tenantId:itemId' format.", nameof(scopeId));

            return parts;
        }
    }
}
