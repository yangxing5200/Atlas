using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace Atlas.Data.Abstractions
{
    /// <summary>
    /// 门店服务接口
    /// </summary>
    public interface IStoreService
    {
        /// <summary>
        /// 获取门店共享范围ID列表
        /// </summary>
        List<long> GetShareStoreIds(long storeId);

        /// <summary>
        /// 获取门店信息
        /// </summary>
        Task<Store> GetStoreAsync(long storeId);

        /// <summary>
        /// 刷新缓存
        /// </summary>
        void RefreshCache(long storeId);
    }
}
