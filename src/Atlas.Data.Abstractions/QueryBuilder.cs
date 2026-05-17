using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Abstractions
{
    #region Query Builder (异步安全, 支持 Include / Select / Where / ToList / FirstOrDefault)

    /// <summary>
    /// 对外暴露受控的查询组合能力，避免仓储接口直接泄露 IQueryable。
    /// </summary>
    /// <remarks>
    /// QueryBuilder 只负责追加表达式并延迟执行；租户、门店、软删除等基础过滤应在创建它之前完成。
    /// </remarks>
    public class QueryBuilder<TEntity> where TEntity : class
    {
        private IQueryable<TEntity> _query;

        public QueryBuilder(IQueryable<TEntity> query)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
        }

        public QueryBuilder<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
        {
            _query = _query.Where(predicate);
            return this;
        }
        /// <summary>
        /// 保存最近一次 Include 的结果，以便 ThenInclude 使用
        /// </summary>
        public QueryBuilder<TEntity> Include<TProperty>(Expression<Func<TEntity, TProperty>> navigation)
        {
            var includable = _query.Include(navigation);
            _query = includable;
            _lastIncludable = includable;
            return this;
        }

        private object? _lastIncludable;

        /// <summary>
        /// 继续包含上一段导航属性。
        /// </summary>
        /// <remarks>
        /// EF Core 的 ThenInclude 返回类型会随集合导航和引用导航变化，这里通过保存最近一次 Include 结果来兼容两类路径。
        /// </remarks>
        public QueryBuilder<TEntity> ThenInclude<TPreviousProperty, TProperty>(
            Expression<Func<TPreviousProperty, TProperty>> navigation)
            where TPreviousProperty : class
        {
            // 先尝试匹配集合类型 (ICollection<T>, IEnumerable<T>, List<T> 等)
            if (_lastIncludable is IIncludableQueryable<TEntity, IEnumerable<TPreviousProperty>> collectionIncludable)
            {
                var newIncludable = collectionIncludable.ThenInclude(navigation);
                _query = newIncludable;
                _lastIncludable = newIncludable;
                return this;
            }

            // 再尝试匹配单个引用类型
            if (_lastIncludable is IIncludableQueryable<TEntity, TPreviousProperty> singleIncludable)
            {
                var newIncludable = singleIncludable.ThenInclude(navigation);
                _query = newIncludable;
                _lastIncludable = newIncludable;
                return this;
            }

            throw new InvalidOperationException("ThenInclude must follow Include or ThenInclude");
        }

        public QueryBuilder<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            _query = _query.OrderBy(keySelector);
            return this;
        }

        public QueryBuilder<TEntity> Skip(int count)
        {
            _query = _query.Skip(count);
            return this;
        }

        public QueryBuilder<TEntity> Take(int count)
        {
            _query = _query.Take(count);
            return this;
        }

        public QueryBuilder<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            _query = _query.OrderByDescending(keySelector);
            return this;
        }

        /// <summary>
        /// 投影为新的查询构建器，继续保持延迟执行。
        /// </summary>
        public QueryBuilder<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> selector) where TResult : class
        {
            return new QueryBuilder<TResult>(_query.Select(selector));
        }
        public Task<List<TResult>> SelectToListAsync<TResult>(Expression<Func<TEntity, TResult>> selector, CancellationToken ct = default)
        {
            return _query.Select(selector).ToListAsync(ct);
        }

        public Task<TEntity?> FirstOrDefaultAsync(CancellationToken ct = default)
        {
            return _query.FirstOrDefaultAsync(ct);
        }

        public Task<List<TEntity>> ToListAsync(CancellationToken ct = default)
        {
            return _query.ToListAsync(ct);
        }

        public Task<long> CountAsync(CancellationToken ct = default)
        {
            return _query.LongCountAsync(ct);
        }
        public Task<bool> AnyAsync(CancellationToken ct = default)
        {
            return _query.AnyAsync(ct);
        }
    }

    #endregion
}
