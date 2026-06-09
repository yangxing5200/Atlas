using Atlas.Core.Context;
using Atlas.Data.Tenant.Providers;
using FluentAssertions;
using Xunit;

namespace Atlas.Data.Tests;

public sealed class TenantDbConnProviderTests
{
    [Fact]
    public async Task GetConnStringAsync_WithCurrentTenant_UsesExecutionContextTenant()
    {
        var context = new TestTenantExecutionContext { TenantId = 42 };
        var directory = new RecordingTenantConnectionDirectory(tenantId => CreateConnectionInfo(tenantId));
        var provider = new TenantDbConnProvider(context, directory);

        var connectionString = await provider.GetConnStringAsync();

        connectionString.Should().Be("Server=master-42;");
        directory.RequestedTenantIds.Should().Equal(42);
    }

    [Fact]
    public async Task GetConnStringAsync_WithoutCurrentTenant_Throws()
    {
        var context = new TestTenantExecutionContext();
        var directory = new RecordingTenantConnectionDirectory(tenantId => CreateConnectionInfo(tenantId));
        var provider = new TenantDbConnProvider(context, directory);

        var act = () => provider.GetConnStringAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("当前上下文中没有租户信息");
        directory.RequestedTenantIds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReadonlyConnStringAsync_WithExplicitTenant_UsesReadonlyServer()
    {
        var context = new TestTenantExecutionContext();
        var directory = new RecordingTenantConnectionDirectory(tenantId => CreateConnectionInfo(
            tenantId,
            readonlyConnection: "Server=readonly-100;"));
        var provider = new TenantDbConnProvider(context, directory);

        var connectionString = await provider.GetReadonlyConnStringAsync(100);

        connectionString.Should().Be("Server=readonly-100;");
        directory.RequestedTenantIds.Should().Equal(100);
    }

    [Fact]
    public async Task GetReportConnStringAsync_WithoutReportServer_FallsBackToReadonlyThenMaster()
    {
        var context = new TestTenantExecutionContext();
        var readonlyDirectory = new RecordingTenantConnectionDirectory(tenantId => CreateConnectionInfo(
            tenantId,
            readonlyConnection: "Server=readonly-101;"));
        var readonlyProvider = new TenantDbConnProvider(context, readonlyDirectory);

        var readonlyFallback = await readonlyProvider.GetReportConnStringAsync(101);

        readonlyFallback.Should().Be("Server=readonly-101;");

        var masterDirectory = new RecordingTenantConnectionDirectory(tenantId => CreateConnectionInfo(tenantId));
        var masterProvider = new TenantDbConnProvider(context, masterDirectory);

        var masterFallback = await masterProvider.GetReportConnStringAsync(102);

        masterFallback.Should().Be("Server=master-102;");
    }

    private static TenantConnectionInfo CreateConnectionInfo(
        long tenantId,
        string? readonlyConnection = null,
        string? reportConnection = null)
    {
        var info = new TenantConnectionInfo
        {
            TenantId = tenantId,
            TenantName = $"tenant-{tenantId}",
            MasterConnectionString = $"Server=master-{tenantId};"
        };

        if (readonlyConnection != null)
        {
            info.ReadonlyServers.Add(new ReadonlyServerInfo
            {
                ServerCode = "readonly",
                ConnectionString = readonlyConnection
            });
        }

        if (reportConnection != null)
        {
            info.ReportServers.Add(new ReadonlyServerInfo
            {
                ServerCode = "report",
                ConnectionString = reportConnection,
                IsReport = true
            });
        }

        return info;
    }

    private sealed class TestTenantExecutionContext : ITenantExecutionContext
    {
        public long? TenantId { get; init; }

        public long? StoreId { get; init; }

        public long? UserId { get; init; }

        public bool IsAuthenticated { get; init; }

        public string? SessionId { get; init; }
    }

    private sealed class RecordingTenantConnectionDirectory : ITenantConnectionDirectory
    {
        private readonly Func<long, TenantConnectionInfo> _factory;
        private readonly List<long> _requestedTenantIds = new();

        public RecordingTenantConnectionDirectory(Func<long, TenantConnectionInfo> factory)
        {
            _factory = factory;
        }

        public IReadOnlyList<long> RequestedTenantIds => _requestedTenantIds;

        public Task<TenantConnectionInfo> GetConnectionInfoAsync(
            long tenantId,
            CancellationToken cancellationToken = default)
        {
            _requestedTenantIds.Add(tenantId);
            return Task.FromResult(_factory(tenantId));
        }
    }
}
