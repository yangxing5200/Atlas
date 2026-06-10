namespace Atlas.Exporting;

public sealed class ExportJobOptions
{
    public const string SectionName = "Exporting";

    public bool Enabled { get; set; } = true;
    public string DefaultFormat { get; set; } = "csv";
    public string[] AllowedFormats { get; set; } = ["csv"];
    public int DefaultPageSize { get; set; } = 500;
    public int MaxPageSize { get; set; } = 2000;
    public int? DefaultMaxRows { get; set; } = 100000;
    public int RetentionDays { get; set; } = 7;
    public string StorageProvider { get; set; } = "Local";
    public LocalExportStorageOptions LocalStorage { get; set; } = new();
    public ExportArtifactCleanupOptions Cleanup { get; set; } = new();
    public ExportJobReconciliationOptions Reconciliation { get; set; } = new();
}

public sealed class LocalExportStorageOptions
{
    public string RootPath { get; set; } = "var/exports";
}

public sealed class ExportArtifactCleanupOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
    public int BatchSize { get; set; } = 100;
}

public sealed class ExportJobReconciliationOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 5;
    public int StalePendingMinutes { get; set; } = 1;
    public int StaleRunningMinutes { get; set; } = 60;
    public int BatchSize { get; set; } = 100;
    public bool RunOnStartup { get; set; }
}
