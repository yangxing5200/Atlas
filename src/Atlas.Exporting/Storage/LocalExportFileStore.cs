using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Atlas.Exporting.Storage;

public sealed class LocalExportFileStore : IExportFileStore
{
    private const string StorageProviderName = "Local";
    private readonly string _rootPath;

    public LocalExportFileStore(IOptions<ExportJobOptions> options)
    {
        var rootPath = options?.Value.LocalStorage.RootPath;
        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(rootPath) ? "var/exports" : rootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public Task<Stream> CreateAsync(
        string temporaryKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path = ResolveStoragePath(temporaryKey);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        Stream stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);

        return Task.FromResult(stream);
    }

    public async Task<ExportStoredFile> CommitAsync(
        string temporaryKey,
        string finalKey,
        CancellationToken ct = default)
    {
        var temporaryPath = ResolveStoragePath(temporaryKey);
        var finalPath = ResolveStoragePath(finalKey);
        var directory = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        if (!File.Exists(temporaryPath))
            throw new FileNotFoundException("Temporary export file does not exist.", temporaryPath);

        File.Move(temporaryPath, finalPath, overwrite: true);

        var info = new FileInfo(finalPath);
        var sha256 = await ComputeSha256Async(finalPath, ct);
        return new ExportStoredFile(StorageProviderName, NormalizeKey(finalKey), info.Length, sha256);
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path = ResolveStoragePath(storageKey);
        if (!File.Exists(path))
            throw new FileNotFoundException("Export file does not exist.", path);

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path = ResolveStoragePath(storageKey);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    private string ResolveStoragePath(string storageKey)
    {
        var key = NormalizeKey(storageKey);
        var segments = key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var path = Path.GetFullPath(Path.Combine(new[] { _rootPath }.Concat(segments).ToArray()));

        if (!path.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Export storage key escapes the configured root path.");

        return path;
    }

    private static string NormalizeKey(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key is required.", nameof(storageKey));

        var key = storageKey.Replace('\\', '/').Trim('/');
        if (key.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Export storage key must not contain parent directory segments.");

        return key;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        var bytes = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
