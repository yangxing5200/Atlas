using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Caching.Abstractions
{
    /// <summary>
    /// Tag版本存储接口
    /// </summary>
    public interface ITagVersionStore
    {
        Task<long> GetVersionAsync(string tag, CancellationToken cancellationToken = default);
        Task<IDictionary<string, long>> GetVersionsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
        Task<long> IncrementVersionAsync(string tag, CancellationToken cancellationToken = default);
        Task IncrementVersionsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
        Task<IEnumerable<string>> GetAllTagsAsync(CancellationToken cancellationToken = default);
    }
}