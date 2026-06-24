namespace Atlas.Modules.BidOps.Services;

public static class BidOpsReviewQualityIssueTypes
{
    public const string MissingProjectCode = nameof(MissingProjectCode);
    public const string MissingLotOrPackage = nameof(MissingLotOrPackage);
    public const string AmbiguousAmountUnit = nameof(AmbiguousAmountUnit);
    public const string RateOrDiscountInAmountColumn = nameof(RateOrDiscountInAmountColumn);
    public const string AiRuleMismatch = nameof(AiRuleMismatch);
    public const string MissingQualificationRequirement = nameof(MissingQualificationRequirement);
    public const string MissingPerformanceRequirement = nameof(MissingPerformanceRequirement);
    public const string MissingPersonnelRequirement = nameof(MissingPersonnelRequirement);
    public const string LifecycleMatchMissing = nameof(LifecycleMatchMissing);
    public const string LifecycleMatchConflict = nameof(LifecycleMatchConflict);
    public const string DuplicatePackageIdentity = nameof(DuplicatePackageIdentity);
    public const string OriginalEvidenceMissing = nameof(OriginalEvidenceMissing);
    public const string MissingSupplierName = nameof(MissingSupplierName);
    public const string MissingCandidateRank = nameof(MissingCandidateRank);
}
