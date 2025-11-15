using Atlas.Data.Abstractions;
using Atlas.Models.Tenant.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Repositories
{
    /// <summary>
    /// 门店服务接口
    /// </summary>
    public interface IStoreRepository : IRepository<Store>
    {
        Task<Store?> GetByIdAsync(long id, CancellationToken ct = default);

        Task<List<Store>> GetChildDirectStoresAsync(
            long parentStoreId,
            CancellationToken ct = default);

        Task<List<Store>> GetSiblingDirectStoresAsync(
            long parentStoreId,
            CancellationToken ct = default);

        Task<List<long>> GetChildStoreIdsAsync(
            long parentStoreId,
            CancellationToken ct = default);
    }
}
