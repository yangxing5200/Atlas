using System.Text.Json;

namespace Atlas.Services.Tests;

public sealed class WorkerLoggingConfigurationTests
{
    [Fact]
    public void WorkerProgram_UsesAtlasSerilogLogging()
    {
        var program = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Atlas.Worker", "Program.cs"));

        Assert.Contains("AddAtlasLogging", program);
        Assert.Contains("ClearProviders", program);
        Assert.Contains("AddSerilog", program);
        Assert.Contains("CloseAndFlush", program);
    }

    [Fact]
    public void WorkerAppsettings_ConfiguresDedicatedAtlasLogFiles()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "src", "Atlas.Worker", "appsettings.json")));

        var atlas = document.RootElement
            .GetProperty("Logging")
            .GetProperty("Atlas");

        Assert.True(atlas.GetProperty("EnableConsole").GetBoolean());
        Assert.True(atlas.GetProperty("EnableFile").GetBoolean());
        Assert.Equal("logs/application/atlas-worker-.log", atlas.GetProperty("FilePath").GetString());
        Assert.Equal("logs/application/atlas-worker-errors-.log", atlas.GetProperty("ErrorFilePath").GetString());
        Assert.Equal("logs/audit/worker-audit-.log", atlas.GetProperty("AuditFilePath").GetString());
    }

    [Fact]
    public void AtlasConsoleLogTemplate_IncludesFullTimestamp()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Atlas.Infrastructure.Logging",
            "Extensions",
            "ServiceCollectionExtensions.cs"));

        Assert.Contains("{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}", source);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Atlas.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
