using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Tenant.Sql;

public interface ITenantSqlExecutor
{
    Task<int> ExecuteTenantCommandAsync(
        DbContext dbContext,
        long tenantId,
        FormattableString command,
        CancellationToken ct = default);
}
