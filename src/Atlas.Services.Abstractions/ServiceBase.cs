using Atlas.Core.Entities;
using Atlas.Core.Exceptions;
using Atlas.Data.Abstractions;
using Atlas.Models.Tenant.Responses;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Atlas.Services.Abstractions
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
    }
    public abstract class DataServiceBase<TEntity, TDto> : ServiceBase
    where TEntity : class, IBaseEntity<long>
    {
        protected readonly IRepository<TEntity> _repository;
        protected readonly IMapper _mapper;

        protected DataServiceBase(
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
            return _mapper.Map<TDto>(entity);
        }

        public virtual async Task UpdateAsync(long id, TDto dto, CancellationToken ct = default)
        {
            var entity = await _repository.Tracking(e => e.Id == id).FirstOrDefaultAsync();
            if (entity == null) throw new AtlasException();

            _mapper.Map(dto, entity);
        }

        /// <summary>
        /// 删除数据
        /// </summary>
        public virtual async Task DeleteAsync(long id, CancellationToken ct = default)
        {
            var entity = await _repository.GetByIdAsync(id, ct);
            if (entity == null)
                throw new AtlasException($"实体不存在: {id}");

            await _repository.RemoveAsync(entity);
        }


        /// <summary>
        /// 判断是否存在符合条件的数据
        /// </summary>
        public virtual Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        {
            return _repository.Query(predicate).AnyAsync(ct);
        }


        /// <summary>
        /// 获取数量
        /// </summary>
        public virtual Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken ct = default)
        {
            return  _repository.Query(predicate).CountAsync(ct);
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

            var query = _repository.Query(predicate);

            var total = await query.CountAsync(ct);

            var entities = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var items = _mapper.Map<List<TDto>>(entities);

            return new PagedResult<TDto>(total, items, pageIndex, pageSize);
        }
    }
}
