using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Modules.BidOps;

namespace Atlas.Modules.BidOps.Ai;

public interface IBidOpsAiCallDiagnostics
{
    IReadOnlyList<BidOpsAiCallDiagnosticEntry> Entries { get; }

    bool HasEntries { get; }

    void Record(BidOpsAiCallDiagnosticEntry entry);
}

public sealed class BidOpsAiCallDiagnostics : IBidOpsAiCallDiagnostics
{
    private readonly List<BidOpsAiCallDiagnosticEntry> _entries = [];

    public IReadOnlyList<BidOpsAiCallDiagnosticEntry> Entries => _entries;

    public bool HasEntries => _entries.Count > 0;

    public void Record(BidOpsAiCallDiagnosticEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
    }
}

public sealed record BidOpsAiCallDiagnosticEntry(
    string Use,
    string Provider,
    string Model,
    string Endpoint,
    int StatusCode,
    long ElapsedMilliseconds,
    int ResponseCharacters,
    int AssistantCharacters,
    string FinishReason,
    string RawResponseBody,
    string AssistantContent,
    string RequestSummaryJson = "",
    string RequestBodyJson = "",
    string RequestPrompt = "");

internal static partial class BidOpsAiDiagnosticRequestCapture
{
    private const int MaxRequestCharacters = 240_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string BuildHttpSummary(
        string use,
        string provider,
        string model,
        string endpoint,
        string prompt,
        string requestJson,
        IReadOnlyDictionary<string, object?>? extra = null)
    {
        var summary = BuildCommonSummary("Http", use, provider, model, endpoint, prompt, requestJson);
        summary["method"] = "POST";
        summary["contentType"] = "application/json";
        summary["authorization"] = "Bearer ***REDACTED***";
        if (extra != null)
        {
            foreach (var item in extra)
                summary[item.Key] = item.Value;
        }

        return Serialize(summary);
    }

    public static string BuildCodexCliSummary(
        BidOpsCodexCliRequest request,
        IReadOnlyDictionary<string, object?>? extra = null)
    {
        var summary = BuildCommonSummary(
            "CodexCli",
            request.Use,
            BidOpsSystemValues.AiProviderCodexCli,
            request.Model,
            request.BinaryPath,
            request.Prompt,
            request.OutputSchemaJson);
        summary["binaryPath"] = request.BinaryPath;
        summary["reasoningEffort"] = request.ReasoningEffort;
        summary["workingDirectory"] = request.WorkingDirectory;
        summary["timeoutSeconds"] = request.TimeoutSeconds;
        summary["sandbox"] = request.Sandbox;
        summary["skipGitRepoCheck"] = request.SkipGitRepoCheck;
        summary["ignoreUserConfig"] = request.IgnoreUserConfig;
        summary["ignoreRules"] = request.IgnoreRules;
        summary["ephemeral"] = request.Ephemeral;
        summary["hasApiKey"] = !string.IsNullOrWhiteSpace(request.ApiKey);
        summary["apiKey"] = "***REDACTED***";
        summary["stdin"] = "-";
        if (extra != null)
        {
            foreach (var item in extra)
                summary[item.Key] = item.Value;
        }

        return Serialize(summary);
    }

    public static string CaptureHttpRequestBody(string requestJson)
    {
        return Truncate(RedactSecrets(requestJson), MaxRequestCharacters);
    }

    public static string CaptureCodexCliRequestBody(BidOpsCodexCliRequest request)
    {
        var body = new Dictionary<string, object?>
        {
            ["transport"] = "CodexCli",
            ["binaryPath"] = request.BinaryPath,
            ["model"] = request.Model,
            ["reasoningEffort"] = request.ReasoningEffort,
            ["workingDirectory"] = request.WorkingDirectory,
            ["timeoutSeconds"] = request.TimeoutSeconds,
            ["sandbox"] = request.Sandbox,
            ["skipGitRepoCheck"] = request.SkipGitRepoCheck,
            ["ignoreUserConfig"] = request.IgnoreUserConfig,
            ["ignoreRules"] = request.IgnoreRules,
            ["ephemeral"] = request.Ephemeral,
            ["apiKey"] = "***REDACTED***",
            ["stdinPrompt"] = CapturePrompt(request.Prompt),
            ["outputSchemaJson"] = CaptureHttpRequestBody(request.OutputSchemaJson)
        };
        return Truncate(Serialize(body), MaxRequestCharacters);
    }

    public static string CapturePrompt(string prompt)
    {
        return Truncate(RedactSecrets(prompt), MaxRequestCharacters);
    }

    private static Dictionary<string, object?> BuildCommonSummary(
        string transport,
        string use,
        string provider,
        string model,
        string endpoint,
        string prompt,
        string requestPayload)
    {
        return new Dictionary<string, object?>
        {
            ["transport"] = transport,
            ["use"] = use,
            ["provider"] = provider,
            ["model"] = model,
            ["endpoint"] = endpoint,
            ["promptCharacters"] = prompt.Length,
            ["requestPayloadCharacters"] = requestPayload.Length
        };
    }

    private static string Serialize(IReadOnlyDictionary<string, object?> value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static string RedactSecrets(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // 后台任务结果可被运维排查，必须保留请求形状但隐藏密钥、Token、Cookie。
        var redacted = SensitiveJsonPropertyRegex().Replace(value, "$1***REDACTED***$3");
        redacted = AuthorizationHeaderRegex().Replace(redacted, "$1***REDACTED***");
        return redacted;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        return value[..maxLength] + $"... [truncated {value.Length - maxLength} chars]";
    }

    [GeneratedRegex(
        "(\"(?:apiKey|api_key|authorization|token|cookie|password|secret|CODEX_API_KEY)\"\\s*:\\s*\")([^\"]*)(\")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveJsonPropertyRegex();

    [GeneratedRegex(
        "((?:Authorization|Cookie|Token)\\s*[:=]\\s*)(?:Bearer\\s+)?[^\\s,;\\r\\n]{8,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeaderRegex();
}
