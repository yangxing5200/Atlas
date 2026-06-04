namespace Atlas.Exporting;

public interface IExportProviderRegistry
{
    IExportTaskProvider GetProvider(string exportTaskType);
    IExportFormatWriter GetWriter(string format);
}

public sealed class ExportProviderRegistry : IExportProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IExportTaskProvider> _providers;
    private readonly IReadOnlyDictionary<string, IExportFormatWriter> _writers;

    public ExportProviderRegistry(IEnumerable<IExportTaskProvider> providers, IEnumerable<IExportFormatWriter> writers)
    {
        _providers = providers.GroupBy(x => Normalize(x.ExportTaskType), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);
        _writers = writers.GroupBy(x => Normalize(x.Format), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);
    }

    public IExportTaskProvider GetProvider(string exportTaskType)
    {
        var key = Normalize(exportTaskType);
        return _providers.TryGetValue(key, out var provider)
            ? provider
            : throw new InvalidOperationException($"No export provider registered for task type '{exportTaskType}'.");
    }

    public IExportFormatWriter GetWriter(string format)
    {
        var key = Normalize(format);
        return _writers.TryGetValue(key, out var writer)
            ? writer
            : throw new InvalidOperationException($"No export writer registered for format '{format}'.");
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Export key is required.", nameof(value))
            : value.Trim().ToLowerInvariant();
    }
}
