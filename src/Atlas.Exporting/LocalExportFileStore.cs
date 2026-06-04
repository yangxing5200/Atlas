using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Atlas.Exporting;

public sealed class LocalExportFileStore : IExportFileStore
{
    private readonly string _rootPath;

    public LocalExportFileStore(IOptions<ExportingOptions> options)
    {
        var root = options?.Value.LocalStorage.RootPath;
        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(root) ? "var/exports" : root);
    }

    public Task<Stream> CreateAsync(string temporaryKey, CancellationToken ct = default)
    {
        var path = ToPath(temporaryKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Stream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public async Task<ExportStoredFile> CommitAsync(string temporaryKey, string finalKey, CancellationToken ct = default)
    {
        var tempPath = ToPath(temporaryKey);
        var finalPath = ToPath(finalKey);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        if (File.Exists(finalPath))
            File.Delete(finalPath);
        File.Move(tempPath, finalPath);

        await using var read = new FileStream(finalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hashBytes = await SHA256.HashDataAsync(read, ct);
        return new ExportStoredFile("Local", NormalizeKey(finalKey), read.Length, Convert.ToHexString(hashBytes).ToLowerInvariant());
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        Stream stream = new FileStream(ToPath(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ToPath(storageKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string ToPath(string key)
    {
        var normalized = NormalizeKey(key);
        var path = Path.GetFullPath(Path.Combine(_rootPath, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(_rootPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Export storage key escapes the configured root path.");
        return path;
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("Storage key is required.", nameof(key))
            : key.Trim().Replace('\\', '/').TrimStart('/');
    }
}
