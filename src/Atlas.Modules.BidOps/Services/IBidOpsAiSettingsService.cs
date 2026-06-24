using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsAiSettingsService
{
    Task<BidOpsAiProviderSettingsDto> GetSettingsAsync(CancellationToken ct = default);

    Task<BidOpsAiProviderSettingsDto> SetProviderAsync(
        UpdateBidOpsAiProviderRequest request,
        CancellationToken ct = default);

    Task<BidOpsAiProviderSettingsDto> SetCodexCliSettingsAsync(
        UpdateBidOpsCodexCliSettingsRequest request,
        CancellationToken ct = default);

    Task<BidOpsAiProviderSettingsDto> SetCodexCliScenarioSettingsAsync(
        UpdateBidOpsCodexCliScenarioSettingsRequest request,
        CancellationToken ct = default);

    Task<string> GetEffectiveProviderAsync(CancellationToken ct = default);

    Task<BidOpsCodexCliRuntimeSettingsDto> GetEffectiveCodexCliSettingsAsync(
        string scenario = BidOpsCodexCliScenarios.Default,
        CancellationToken ct = default);
}
