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
            var pattern = scope switch
            {
                CacheScope.Global => "G:*",
                CacheScope.Tenant => $"T:{scopeId}:*",
                CacheScope.Store => $"S:{scopeId}:*",
                CacheScope.User => $"U:{scopeId}:*",
                _ => throw new ArgumentException($"Unknown scope: {scope}")
            };

            await InvalidateByPatternAsync(pattern, cancellationToken);
        }

        public async Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
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
    }
}
