using System.Text.Encodings.Web;
using System.Text.Json;
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
    int? MaxOutputTokens);

internal sealed record BidOpsAiHttpSettingsDiagnostics(
    bool Enabled,
    bool UseEnabled,
    string Provider,
    bool SupportedProvider,
    string ApiKeySource,
    bool HasApiKey,
    bool HasModel,
    bool HasEndpoint);

internal static class BidOpsAiJsonLogging
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    public static string FormatJsonForLog(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, Options);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    public static string ExtractAssistantContentOrRaw(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? string.Empty;
                }
            }
        }
        catch (JsonException)
        {
            return responseText;
        }

        return responseText;
    }

    public static string ExtractFinishReason(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("finish_reason", out var finishReason))
            {
                return finishReason.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    public static string ExtractJsonObjectOrRaw(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = StripJsonFence(value.Trim());
        var firstObject = trimmed.IndexOf('{');
        var lastObject = trimmed.LastIndexOf('}');
        if (firstObject >= 0 && lastObject > firstObject)
            return trimmed[firstObject..(lastObject + 1)].Trim();

        var firstArray = trimmed.IndexOf('[');
        var lastArray = trimmed.LastIndexOf(']');
        if (firstArray >= 0 && lastArray > firstArray)
            return trimmed[firstArray..(lastArray + 1)].Trim();

        return trimmed;
    }

    private static string StripJsonFence(string value)
    {
        if (!value.StartsWith("```", StringComparison.Ordinal))
            return value;

        var firstLineEnd = value.IndexOf('\n');
        if (firstLineEnd < 0)
            return value;

        var body = value[(firstLineEnd + 1)..];
        var lastFence = body.LastIndexOf("```", StringComparison.Ordinal);
        return lastFence >= 0 ? body[..lastFence].Trim() : body.Trim();
    }
}

internal static class BidOpsAiHttpSettingsFactory
{
    public static bool TryCreate(
        IConfiguration configuration,
        BidOpsAiUse use,
        string? providerOverride,
        out BidOpsAiHttpSettings settings)
    {
        settings = default!;
        var provider = FirstNonEmpty(providerOverride, configuration["BidOps:Ai:Provider"], "CodexCli");
        if (!configuration.GetValue<bool>("BidOps:Ai:Enabled") || !IsEnabledForUse(configuration, use, provider))
            return false;

        if (!IsSupportedProvider(provider))
            return false;

        var apiKey = ResolveApiKey(configuration);
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        var model = FirstNonEmpty(configuration["BidOps:Ai:Model"], DefaultModel(provider));
        if (string.IsNullOrWhiteSpace(model))
            return false;

        var endpoint = ResolveEndpoint(configuration, provider);
        if (string.IsNullOrWhiteSpace(endpoint))
            return false;

        var configuredMaxOutputTokens = configuration.GetValue<int?>("BidOps:Ai:MaxOutputTokens");

        settings = new BidOpsAiHttpSettings(
            provider.Trim(),
            endpoint,
            apiKey.Trim(),
            model.Trim(),
            Math.Clamp(configuration.GetValue<int?>("BidOps:Ai:MaxInputCharacters") ?? 24_000, 4_000, 80_000),
            configuredMaxOutputTokens.HasValue && configuredMaxOutputTokens.Value > 0
                ? configuredMaxOutputTokens.Value
                : null);
        return true;
    }

    public static BidOpsAiHttpSettingsDiagnostics Diagnose(
        IConfiguration configuration,
        BidOpsAiUse use,
        string? providerOverride = null)
    {
        var provider = FirstNonEmpty(providerOverride, configuration["BidOps:Ai:Provider"], "CodexCli");
        var apiKey = ResolveApiKey(configuration);
        var model = FirstNonEmpty(configuration["BidOps:Ai:Model"], DefaultModel(provider));
        var endpoint = ResolveEndpoint(configuration, provider);

        return new BidOpsAiHttpSettingsDiagnostics(
            configuration.GetValue<bool>("BidOps:Ai:Enabled"),
            IsEnabledForUse(configuration, use, provider),
            provider.Trim(),
            IsSupportedProvider(provider),
            ResolveApiKeySource(configuration),
            !string.IsNullOrWhiteSpace(apiKey),
            !string.IsNullOrWhiteSpace(model),
            !string.IsNullOrWhiteSpace(endpoint));
    }

    public static bool TryCreate(
        IConfiguration configuration,
        BidOpsAiUse use,
        out BidOpsAiHttpSettings settings)
    {
        return TryCreate(configuration, use, providerOverride: null, out settings);
    }

    private static bool IsEnabledForUse(IConfiguration configuration, BidOpsAiUse use, string provider)
    {
        return use switch
        {
            BidOpsAiUse.NoticeStaging => configuration.GetValue<bool?>("BidOps:Ai:UseForNoticeStaging") ?? true,
            BidOpsAiUse.OutcomeSuppliers => configuration.GetValue<bool?>("BidOps:Ai:UseForOutcomeSuppliers") ?? IsDeepSeekLike(provider),
            _ => false
        };
    }

    private static bool IsDeepSeekLike(string provider)
    {
        return string.Equals(provider, "DeepSeek", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase);
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
            ? "deepseek-v4-pro"
            : string.Empty;
    }

    private static string ResolveApiKey(IConfiguration configuration)
    {
        return FirstNonEmpty(
            configuration["BidOps:Ai:ApiKey"],
            configuration["BidOps:DeepSeek:ApiKey"],
            Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"));
    }

    private static string ResolveApiKeySource(IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration["BidOps:Ai:ApiKey"]))
            return "BidOps:Ai:ApiKey";

        if (!string.IsNullOrWhiteSpace(configuration["BidOps:DeepSeek:ApiKey"]))
            return "BidOps:DeepSeek:ApiKey";

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")))
            return "DEEPSEEK_API_KEY";

        return "None";
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
