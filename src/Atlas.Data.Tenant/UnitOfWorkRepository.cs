using System;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Core.Entities;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Repositories;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// UnitOfWork 专用的 Repository 实现
    /// 使用 UnitOfWork 提供的共享 DbContext
    /// </summary>
    internal class UnitOfWorkRepository<TEntity> : RepositoryOperationsBase<TEntity, long>, IRepository<TEntity>
        where TEntity : class, IBaseEntity<long>
    {
        private readonly UnitOfWork _unitOfWork;

        public UnitOfWorkRepository(
            UnitOfWork unitOfWork,
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator)
            : base(currentIdentity, idGenerator)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        protected override async Task<AtlasTenantDbContext> GetContextAsync()
        {
            return await _unitOfWork.GetDbContextAsync();
        }

        // ========== 同步查询方法 ==========

        public override IQueryable<TEntity> AsReadonlyQueryable()
        {
            throw new NotSupportedException(
                "UnitOfWorkRepository 不支持同步获取 Queryable，请使用 AsQueryable()");
        }

        public override IQueryable<TEntity> AsReadonlyQueryableUnfiltered()
        {
            throw new NotSupportedException(
                "UnitOfWorkRepository 不支持同步获取 Queryable");
        }

        // ========== 保存 ==========

        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            return _unitOfWork.SaveChangesAsync(ct);
        }

        public override void Dispose()
        {
            // UnitOfWork 负责释放 DbContext
        }
    }
}