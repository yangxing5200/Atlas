using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Abstractions
{
    #region Query Builder (异步安全, 支持 Include / Select / Where / ToList / FirstOrDefault)

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

        public QueryBuilder<TEntity> Include<TProperty>(Expression<Func<TEntity, TProperty>> navigation)
        {
            _query = _query.Include(navigation);
            return this;
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
    }

    #endregion
}
