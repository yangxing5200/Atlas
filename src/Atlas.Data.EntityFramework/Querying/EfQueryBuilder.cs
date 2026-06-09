using System.Linq.Expressions;
using Atlas.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Atlas.Data.Common.Querying;

/// <summary>
/// EF Core-backed implementation of the Atlas query builder abstraction.
/// </summary>
public sealed class EfQueryBuilder<TEntity> : IQueryBuilder<TEntity>
    where TEntity : class
{
    private IQueryable<TEntity> _query;
    private object? _lastIncludable;

    public EfQueryBuilder(IQueryable<TEntity> query)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
    }

    public IQueryBuilder<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        _query = _query.Where(predicate);
        return this;
    }

    public IQueryBuilder<TEntity> Include<TProperty>(Expression<Func<TEntity, TProperty>> navigation)
    {
        var includable = _query.Include(navigation);
        _query = includable;
        _lastIncludable = includable;
        return this;
    }

    public IQueryBuilder<TEntity> ThenInclude<TPreviousProperty, TProperty>(
        Expression<Func<TPreviousProperty, TProperty>> navigation)
        where TPreviousProperty : class
    {
        if (_lastIncludable is IIncludableQueryable<TEntity, IEnumerable<TPreviousProperty>> collectionIncludable)
        {
            var newIncludable = collectionIncludable.ThenInclude(navigation);
            _query = newIncludable;
            _lastIncludable = newIncludable;
            return this;
        }

        if (_lastIncludable is IIncludableQueryable<TEntity, TPreviousProperty> singleIncludable)
        {
            var newIncludable = singleIncludable.ThenInclude(navigation);
            _query = newIncludable;
            _lastIncludable = newIncludable;
            return this;
        }

        throw new InvalidOperationException("ThenInclude must follow Include or ThenInclude.");
    }

    public IQueryBuilder<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector)
    {
        _query = _query.OrderBy(keySelector);
        return this;
    }

    public IQueryBuilder<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
    {
        _query = _query.OrderByDescending(keySelector);
        return this;
    }

    public IQueryBuilder<TEntity> Skip(int count)
    {
        _query = _query.Skip(count);
        return this;
    }

    public IQueryBuilder<TEntity> Take(int count)
    {
        _query = _query.Take(count);
        return this;
    }

    public IQueryBuilder<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> selector)
        where TResult : class
    {
        return new EfQueryBuilder<TResult>(_query.Select(selector));
    }

    public Task<List<TResult>> SelectToListAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        CancellationToken ct = default)
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
