using Atlas.Core.Entities.Global;
using Atlas.Data.Common;
using Atlas.Data.Global;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Data.Tests;

public sealed class TenantSchemaMigrationStateModelTests
{
    [Fact]
    public void GlobalModel_ConfiguresTenantSchemaMigrationStateUniqueTenantId()
    {
        var options = new DbContextOptionsBuilder<AtlasGlobalDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var dbContext = new AtlasGlobalDbContext(options, SystemIdentity.Migration);
        var entityType = dbContext.Model.FindEntityType(typeof(TenantSchemaMigrationState));

        Assert.NotNull(entityType);
        Assert.Contains(
            entityType!.GetIndexes(),
            index => index.IsUnique &&
                     index.Properties.Count == 1 &&
                     index.Properties[0].Name == nameof(TenantSchemaMigrationState.TenantId));
    }
}
