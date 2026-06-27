using Atlas.Modules.BidOps.Ai.Evidence;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsPricingInferenceService
{
    BidOpsPricingDecision InferFinalAmount(
        AwardEvidence award,
        CandidateEvidence? matchedCandidate,
        IReadOnlyList<CandidateEvidence> packageCandidates,
        TenderPackageEvidence? tender);
}
