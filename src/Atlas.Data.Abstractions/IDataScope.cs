using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Abstractions
{
    public interface IDataScope
    {
        /// <summary>
        /// 获取与当前门店共享数据的门店ID列表
        /// </summary>
        List<long> GetShareStoreIds();
        Task PreloadShareStoreIdsAsync(CancellationToken ct);
        long? TenantId { get; }
        long? StoreId { get; }

    }
}
