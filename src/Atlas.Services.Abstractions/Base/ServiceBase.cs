using Atlas.Core.Entities.Interfaces;
using Atlas.Core.Exceptions;
using Atlas.Data.Abstractions;
using Atlas.Models.Tenant.Responses;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Atlas.Services.Abstractions.Base
{
    public abstract class ServiceBase
    {
        protected readonly IUnitOfWork UnitOfWork;

        protected ServiceBase(IUnitOfWork unitOfWork)
        {
            UnitOfWork = unitOfWork;
        }

        /// <summary>
        /// 在事务中执行操作
        /// </summary>
        protected async Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> action,
            CancellationToken ct = default)
        {
            await UnitOfWork.BeginTransactionAsync(ct);
            try
            {
                var result = await action();
                await UnitOfWork.CommitAsync(ct);
                return result;
            }
            catch
            {
                await UnitOfWork.RollbackAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// 在事务中执行操作（无返回值）
        /// </summary>
        protected async Task ExecuteInTransactionAsync(
            Func<Task> action,
            CancellationToken ct = default)
        {
            await UnitOfWork.BeginTransactionAsync(ct);
            try
            {
                await action();
                await UnitOfWork.CommitAsync(ct);
            }
            catch
            {
                await UnitOfWork.RollbackAsync(ct);
                throw;
            }
        }
        protected async Task CommitAsync(CancellationToken ct = default)
        {
            await UnitOfWork.SaveChangesAsync(ct);
        }
    }
    public abstract class ServiceBase<TEntity, TDto> : ServiceBase, IServiceBase<TEntity, TDto>
    where TEntity : class, IBaseEntity<long>
    {
        protected readonly IRepository<TEntity> _repository;
        protected readonly IMapper _mapper;

        protected ServiceBase(
            IRepository<TEntity> repository,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public virtual async Task<TDto?> GetByIdAsync(long id, CancellationToken ct = default)
        {
            var entity = await _repository.GetByIdAsync(id, ct);
            return entity == null ? default : _mapper.Map<TDto>(entity);
        }


        public virtual async Task<TDto> AddAsync(TDto dto, CancellationToken ct = default)
        {
            var entity = _mapper.Map<TEntity>(dto);
            await _repository.AddAsync(entity, ct);
            await UnitOfWork.SaveChangesAsync(ct);
            return _mapper.Map<TDto>(entity);
        }

        public virtual async Task UpdateAsync(long id, TDto dto, CancellationToken ct = default)
        {
            var builder = await _repository.QueryTrackingAsync(ct);
            var entity = await builder.Where(e => e.Id == id).FirstOrDefaultAsync(ct);
            if (entity == null) throw new AtlasException();
            _mapper.Map(dto, entity);
            await UnitOfWork.SaveChangesAsync(ct);
        }

        public virtual async Task RemoveAsync(long id, CancellationToken ct = default)
        {
            var entity = await _repository.GetByIdAsync(id, ct);
            if (entity == null) throw new AtlasException($"实体不存在: {id}");
            await _repository.RemoveAsync(entity);
            await UnitOfWork.SaveChangesAsync(ct);
        }


        /// <summary>
        /// 判断是否存在符合条件的数据
        /// </summary>
        public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        {
            var builder = await _repository.QueryAsync(ct: ct);
            return await builder.Where(predicate).CountAsync(ct) > 0;
        }


        /// <summary>
        /// 获取数量
        /// </summary>
        public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        {
            var builder = await _repository.QueryAsync(ct: ct);
            var count = await builder.Where(predicate).CountAsync(ct);
            return (int)count;
        }



        /// <summary>
        /// 通用分页查询
        /// </summary>
        public virtual async Task<PagedResult<TDto>> PageQueryAsync(
            Expression<Func<TEntity, bool>> predicate,
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            if (pageIndex < 1) pageIndex = 1;
            if (pageSize < 1) pageSize = 10;

            var builder = await _repository.QueryAsync(ct: ct);
            builder = builder.Where(predicate);

            var total = await builder.CountAsync(ct);

            var entities = await builder
                .OrderBy(e => e.Id) // 你可以根据需求改成其他排序
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var items = _mapper.Map<List<TDto>>(entities);

            return new PagedResult<TDto>(total, items, pageIndex, pageSize);
        }
    }
}
