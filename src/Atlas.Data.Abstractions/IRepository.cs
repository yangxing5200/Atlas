using Atlas.Core.Entities.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Authorization;

namespace Atlas.Data.Abstractions
{
    /// <summary>
    /// 最基础的仓储接口，不向应用层直接暴露 IQueryable。
    /// </summary>
    /// <remarks>
    /// 查询入口返回 QueryBuilder，仓储实现负责在创建查询时先应用租户、门店等数据边界。
    /// 显式 tenantId 重载仅用于登录、系统任务等尚无完整身份上下文的流程。
    /// </remarks>
    public interface IRepository<TEntity, TKey>
        where TEntity : class
    {
        Task AddAsync(TEntity entity, CancellationToken ct = default);
        Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);

        Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default);
        Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
        Task<List<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default);

        Task RemoveAsync(TEntity entity, CancellationToken ct = default);
        Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
        /// <summary>
        /// 获取不可追踪查询构建器（用于只读查询）- 依赖 ICurrentIdentity
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<QueryBuilder<TEntity>> QueryAsync(CancellationToken ct = default);

        /// <summary>
        /// 获取可追踪查询构建器，用于读取后修改并提交的场景。
        /// </summary>
        Task<QueryBuilder<TEntity>> QueryTrackingAsync(CancellationToken ct = default);

        /// <summary>
        /// 获取已应用声明式数据权限的不可追踪查询构建器。
        /// </summary>
        Task<QueryBuilder<TEntity>> QueryDataScopeAsync(
            string resourceCode,
            AtlasDataScopeType scopeType,
            CancellationToken ct = default);

        /// <summary>
        /// 获取已应用声明式数据权限的可追踪查询构建器。
        /// </summary>
        Task<QueryBuilder<TEntity>> QueryDataScopeTrackingAsync(
            string resourceCode,
            AtlasDataScopeType scopeType,
            CancellationToken ct = default);

        /// <summary>
        /// 获取不可追踪查询构建器（用于只读查询）- 显式传入 tenantId，用于登录等场景
        /// </summary>
        Task<QueryBuilder<TEntity>> QueryAsync(long tenantId, CancellationToken ct = default);

        /// <summary>
        /// 获取可追踪查询构建器（用于后续更新）- 显式传入 tenantId，用于登录等场景
        /// </summary>
        Task<QueryBuilder<TEntity>> QueryTrackingAsync(long tenantId, CancellationToken ct = default);

        /// <summary>
        /// 添加实体（显式传入 tenantId，用于登录等场景）
        /// </summary>
        Task AddAsync(TEntity entity, long tenantId, CancellationToken ct = default);

        /// <summary>
        /// 批量添加实体（显式传入 tenantId，用于登录等场景）
        /// </summary>
        Task AddRangeAsync(IEnumerable<TEntity> entities, long tenantId, CancellationToken ct = default);

        /// <summary>
        /// 删除实体（显式传入 tenantId，用于登录等场景）
        /// </summary>
        Task RemoveAsync(TEntity entity, long tenantId, CancellationToken ct = default);

        /// <summary>
        /// 批量删除实体（显式传入 tenantId，用于登录等场景）
        /// </summary>
        Task RemoveRangeAsync(IEnumerable<TEntity> entities, long tenantId, CancellationToken ct = default);

    }

    public interface IRepository<TEntity> : IRepository<TEntity, long>
        where TEntity : class
    {
    }



}
