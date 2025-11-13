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

        public CacheInvalidator(ICacheProvider provider, ITagManager tagManager)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _tagManager = tagManager ?? throw new ArgumentNullException(nameof(tagManager));
        }

        public async Task InvalidateByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            await _provider.RemoveAsync(key, cancellationToken);
        }

        public async Task InvalidateByKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            await _provider.RemoveManyAsync(keys, cancellationToken);
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
            var keys = await _provider.GetKeysByPatternAsync(pattern, cancellationToken);
            if (keys.Any())
            {
                await _provider.RemoveManyAsync(keys, cancellationToken);
            }
        }
    }
}