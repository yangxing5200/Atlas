using Atlas.Models.Tenant.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Services.Abstractions.Base
{
    public interface IServiceBase<TEntity,TDto>
    {
        Task<TDto?> GetByIdAsync(long id, CancellationToken ct = default);
        Task<TDto> AddAsync(TDto dto, CancellationToken ct = default);
        Task UpdateAsync(long id, TDto dto, CancellationToken ct = default);
        Task RemoveAsync(long id, CancellationToken ct = default);
        Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
        Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
        Task<PagedResult<TDto>> PageQueryAsync(Expression<Func<TEntity, bool>> predicate, int pageIndex, int pageSize, CancellationToken ct = default);
    }
}
