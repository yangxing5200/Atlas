using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Tenant.Sql;

public sealed class TenantSqlExecutor : ITenantSqlExecutor
{
    public Task<int> ExecuteTenantCommandAsync(
        DbContext dbContext,
        long tenantId,
        FormattableString command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(command);

        if (tenantId <= 0)
            throw new ArgumentOutOfRangeException(nameof(tenantId), tenantId, "Tenant id must be greater than zero.");

        EnsureTenantPredicate(command.Format);
        return dbContext.Database.ExecuteSqlInterpolatedAsync(command, ct);
    }

    private static void EnsureTenantPredicate(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            throw new ArgumentException("SQL command cannot be empty.");

        if (!commandText.Contains("TenantId", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Tenant SQL commands must contain an explicit TenantId predicate. " +
                "Use a dedicated infrastructure method for approved cross-tenant operations.");
        }
    }
}
