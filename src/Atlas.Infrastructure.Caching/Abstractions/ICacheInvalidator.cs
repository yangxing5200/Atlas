using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.Abstractions
{
    /// <summary>
    /// 缓存失效器接口
    /// </summary>
    public interface ICacheInvalidator
    {
        Task InvalidateByKeyAsync(string key, CancellationToken cancellationToken = default);
        Task InvalidateByKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
        Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default);
        Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
        Task InvalidateByScopeAsync(CacheScope scope, string? scopeId = null, CancellationToken cancellationToken = default);
        Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default);
    }
}