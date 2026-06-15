using Microsoft.Extensions.Configuration;

namespace Atlas.Modules.BidOps.Ai;

internal enum BidOpsAiUse
{
    NoticeStaging,
    OutcomeSuppliers
}

internal sealed record BidOpsAiHttpSettings(
    string Provider,
    string Endpoint,
    string ApiKey,
    string Model,
    int MaxInputCharacters,
    int MaxOutputTokens);

internal static class BidOpsAiHttpSettingsFactory
{
    public static bool TryCreate(
        IConfiguration configuration,
        BidOpsAiUse use,
        out BidOpsAiHttpSettings settings)
    {
        settings = default!;
        if (!configuration.GetValue<bool>("BidOps:Ai:Enabled") || !IsEnabledForUse(configuration, use))
            return false;

        var provider = FirstNonEmpty(configuration["BidOps:Ai:Provider"], "OpenAICompatible");
        if (!IsSupportedProvider(provider))
            return false;

        var apiKey = FirstNonEmpty(
            configuration["BidOps:Ai:ApiKey"],
            configuration["BidOps:DeepSeek:ApiKey"],
            Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"));
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        var model = FirstNonEmpty(configuration["BidOps:Ai:Model"], DefaultModel(provider));
        if (string.IsNullOrWhiteSpace(model))
            return false;

        var endpoint = ResolveEndpoint(configuration, provider);
        if (string.IsNullOrWhiteSpace(endpoint))
            return false;

        settings = new BidOpsAiHttpSettings(
            provider.Trim(),
            endpoint,
            apiKey.Trim(),
            model.Trim(),
            Math.Clamp(configuration.GetValue<int?>("BidOps:Ai:MaxInputCharacters") ?? 24_000, 4_000, 80_000),
            Math.Clamp(configuration.GetValue<int?>("BidOps:Ai:MaxOutputTokens") ?? 4096, 512, 16_000));
        return true;
    }

    private static bool IsEnabledForUse(IConfiguration configuration, BidOpsAiUse use)
    {
        return use switch
        {
            BidOpsAiUse.NoticeStaging => configuration.GetValue<bool?>("BidOps:Ai:UseForNoticeStaging") ?? true,
            BidOpsAiUse.OutcomeSuppliers => configuration.GetValue<bool?>("BidOps:Ai:UseForOutcomeSuppliers") ?? IsDeepSeek(configuration),
            _ => false
        };
    }

    private static bool IsDeepSeek(IConfiguration configuration)
    {
        return string.Equals(
            configuration["BidOps:Ai:Provider"],
            "DeepSeek",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedProvider(string provider)
    {
        return string.Equals(provider, "DeepSeek", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveEndpoint(IConfiguration configuration, string provider)
    {
        var endpoint = FirstNonEmpty(
            configuration["BidOps:Ai:Endpoint"],
            configuration["BidOps:DeepSeek:Endpoint"]);
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            return string.Equals(provider, "DeepSeek", StringComparison.OrdinalIgnoreCase)
                ? EnsureChatCompletionsEndpoint(endpoint)
                : endpoint.Trim();
        }

        if (!string.Equals(provider, "DeepSeek", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var baseUrl = FirstNonEmpty(
            configuration["BidOps:Ai:BaseUrl"],
            configuration["BidOps:DeepSeek:BaseUrl"],
            "https://api.deepseek.com");
        return EnsureChatCompletionsEndpoint(baseUrl);
    }

    private static string EnsureChatCompletionsEndpoint(string value)
    {
        var trimmed = value.Trim().TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/chat/completions";
    }

    private static string DefaultModel(string provider)
    {
        return string.Equals(provider, "DeepSeek", StringComparison.OrdinalIgnoreCase)
            ? "deepseek-v4-flash"
            : string.Empty;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }
}
