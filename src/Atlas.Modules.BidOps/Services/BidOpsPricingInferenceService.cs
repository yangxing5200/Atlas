using System.Globalization;
using Atlas.Modules.BidOps.Ai.Evidence;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsPricingInferenceService : IBidOpsPricingInferenceService
{
    public BidOpsPricingDecision InferFinalAmount(
        AwardEvidence award,
        CandidateEvidence? matchedCandidate,
        IReadOnlyList<CandidateEvidence> packageCandidates,
        TenderPackageEvidence? tender)
    {
        return Infer(award, matchedCandidate, packageCandidates, tender);
    }

    public static BidOpsPricingDecision Infer(
        AwardEvidence award,
        CandidateEvidence? matchedCandidate,
        IReadOnlyList<CandidateEvidence> packageCandidates,
        TenderPackageEvidence? tender)
    {
        ArgumentNullException.ThrowIfNull(award);
        packageCandidates ??= [];

        if (award.AwardAmount.HasValue)
        {
            return Decision(
                amount: award.AwardAmount.Value,
                amountKind: BidOpsAmountKinds.DirectAwardAmount,
                amountSourceStage: BidOpsAmountSourceStages.AwardNotice,
                amountEvidence: award.Evidence,
                baseAmount: null,
                baseAmountType: BidOpsBaseAmountTypes.Unknown,
                rate: null,
                formula: null,
                confidence: 0.98,
                requiresManualReview: false,
                reasons: ["Award notice disclosed a direct award amount."],
                missing: []);
        }

        if (matchedCandidate?.FinalQuoteAmount.HasValue == true &&
            (matchedCandidate.Rank == 1 ||
             packageCandidates.Count(candidate => SupplierMatches(award.AwardedSupplierName, candidate.SupplierName)) == 1))
        {
            return Decision(
                amount: matchedCandidate.FinalQuoteAmount.Value,
                amountKind: BidOpsAmountKinds.CandidateFinalQuote,
                amountSourceStage: BidOpsAmountSourceStages.CandidateNotice,
                amountEvidence: matchedCandidate.Evidence,
                baseAmount: null,
                baseAmountType: BidOpsBaseAmountTypes.Unknown,
                rate: null,
                formula: null,
                confidence: matchedCandidate.Rank == 1 ? 0.95 : 0.88,
                requiresManualReview: matchedCandidate.Rank != 1,
                reasons: matchedCandidate.Rank == 1
                    ? ["Awarded supplier matched candidate rank 1 final quote."]
                    : ["Awarded supplier had a unique candidate final quote."],
                missing: []);
        }

        BidOpsPricingDecision? rateDecision = null;
        if (award.RateEvidence != null)
        {
            rateDecision = InferFromRate(award.RateEvidence, tender);
            if (rateDecision.AmountValue.HasValue)
                return rateDecision;

            if (award.RateEvidence.RateType == BidOpsRateTypes.Unknown)
                return rateDecision;
        }

        var procurementAmount = ResolveDefaultProcurementAmount(tender);
        if (procurementAmount.HasValue)
        {
            return Decision(
                amount: procurementAmount.Value.Amount,
                amountKind: BidOpsAmountKinds.DefaultedFromProcurementPackageAmount,
                amountSourceStage: BidOpsAmountSourceStages.TenderNotice,
                amountEvidence: tender?.Evidence,
                baseAmount: procurementAmount.Value.Amount,
                baseAmountType: procurementAmount.Value.BaseAmountType,
                rate: null,
                formula: null,
                confidence: procurementAmount.Value.BaseAmountType == BidOpsBaseAmountTypes.PackageGuidePrice ? 0.76 : 0.72,
                requiresManualReview: true,
                reasons:
                [
                    "Award amount missing; defaulted final award amount to procurement package amount.",
                    $"Tender evidence provided {procurementAmount.Value.BaseAmountType}.",
                    "Defaulted procurement amounts require manual review before formal supplier analytics."
                ],
                missing: ["ManualReviewRequired"]);
        }

        if (rateDecision != null)
            return rateDecision;

        return Unknown(
            BidOpsAmountSourceStages.Unknown,
            ["Award amount missing.", "Candidate final quote missing.", "No supported award rate evidence found."],
            "AmountCannotBeInferred");
    }

    private static BidOpsPricingDecision InferFromRate(
        BidOpsRateEvidence rate,
        TenderPackageEvidence? tender)
    {
        if (rate.RateType == BidOpsRateTypes.Unknown)
        {
            return Unknown(
                BidOpsAmountSourceStages.AwardNotice,
                ["Rate semantics are ambiguous; the source has a percentage but not discount/reduction/coefficient wording."],
                "RateSemanticsAmbiguous",
                rate);
        }

        var baseAmount = ResolveBaseAmount(tender);
        if (baseAmount == null)
        {
            return Unknown(
                BidOpsAmountSourceStages.AwardNotice,
                ["A rate was found, but no unambiguous package-level base amount was found."],
                "BaseAmountMissing",
                rate);
        }

        decimal amount;
        string kind;
        string formula;
        if (rate.RateType == BidOpsRateTypes.ReductionRate)
        {
            amount = baseAmount.Value.Amount * (1m - rate.RateValue);
            kind = BidOpsAmountKinds.InferredFromReductionRate;
            formula = $"{Format(baseAmount.Value.Amount)} * (1 - {Format(rate.RateValue)})";
        }
        else
        {
            amount = baseAmount.Value.Amount * rate.RateValue;
            kind = rate.RateType == BidOpsRateTypes.Coefficient
                ? BidOpsAmountKinds.InferredFromCoefficient
                : BidOpsAmountKinds.InferredFromDiscountRate;
            formula = $"{Format(baseAmount.Value.Amount)} * {Format(rate.RateValue)}";
        }

        return Decision(
            amount: Math.Round(amount, 2),
            amountKind: kind,
            amountSourceStage: BidOpsAmountSourceStages.AwardNotice,
            amountEvidence: rate.Evidence,
            baseAmount: baseAmount.Value.Amount,
            baseAmountType: baseAmount.Value.BaseAmountType,
            rate: rate,
            formula: formula,
            confidence: baseAmount.Value.BaseAmountType == BidOpsBaseAmountTypes.PackageGuidePrice ? 0.84 : 0.78,
            requiresManualReview: true,
            reasons:
            [
                $"Award notice disclosed {rate.RateType}.",
                $"Tender evidence provided {baseAmount.Value.BaseAmountType}.",
                "Inferred amounts require manual review before formal supplier analytics."
            ],
            missing: ["ManualReviewRequired"]);
    }

    private static BidOpsPricingDecision Unknown(
        string sourceStage,
        IReadOnlyList<string> reasons,
        string missingReason,
        BidOpsRateEvidence? rate = null)
    {
        return Decision(
            amount: null,
            amountKind: BidOpsAmountKinds.Unknown,
            amountSourceStage: sourceStage,
            amountEvidence: rate?.Evidence,
            baseAmount: null,
            baseAmountType: BidOpsBaseAmountTypes.Unknown,
            rate: rate,
            formula: null,
            confidence: 0,
            requiresManualReview: true,
            reasons: reasons,
            missing: [missingReason]);
    }

    private static BidOpsPricingDecision Decision(
        decimal? amount,
        string amountKind,
        string amountSourceStage,
        EvidenceSourceRef? amountEvidence,
        decimal? baseAmount,
        string baseAmountType,
        BidOpsRateEvidence? rate,
        string? formula,
        double confidence,
        bool requiresManualReview,
        IReadOnlyList<string> reasons,
        IReadOnlyList<string> missing)
    {
        return new BidOpsPricingDecision(
            AmountValue: amount,
            AmountKind: amountKind,
            AmountSourceStage: amountSourceStage,
            AmountSourceNoticeId: amountEvidence?.RawNoticeId,
            AmountSourceAttachmentId: amountEvidence?.RawAttachmentId,
            AmountSourceTableOrSheet: amountEvidence?.TableIndex.HasValue == true
                ? $"table:{amountEvidence.TableIndex.Value}"
                : null,
            AmountSourceRow: amountEvidence?.RowIndex,
            BaseAmount: baseAmount,
            BaseAmountType: baseAmountType,
            RateValue: rate?.RateValue,
            RateType: rate?.RateType,
            Formula: formula,
            Confidence: confidence,
            RequiresManualReview: requiresManualReview,
            EvidenceText: amountEvidence?.EvidenceText ?? rate?.SourceText,
            Reasons: reasons,
            MissingReasons: missing);
    }

    private static BaseAmountCandidate? ResolveBaseAmount(TenderPackageEvidence? tender)
    {
        if (tender == null)
            return null;

        if (string.IsNullOrWhiteSpace(tender.NormalizedPackageNo))
            return null;

        if (tender.GuidePrice.HasValue)
            return new BaseAmountCandidate(tender.GuidePrice.Value, BidOpsBaseAmountTypes.PackageGuidePrice);

        var candidates = new List<BaseAmountCandidate>();
        if (tender.MaxPrice.HasValue)
            candidates.Add(new BaseAmountCandidate(tender.MaxPrice.Value, BidOpsBaseAmountTypes.PackageMaxPrice));
        if (tender.BudgetAmount.HasValue)
            candidates.Add(new BaseAmountCandidate(tender.BudgetAmount.Value, BidOpsBaseAmountTypes.PackageBudget));

        if (candidates.Count == 1)
            return candidates[0];

        if (candidates.Count > 1 &&
            candidates.Select(x => x.Amount).Distinct().Count() == 1)
        {
            return candidates[0];
        }

        return null;
    }

    private static BaseAmountCandidate? ResolveDefaultProcurementAmount(TenderPackageEvidence? tender)
    {
        if (tender == null)
            return null;

        if (string.IsNullOrWhiteSpace(tender.NormalizedPackageNo))
            return null;

        if (tender.GuidePrice.HasValue)
            return new BaseAmountCandidate(tender.GuidePrice.Value, BidOpsBaseAmountTypes.PackageGuidePrice);

        var candidates = new List<BaseAmountCandidate>();
        if (tender.BudgetAmount.HasValue)
            candidates.Add(new BaseAmountCandidate(tender.BudgetAmount.Value, BidOpsBaseAmountTypes.PackageBudget));
        if (tender.MaxPrice.HasValue)
            candidates.Add(new BaseAmountCandidate(tender.MaxPrice.Value, BidOpsBaseAmountTypes.PackageMaxPrice));

        if (candidates.Count == 1)
            return candidates[0];

        if (candidates.Count > 1 &&
            candidates.Select(x => x.Amount).Distinct().Count() == 1)
        {
            return candidates[0];
        }

        return null;
    }

    private static bool SupplierMatches(string? left, string? right)
    {
        var normalizedLeft = BidOpsSupplierNameNormalizer.NormalizeForMatch(left);
        var normalizedRight = BidOpsSupplierNameNormalizer.NormalizeForMatch(right);
        return !string.IsNullOrWhiteSpace(normalizedLeft) &&
               !string.IsNullOrWhiteSpace(normalizedRight) &&
               string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string Format(decimal value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private readonly record struct BaseAmountCandidate(decimal Amount, string BaseAmountType);
}
