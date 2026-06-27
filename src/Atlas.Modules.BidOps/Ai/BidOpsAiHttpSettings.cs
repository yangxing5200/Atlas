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
        return TryCreate(configuration, use, providerOverride, apiKeyOverride: null, out settings);
    }

    public static bool TryCreate(
        IConfiguration configuration,
        BidOpsAiUse use,
        string? providerOverride,
        string? apiKeyOverride,
        out BidOpsAiHttpSettings settings)
    {
        return TryCreate(configuration, use, providerOverride, apiKeyOverride, requireEnabled: true, out settings);
    }

    public static bool TryCreate(
        IConfiguration configuration,
        BidOpsAiUse use,
        string? providerOverride,
        string? apiKeyOverride,
        bool requireEnabled,
        out BidOpsAiHttpSettings settings)
    {
        settings = default!;
        var provider = NormalizeProvider(FirstNonEmpty(providerOverride, configuration["BidOps:Ai:Provider"], BidOpsSystemValues.AiProviderCodexCli));
        if (requireEnabled && (!configuration.GetValue<bool>("BidOps:Ai:Enabled") || !IsEnabledForUse(configuration, use, provider)))
            return false;

        if (!IsSupportedProvider(provider))
            return false;

        var apiKey = ResolveApiKey(configuration, provider, apiKeyOverride);
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        var model = ResolveModel(configuration, provider);
        if (string.IsNullOrWhiteSpace(model))
            return false;

        var endpoint = ResolveEndpoint(configuration, provider);
        if (string.IsNullOrWhiteSpace(endpoint))
            return false;

        var configuredMaxOutputTokens = configuration.GetValue<int?>("BidOps:Ai:MaxOutputTokens");

        settings = new BidOpsAiHttpSettings(
            provider,
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
        string? providerOverride = null,
        string? apiKeyOverride = null)
    {
        var provider = NormalizeProvider(FirstNonEmpty(providerOverride, configuration["BidOps:Ai:Provider"], BidOpsSystemValues.AiProviderCodexCli));
        var apiKey = ResolveApiKey(configuration, provider, apiKeyOverride);
        var model = ResolveModel(configuration, provider);
        var endpoint = ResolveEndpoint(configuration, provider);

        return new BidOpsAiHttpSettingsDiagnostics(
            configuration.GetValue<bool>("BidOps:Ai:Enabled"),
            IsEnabledForUse(configuration, use, provider),
            provider,
            IsSupportedProvider(provider),
            ResolveApiKeySource(configuration, provider, apiKeyOverride),
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
            BidOpsAiUse.OutcomeSuppliers => configuration.GetValue<bool?>("BidOps:Ai:UseForOutcomeSuppliers") ?? IsOpenAiCompatibleProvider(provider),
            _ => false
        };
    }

    private static bool IsOpenAiCompatibleProvider(string provider)
    {
        return string.Equals(provider, BidOpsSystemValues.AiProviderDeepSeek, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, BidOpsSystemValues.AiProviderMimo, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedProvider(string provider)
    {
        return string.Equals(provider, BidOpsSystemValues.AiProviderDeepSeek, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, BidOpsSystemValues.AiProviderMimo, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveEndpoint(IConfiguration configuration, string provider)
    {
        if (string.Equals(provider, BidOpsSystemValues.AiProviderMimo, StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = FirstNonEmpty(
                configuration["BidOps:Mimo:Endpoint"],
                GenericAiValueForProvider(configuration, provider, "Endpoint"));
            if (!string.IsNullOrWhiteSpace(endpoint))
                return EnsureChatCompletionsEndpoint(endpoint);

            var baseUrl = FirstNonEmpty(
                configuration["BidOps:Mimo:BaseUrl"],
                GenericAiValueForProvider(configuration, provider, "BaseUrl"),
                BidOpsSystemValues.DefaultMimoBaseUrl);
            return EnsureChatCompletionsEndpoint(baseUrl);
        }

        if (string.Equals(provider, BidOpsSystemValues.AiProviderDeepSeek, StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = FirstNonEmpty(
                configuration["BidOps:Ai:Endpoint"],
                configuration["BidOps:DeepSeek:Endpoint"]);
            if (!string.IsNullOrWhiteSpace(endpoint))
                return EnsureChatCompletionsEndpoint(endpoint);

            var baseUrl = FirstNonEmpty(
                configuration["BidOps:Ai:BaseUrl"],
                configuration["BidOps:DeepSeek:BaseUrl"],
                BidOpsSystemValues.DefaultDeepSeekBaseUrl);
            return EnsureChatCompletionsEndpoint(baseUrl);
        }

        var openAiEndpoint = FirstNonEmpty(configuration["BidOps:Ai:Endpoint"]);
        if (!string.IsNullOrWhiteSpace(openAiEndpoint))
            return openAiEndpoint.Trim();

        var openAiBaseUrl = FirstNonEmpty(configuration["BidOps:Ai:BaseUrl"]);
        return string.IsNullOrWhiteSpace(openAiBaseUrl)
            ? string.Empty
            : EnsureChatCompletionsEndpoint(openAiBaseUrl);
    }

    private static string EnsureChatCompletionsEndpoint(string value)
    {
        var trimmed = value.Trim().TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/chat/completions";
    }

    private static string ResolveModel(IConfiguration configuration, string provider)
    {
        if (string.Equals(provider, BidOpsSystemValues.AiProviderMimo, StringComparison.OrdinalIgnoreCase))
        {
            return FirstNonEmpty(
                configuration["BidOps:Mimo:Model"],
                GenericAiValueForProvider(configuration, provider, "Model"),
                BidOpsSystemValues.DefaultMimoModel);
        }

        if (string.Equals(provider, BidOpsSystemValues.AiProviderDeepSeek, StringComparison.OrdinalIgnoreCase))
        {
            return FirstNonEmpty(
                configuration["BidOps:DeepSeek:Model"],
                configuration["BidOps:Ai:Model"],
                BidOpsSystemValues.DefaultDeepSeekModel);
        }

        return FirstNonEmpty(configuration["BidOps:Ai:Model"]);
    }

    private static string ResolveApiKey(IConfiguration configuration, string provider, string? apiKeyOverride = null)
    {
        if (string.Equals(provider, BidOpsSystemValues.AiProviderMimo, StringComparison.OrdinalIgnoreCase))
        {
            return FirstNonEmpty(
                apiKeyOverride,
                configuration["BidOps:Mimo:ApiKey"],
                GenericAiValueForProvider(configuration, provider, "ApiKey"),
                Environment.GetEnvironmentVariable("MIMO_API_KEY"));
        }

        if (string.Equals(provider, BidOpsSystemValues.AiProviderDeepSeek, StringComparison.OrdinalIgnoreCase))
        {
            return FirstNonEmpty(
                apiKeyOverride,
                configuration["BidOps:Ai:ApiKey"],
                configuration["BidOps:DeepSeek:ApiKey"],
                Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"));
        }

        return FirstNonEmpty(configuration["BidOps:Ai:ApiKey"]);
    }

    private static string ResolveApiKeySource(IConfiguration configuration, string provider, string? apiKeyOverride = null)
    {
        if (string.Equals(provider, BidOpsSystemValues.AiProviderMimo, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(apiKeyOverride))
                return "Runtime";

            if (!string.IsNullOrWhiteSpace(configuration["BidOps:Mimo:ApiKey"]))
                return "BidOps:Mimo:ApiKey";

            if (!string.IsNullOrWhiteSpace(GenericAiValueForProvider(configuration, provider, "ApiKey")))
                return "BidOps:Ai:ApiKey";

            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MIMO_API_KEY")))
                return "MIMO_API_KEY";

            return "None";
        }

        if (string.Equals(provider, BidOpsSystemValues.AiProviderDeepSeek, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(apiKeyOverride))
                return "Runtime";

            if (!string.IsNullOrWhiteSpace(configuration["BidOps:Ai:ApiKey"]))
                return "BidOps:Ai:ApiKey";

            if (!string.IsNullOrWhiteSpace(configuration["BidOps:DeepSeek:ApiKey"]))
                return "BidOps:DeepSeek:ApiKey";

            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")))
                return "DEEPSEEK_API_KEY";

            return "None";
        }

        if (!string.IsNullOrWhiteSpace(configuration["BidOps:Ai:ApiKey"]))
            return "BidOps:Ai:ApiKey";

        return "None";
    }

    private static string GenericAiValueForProvider(IConfiguration configuration, string provider, string key)
    {
        var configuredProvider = NormalizeProvider(configuration["BidOps:Ai:Provider"]);
        return string.Equals(configuredProvider, provider, StringComparison.OrdinalIgnoreCase)
            ? FirstNonEmpty(configuration[$"BidOps:Ai:{key}"])
            : string.Empty;
    }

    private static string NormalizeProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return BidOpsSystemValues.AiProviderCodexCli;

        var trimmed = provider.Trim();
        if (trimmed.Equals(BidOpsSystemValues.AiProviderDeepSeek, StringComparison.OrdinalIgnoreCase))
            return BidOpsSystemValues.AiProviderDeepSeek;

        if (trimmed.Equals(BidOpsSystemValues.AiProviderMimo, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("XiaomiMimo", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("MiMo", StringComparison.OrdinalIgnoreCase))
        {
            return BidOpsSystemValues.AiProviderMimo;
        }

        if (trimmed.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase))
            return "OpenAICompatible";

        if (trimmed.Equals(BidOpsSystemValues.AiProviderCodexCli, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("CodexCLI", StringComparison.OrdinalIgnoreCase))
        {
            return BidOpsSystemValues.AiProviderCodexCli;
        }

        return trimmed;
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

internal static class BidOpsAiHttpRateLimiter
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, DateTimeOffset> NextAllowedAtUtc = new(StringComparer.OrdinalIgnoreCase);

    public static async Task WaitAsync(
        BidOpsAiHttpSettings settings,
        IConfiguration configuration,
        CancellationToken ct)
    {
        var minInterval = ResolveMinInterval(settings, configuration);
        var key = BuildKey(settings);

        while (true)
        {
            TimeSpan delay;
            lock (SyncRoot)
            {
                var now = DateTimeOffset.UtcNow;
                if (!NextAllowedAtUtc.TryGetValue(key, out var nextAllowedAt))
                    nextAllowedAt = now;

                delay = nextAllowedAt - now;
                if (delay <= TimeSpan.Zero)
                {
                    if (minInterval > TimeSpan.Zero)
                        NextAllowedAtUtc[key] = now.Add(minInterval);

                    return;
                }
            }

            await Task.Delay(delay, ct);
        }
    }

    public static void RegisterRateLimit(
        BidOpsAiHttpSettings settings,
        IConfiguration configuration)
    {
        var backoff = ResolveRateLimitBackoff(settings, configuration);
        if (backoff <= TimeSpan.Zero)
            return;

        var key = BuildKey(settings);
        lock (SyncRoot)
        {
            var nextAllowedAt = DateTimeOffset.UtcNow.Add(backoff);
            if (!NextAllowedAtUtc.TryGetValue(key, out var existing) || existing < nextAllowedAt)
                NextAllowedAtUtc[key] = nextAllowedAt;
        }
    }

    private static string BuildKey(BidOpsAiHttpSettings settings)
    {
        return Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var uri)
            ? $"{settings.Provider}:{uri.Host}"
            : settings.Provider;
    }

    private static TimeSpan ResolveMinInterval(
        BidOpsAiHttpSettings settings,
        IConfiguration configuration)
    {
        var defaultSeconds = settings.Provider.Equals(BidOpsSystemValues.AiProviderMimo, StringComparison.OrdinalIgnoreCase)
            ? 15
            : 0;
        var seconds = configuration.GetValue<int?>($"BidOps:{settings.Provider}:MinRequestIntervalSeconds") ??
            configuration.GetValue<int?>("BidOps:Ai:MinRequestIntervalSeconds") ??
            defaultSeconds;

        return TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 600));
    }

    private static TimeSpan ResolveRateLimitBackoff(
        BidOpsAiHttpSettings settings,
        IConfiguration configuration)
    {
        var defaultSeconds = settings.Provider.Equals(BidOpsSystemValues.AiProviderMimo, StringComparison.OrdinalIgnoreCase)
            ? 180
            : 60;
        var seconds = configuration.GetValue<int?>($"BidOps:{settings.Provider}:RateLimitBackoffSeconds") ??
            configuration.GetValue<int?>("BidOps:Ai:RateLimitBackoffSeconds") ??
            defaultSeconds;

        return TimeSpan.FromSeconds(Math.Clamp(seconds, 5, 1800));
    }
}
