namespace Atlas.Data.Abstractions;

public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 开启事务
    /// </summary>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// 提交事务（将所有 SaveChanges 的更改持久化到数据库）
    /// </summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>
    /// 回滚事务（撤销所有未提交的更改）
    /// </summary>
    Task RollbackAsync(CancellationToken ct = default);

    /// <summary>
    /// 在数据库执行策略内执行一个事务单元。
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct = default);

    /// <summary>
    /// 在数据库执行策略内执行一个事务单元。
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken ct = default);

    /// <summary>
    /// 保存更改到 DbContext 的 ChangeTracker（但不提交事务）
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// 保存更改（显式传入 tenantId，用于登录等无身份上下文的场景）
    /// </summary>
    Task<int> SaveChangesAsync(long tenantId, CancellationToken ct = default);

    /// <summary>
    /// 是否在事务中
    /// </summary>
    bool HasActiveTransaction { get; }
}
