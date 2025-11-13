using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Caching.Abstractions
{
    /// <summary>
    /// 底层缓存存储提供者接口
    /// </summary>
    public interface ICacheProvider
    {
        Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default);
        Task SetAsync(string key, byte[] value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
        Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        Task<IDictionary<string, byte[]?>> GetManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
        Task SetManyAsync(IDictionary<string, byte[]> items, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
        Task<int> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

        Task<IEnumerable<string>> GetKeysByPatternAsync(string pattern, CancellationToken cancellationToken = default);
        Task ClearAsync(CancellationToken cancellationToken = default);
    }
}