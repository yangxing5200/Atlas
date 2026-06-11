using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace Atlas.Modules.BidOps.Documents;

public sealed class LocalBidOpsFileStore : IBidOpsFileStore
{
    private readonly string _rootPath;

    public LocalBidOpsFileStore(IConfiguration configuration)
    {
        var configuredPath = configuration["BidOps:FileStore:LocalRootPath"];
        _rootPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "storage", "bidops")
            : configuredPath.Trim();
    }

    public async Task<StoredFileInfo> SaveAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var safeFileName = SanitizeFileName(fileName);
        var relativeDirectory = Path.Combine(
            DateTime.UtcNow.ToString("yyyy"),
            DateTime.UtcNow.ToString("MM"),
            DateTime.UtcNow.ToString("dd"));
        var directory = Path.Combine(_rootPath, relativeDirectory);
        Directory.CreateDirectory(directory);

        var storageFileName = $"{Guid.NewGuid():N}-{safeFileName}";
        var path = Path.Combine(directory, storageFileName);
        await using var target = File.Create(path);
        using var sha = SHA256.Create();
        await using var crypto = new CryptoStream(target, sha, CryptoStreamMode.Write);
        await stream.CopyToAsync(crypto, cancellationToken);
        await crypto.FlushAsync(cancellationToken);
        crypto.FlushFinalBlock();

        var fileInfo = new FileInfo(path);
        var storageKey = Path.Combine(relativeDirectory, storageFileName).Replace('\\', '/');
        return new StoredFileInfo(
            BidOpsSystemValues.LocalStorageProvider,
            storageKey,
            safeFileName,
            contentType,
            fileInfo.Length,
            Convert.ToHexString(sha.Hash ?? Array.Empty<byte>()).ToLowerInvariant());
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        var root = Path.GetFullPath(_rootPath);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Storage key resolves outside the BidOps file store.");

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = string.IsNullOrWhiteSpace(fileName) ? "bidops-file" : Path.GetFileName(fileName.Trim());
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '-');
        }

        return string.IsNullOrWhiteSpace(name) ? "bidops-file" : name;
    }
}
