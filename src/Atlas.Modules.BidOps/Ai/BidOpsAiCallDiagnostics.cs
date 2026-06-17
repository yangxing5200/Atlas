namespace Atlas.Modules.BidOps.Ai;

public interface IBidOpsAiCallDiagnostics
{
    IReadOnlyList<BidOpsAiCallDiagnosticEntry> Entries { get; }

    bool HasEntries { get; }

    void Record(BidOpsAiCallDiagnosticEntry entry);
}

public sealed class BidOpsAiCallDiagnostics : IBidOpsAiCallDiagnostics
{
    private readonly List<BidOpsAiCallDiagnosticEntry> _entries = [];

    public IReadOnlyList<BidOpsAiCallDiagnosticEntry> Entries => _entries;

    public bool HasEntries => _entries.Count > 0;

    public void Record(BidOpsAiCallDiagnosticEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
    }
}

public sealed record BidOpsAiCallDiagnosticEntry(
    string Use,
    string Provider,
    string Model,
    string Endpoint,
    int StatusCode,
    long ElapsedMilliseconds,
    int ResponseCharacters,
    int AssistantCharacters,
    string FinishReason,
    string RawResponseBody,
    string AssistantContent);
