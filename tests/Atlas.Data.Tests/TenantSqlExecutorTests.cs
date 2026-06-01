using Atlas.Data.Tenant.Sql;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Atlas.Data.Tests;

public sealed class TenantSqlExecutorTests
{
    [Fact]
    public async Task ClaimOutboxMessageAsync_WithMissingTenantId_Throws()
    {
        await using var fixture = await SqliteTenantSqlFixture.CreateAsync();
        var executor = new TenantSqlExecutor();
        var now = DateTime.UtcNow;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            executor.ClaimOutboxMessageAsync(
                fixture.DbContext,
                tenantId: 0,
                messageId: 1,
                workerId: "worker-1",
                now,
                staleProcessingBefore: now.AddMinutes(-5),
                maxAttempts: 3));
    }

    [Fact]
    public async Task ClaimOutboxMessageAsync_WithDifferentTenantId_DoesNotClaimOtherTenantMessage()
    {
        await using var fixture = await SqliteTenantSqlFixture.CreateAsync();
        await InsertOutboxAsync(fixture.DbContext, id: 100, tenantId: 2, processedAtUtc: null);
        fixture.CommandCapture.Clear();

        var executor = new TenantSqlExecutor();
        var now = DateTime.UtcNow;
        var updated = await executor.ClaimOutboxMessageAsync(
            fixture.DbContext,
            tenantId: 1,
            messageId: 100,
            workerId: "worker-1",
            now,
            staleProcessingBefore: now.AddMinutes(-5),
            maxAttempts: 3);

        var processingBy = await QueryScalarAsync<string>(
            fixture.DbContext,
            "SELECT ProcessingBy FROM TenantOutboxMessages WHERE Id = 100");

        Assert.Equal(0, updated);
        Assert.Null(processingBy);
        Assert.Contains(
            fixture.CommandCapture.Commands,
            command => command.Contains("UPDATE TenantOutboxMessages", StringComparison.OrdinalIgnoreCase) &&
                       command.Contains("TenantId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ClaimOutboxMessageAsync_WithMatchingTenant_ClaimsMessage()
    {
        await using var fixture = await SqliteTenantSqlFixture.CreateAsync();
        await InsertOutboxAsync(fixture.DbContext, id: 101, tenantId: 1, processedAtUtc: null);
        fixture.CommandCapture.Clear();

        var executor = new TenantSqlExecutor();
        var now = DateTime.UtcNow;
        var updated = await executor.ClaimOutboxMessageAsync(
            fixture.DbContext,
            tenantId: 1,
            messageId: 101,
            workerId: "worker-1",
            now,
            staleProcessingBefore: now.AddMinutes(-5),
            maxAttempts: 3);

        var processingBy = await QueryScalarAsync<string>(
            fixture.DbContext,
            "SELECT ProcessingBy FROM TenantOutboxMessages WHERE Id = 101");

        Assert.Equal(1, updated);
        Assert.Equal("worker-1", processingBy);
        Assert.Contains(
            fixture.CommandCapture.Commands,
            command => command.Contains("UPDATE TenantOutboxMessages", StringComparison.OrdinalIgnoreCase) &&
                       command.Contains("TenantId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeleteProcessedOutboxMessagesAsync_DeletesOnlyRequestedTenantRows()
    {
        await using var fixture = await SqliteTenantSqlFixture.CreateAsync();
        var cutoff = DateTime.UtcNow.AddDays(-1);
        await InsertOutboxAsync(fixture.DbContext, id: 201, tenantId: 1, processedAtUtc: cutoff.AddHours(-1));
        await InsertOutboxAsync(fixture.DbContext, id: 202, tenantId: 2, processedAtUtc: cutoff.AddHours(-1));
        await InsertOutboxAsync(fixture.DbContext, id: 203, tenantId: 1, processedAtUtc: null);
        fixture.CommandCapture.Clear();

        var deleted = await new TenantSqlExecutor().DeleteProcessedOutboxMessagesAsync(
            fixture.DbContext,
            tenantId: 1,
            cutoffUtc: cutoff,
            batchSize: 10);

        Assert.Equal(1, deleted);
        Assert.Equal(0, await CountAsync(
            fixture.DbContext,
            "SELECT COUNT(*) FROM TenantOutboxMessages WHERE TenantId = 1 AND ProcessedAtUtc IS NOT NULL"));
        Assert.Equal(1, await CountAsync(
            fixture.DbContext,
            "SELECT COUNT(*) FROM TenantOutboxMessages WHERE TenantId = 2 AND ProcessedAtUtc IS NOT NULL"));
        Assert.Equal(1, await CountAsync(
            fixture.DbContext,
            "SELECT COUNT(*) FROM TenantOutboxMessages WHERE TenantId = 1 AND ProcessedAtUtc IS NULL"));
        Assert.Contains(
            fixture.CommandCapture.Commands,
            command => command.Contains("DELETE FROM TenantOutboxMessages", StringComparison.OrdinalIgnoreCase) &&
                       command.Contains("TenantId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeleteReceivedInboxMessagesAsync_DeletesOnlyRequestedTenantRows()
    {
        await using var fixture = await SqliteTenantSqlFixture.CreateAsync();
        var cutoff = DateTime.UtcNow.AddDays(-1);
        await InsertInboxAsync(fixture.DbContext, id: 301, tenantId: 1, receivedAtUtc: cutoff.AddHours(-1));
        await InsertInboxAsync(fixture.DbContext, id: 302, tenantId: 2, receivedAtUtc: cutoff.AddHours(-1));
        await InsertInboxAsync(fixture.DbContext, id: 303, tenantId: 1, receivedAtUtc: cutoff.AddHours(1));
        fixture.CommandCapture.Clear();

        var deleted = await new TenantSqlExecutor().DeleteReceivedInboxMessagesAsync(
            fixture.DbContext,
            tenantId: 1,
            cutoffUtc: cutoff,
            batchSize: 10);

        Assert.Equal(1, deleted);
        Assert.Equal(0, await CountAsync(
            fixture.DbContext,
            "SELECT COUNT(*) FROM TenantInboxMessages WHERE Id = 301"));
        Assert.Equal(1, await CountAsync(
            fixture.DbContext,
            "SELECT COUNT(*) FROM TenantInboxMessages WHERE Id = 302"));
        Assert.Equal(1, await CountAsync(
            fixture.DbContext,
            "SELECT COUNT(*) FROM TenantInboxMessages WHERE Id = 303"));
        Assert.Contains(
            fixture.CommandCapture.Commands,
            command => command.Contains("DELETE FROM TenantInboxMessages", StringComparison.OrdinalIgnoreCase) &&
                       command.Contains("TenantId", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task InsertOutboxAsync(
        DbContext dbContext,
        long id,
        long tenantId,
        DateTime? processedAtUtc)
    {
        var createdAt = DateTime.UtcNow.AddMinutes(-10);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO TenantOutboxMessages
                 (Id, TenantId, CreatedAt, UpdatedAt, ProcessingAtUtc, ProcessingBy, ProcessedAtUtc,
                  AttemptCount, AvailableAtUtc, NextAttemptAtUtc)
             VALUES
                 ({id}, {tenantId}, {createdAt}, NULL, NULL, NULL, {processedAtUtc},
                  0, NULL, NULL)
             """);
    }

    private static async Task InsertInboxAsync(
        DbContext dbContext,
        long id,
        long tenantId,
        DateTime receivedAtUtc)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO TenantInboxMessages (Id, TenantId, ReceivedAtUtc)
             VALUES ({id}, {tenantId}, {receivedAtUtc})
             """);
    }

    private static async Task<int> CountAsync(DbContext dbContext, string sql)
    {
        var value = await QueryScalarAsync<long>(dbContext, sql);
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task<T?> QueryScalarAsync<T>(DbContext dbContext, string sql)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var value = await command.ExecuteScalarAsync();
        if (value is null or DBNull)
            return default;

        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    private sealed class SqliteTenantSqlFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private SqliteTenantSqlFixture(
            SqliteConnection connection,
            DbContext dbContext,
            CaptureCommandInterceptor commandCapture)
        {
            _connection = connection;
            DbContext = dbContext;
            CommandCapture = commandCapture;
        }

        public DbContext DbContext { get; }

        public CaptureCommandInterceptor CommandCapture { get; }

        public static async Task<SqliteTenantSqlFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var commandCapture = new CaptureCommandInterceptor();
            var options = new DbContextOptionsBuilder<DbContext>()
                .UseSqlite(connection)
                .AddInterceptors(commandCapture)
                .Options;

            var dbContext = new DbContext(options);
            var fixture = new SqliteTenantSqlFixture(connection, dbContext, commandCapture);
            await fixture.CreateSchemaAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private async Task CreateSchemaAsync()
        {
            await DbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE TenantOutboxMessages (
                    Id INTEGER PRIMARY KEY,
                    TenantId INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NULL,
                    ProcessingAtUtc TEXT NULL,
                    ProcessingBy TEXT NULL,
                    ProcessedAtUtc TEXT NULL,
                    AttemptCount INTEGER NOT NULL,
                    AvailableAtUtc TEXT NULL,
                    NextAttemptAtUtc TEXT NULL
                );
                """);

            await DbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE TenantInboxMessages (
                    Id INTEGER PRIMARY KEY,
                    TenantId INTEGER NOT NULL,
                    ReceivedAtUtc TEXT NOT NULL
                );
                """);
        }
    }

    private sealed class CaptureCommandInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = new();

        public void Clear() => Commands.Clear();

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            Commands.Add(command.CommandText);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
