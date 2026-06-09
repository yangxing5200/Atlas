using System.Linq.Expressions;

namespace Atlas.Data.Abstractions;

/// <summary>
/// Exposes the query composition surface that application code is allowed to use.
/// </summary>
/// <remarks>
/// Implementations own the underlying query provider. Tenant, store, and soft-delete filters
/// must be applied before an instance is returned from a repository.
/// </remarks>
public interface IQueryBuilder<TEntity>
    where TEntity : class
{
    IQueryBuilder<TEntity> Where(Expression<Func<TEntity, bool>> predicate);

    IQueryBuilder<TEntity> Include<TProperty>(Expression<Func<TEntity, TProperty>> navigation);

    IQueryBuilder<TEntity> ThenInclude<TPreviousProperty, TProperty>(
        Expression<Func<TPreviousProperty, TProperty>> navigation)
        where TPreviousProperty : class;

    IQueryBuilder<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector);

    IQueryBuilder<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector);

    IQueryBuilder<TEntity> Skip(int count);

    IQueryBuilder<TEntity> Take(int count);

    IQueryBuilder<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> selector)
        where TResult : class;

    Task<List<TResult>> SelectToListAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        CancellationToken ct = default);

    Task<TEntity?> FirstOrDefaultAsync(CancellationToken ct = default);

    Task<List<TEntity>> ToListAsync(CancellationToken ct = default);

    Task<long> CountAsync(CancellationToken ct = default);

    Task<bool> AnyAsync(CancellationToken ct = default);
}
