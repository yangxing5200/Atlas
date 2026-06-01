namespace Atlas.Infrastructure.Security.DataMasking;

public interface ISensitiveDataRevealExecutor
{
    Task<TResponse> ExecuteAsync<TResponse>(
        SensitiveDataRevealContext context,
        Func<CancellationToken, Task<TResponse>> revealAction,
        CancellationToken ct = default);
}
