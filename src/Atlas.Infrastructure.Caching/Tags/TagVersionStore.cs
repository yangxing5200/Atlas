// Tags/TagVersionStore.cs
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;

namespace Atlas.Infrastructure.Caching.Tags
{
    public class TagVersionStore : ITagVersionStore
    {
        private readonly ConcurrentDictionary<string, long> _versions = new();

        public Task<long> GetVersionAsync(string tag, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_versions.GetOrAdd(tag, 1));
        }

        public Task<IDictionary<string, long>> GetVersionsAsync(
            IEnumerable<string> tags,
            CancellationToken cancellationToken = default)
        {
            var result = tags.ToDictionary(
                tag => tag,
                tag => _versions.GetOrAdd(tag, 1));

            return Task.FromResult<IDictionary<string, long>>(result);
        }

        public Task<long> IncrementVersionAsync(string tag, CancellationToken cancellationToken = default)
        {
            var newVersion = _versions.AddOrUpdate(tag, 1, (_, current) => current + 1);
            return Task.FromResult(newVersion);
        }

        public Task IncrementVersionsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        {
            foreach (var tag in tags)
            {
                _versions.AddOrUpdate(tag, 1, (_, current) => current + 1);
            }
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetAllTagsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<string>>(_versions.Keys.ToList());
        }
    }
}