// Tags/TagManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;

namespace Atlas.Infrastructure.Caching.Tags
{
    public class TagManager : ITagManager
    {
        private readonly ITagVersionStore _versionStore;

        public TagManager(ITagVersionStore versionStore)
        {
            _versionStore = versionStore ?? throw new ArgumentNullException(nameof(versionStore));
        }

        public async Task<long> GetTagVersionAsync(string tag, CancellationToken cancellationToken = default)
        {
            ValidateTag(tag);
            return await _versionStore.GetVersionAsync(tag, cancellationToken);
        }

        public async Task<IDictionary<string, long>> GetTagVersionsAsync(
            IEnumerable<string> tags,
            CancellationToken cancellationToken = default)
        {
            var tagList = tags.ToList();
            foreach (var tag in tagList)
            {
                ValidateTag(tag);
            }

            return await _versionStore.GetVersionsAsync(tagList, cancellationToken);
        }

        public async Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            ValidateTag(tag);
            await _versionStore.IncrementVersionAsync(tag, cancellationToken);
        }

        public async Task InvalidateTagsAsync(
            IEnumerable<string> tags,
            CancellationToken cancellationToken = default)
        {
            var tagList = tags.ToList();
            foreach (var tag in tagList)
            {
                ValidateTag(tag);
            }

            await _versionStore.IncrementVersionsAsync(tagList, cancellationToken);
        }

        public async Task<IEnumerable<string>> GetAllTagsAsync(CancellationToken cancellationToken = default)
        {
            return await _versionStore.GetAllTagsAsync(cancellationToken);
        }

        private static void ValidateTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentException("Tag cannot be null or empty", nameof(tag));
            }
        }
    }
}