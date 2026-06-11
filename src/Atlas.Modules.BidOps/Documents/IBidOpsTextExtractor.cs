namespace Atlas.Modules.BidOps.Documents;

public interface IBidOpsTextExtractor
{
    Task<string> ExtractAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
