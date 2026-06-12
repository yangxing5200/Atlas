using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace Atlas.Modules.BidOps.Documents;

public sealed class LocalBidOpsFileStore : IBidOpsFileStore
{
    private readonly string _rootPath;
    private readonly IReadOnlyList<string> _fallbackRootPaths;

    public LocalBidOpsFileStore(IConfiguration configuration)
    {
        var configuredPath = configuration["BidOps:FileStore:LocalRootPath"];
        _rootPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "storage", "bidops")
            : ResolveRootPath(configuredPath.Trim());
        _fallbackRootPaths = configuration
            .GetSection("BidOps:FileStore:FallbackLocalRootPaths")
            .GetChildren()
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Select(ResolveRootPath)
            .ToArray();
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

        var fullPath = ResolveExistingPath(storageKey, _rootPath);
        if (fullPath == null)
        {
            foreach (var fallbackRootPath in _fallbackRootPaths)
            {
                fullPath = ResolveExistingPath(storageKey, fallbackRootPath);
                if (fullPath != null)
                    break;
            }
        }

        if (fullPath == null)
            throw new FileNotFoundException("BidOps stored file was not found.", storageKey);

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    private static string? ResolveExistingPath(string storageKey, string rootPath)
    {
        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, normalized));
        var root = Path.GetFullPath(rootPath);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Storage key resolves outside the BidOps file store.");

        return File.Exists(fullPath) ? fullPath : null;
    }

    private static string ResolveRootPath(string rootPath)
    {
        if (Path.IsPathRooted(rootPath))
            return rootPath;

        var workspaceRoot = FindAncestorWithFile(Directory.GetCurrentDirectory(), "Atlas.sln")
            ?? FindAncestorWithFile(AppContext.BaseDirectory, "Atlas.sln");
        if (workspaceRoot != null)
            return Path.GetFullPath(Path.Combine(workspaceRoot, rootPath));

        foreach (var baseDirectory in EnumerateResolutionBaseDirectories())
        {
            var candidate = Path.GetFullPath(Path.Combine(baseDirectory, rootPath));
            if (Directory.Exists(candidate))
                return candidate;
        }

        return Path.GetFullPath(rootPath);
    }

    private static IEnumerable<string> EnumerateResolutionBaseDirectories()
    {
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(start);
            while (current != null)
            {
                yield return current.FullName;
                current = current.Parent;
            }
        }
    }

    private static string? FindAncestorWithFile(string startDirectory, string fileName)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, fileName)))
                return current.FullName;

            current = current.Parent;
        }

        return null;
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
