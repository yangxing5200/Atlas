using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Caching.Abstractions
{
    /// <summary>
    /// Tag管理器接口
    /// </summary>
    public interface ITagManager
    {
        Task<long> GetTagVersionAsync(string tag, CancellationToken cancellationToken = default);
        Task<IDictionary<string, long>> GetTagVersionsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
        Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default);
        Task InvalidateTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
        Task<IEnumerable<string>> GetAllTagsAsync(CancellationToken cancellationToken = default);
    }
}