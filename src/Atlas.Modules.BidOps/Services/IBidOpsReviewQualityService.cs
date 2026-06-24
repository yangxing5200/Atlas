using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsReviewQualityService
{
    Task ApplyNoticeQualityAsync(
        ReviewTask task,
        NoticeStaging notice,
        IReadOnlyCollection<PackageStaging> packages,
        IReadOnlyCollection<RequirementStaging> requirements,
        CancellationToken ct = default);

    Task ApplyOutcomeQualityAsync(
        ReviewTask task,
        RawNotice raw,
        NoticeStaging? notice,
        IReadOnlyCollection<OutcomeSupplierRecord> outcomeRecords,
        CancellationToken ct = default);

    Task<ReviewQualityBackfillResultDto> BackfillReviewQualityAsync(
        ReviewQualityBackfillRequest request,
        CancellationToken ct = default);
}
