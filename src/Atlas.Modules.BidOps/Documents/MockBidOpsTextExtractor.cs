using System.Text;

namespace Atlas.Modules.BidOps.Documents;

public sealed class MockBidOpsTextExtractor : IBidOpsTextExtractor
{
    public async Task<string> ExtractAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(text)
            ? $"Mock extracted text for {fileName} ({contentType})."
            : text;
    }
}
