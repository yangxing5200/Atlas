namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsOpportunityMaintenanceService
{
    Task<BidOpsOpportunityMaintenanceResult> RunValueAssessmentAsync(int maxItems, CancellationToken ct = default);

    Task<BidOpsOpportunityMaintenanceResult> RunDeadlineReminderScanAsync(
        int maxItems,
        int warningDays,
        CancellationToken ct = default);

    Task<BidOpsOpportunityMaintenanceResult> RunWatchReminderScanAsync(
        int maxItems,
        int warningDays,
        CancellationToken ct = default);

    Task<BidOpsOpportunityMaintenanceResult> RunStaleStateScanAsync(
        int maxItems,
        int staleDays,
        CancellationToken ct = default);
}

public sealed record BidOpsOpportunityMaintenanceResult(
    int Scanned,
    int Matched,
    int Updated,
    string Summary)
{
    public string ToJobResult()
    {
        return $"scanned={Scanned};matched={Matched};updated={Updated};summary={Summary}";
    }
}
