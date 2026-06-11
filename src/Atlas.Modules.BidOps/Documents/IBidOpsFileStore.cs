namespace Atlas.Modules.BidOps.Documents;

public sealed record StoredFileInfo(
    string StorageProvider,
    string StorageKey,
    string FileName,
    string ContentType,
    long FileSize,
    string FileHash);

public interface IBidOpsFileStore
{
    Task<StoredFileInfo> SaveAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
