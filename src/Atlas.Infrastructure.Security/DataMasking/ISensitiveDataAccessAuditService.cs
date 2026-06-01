namespace Atlas.Infrastructure.Security.DataMasking;

public interface ISensitiveDataAccessAuditService
{
    Task LogRevealSucceededAsync(
        SensitiveDataRevealContext context,
        CancellationToken ct = default);

    Task LogRevealFailedAsync(
        SensitiveDataRevealContext context,
        string failureReason,
        CancellationToken ct = default);
}
