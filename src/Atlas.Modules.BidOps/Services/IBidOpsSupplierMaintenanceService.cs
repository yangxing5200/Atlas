namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsSupplierMaintenanceService
{
    Task<BidOpsSupplierMaintenanceResult> RunEvidenceExpiryScanAsync(
        int maxItems,
        int warningDays,
        CancellationToken ct = default);
}

public sealed record BidOpsSupplierMaintenanceResult(
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
