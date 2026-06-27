using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Ai;

public sealed class BidOpsLifecycleFieldEnrichmentAiService : IBidOpsLifecycleFieldEnrichmentAiService
{
    private const int DefaultSourceMaxCharacters = 22_000;
    private const int ReviewerPromptSourceMaxCharacters = 32_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    private const string FieldEnrichmentJsonSchema = """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "fields": {
      "type": "array",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "properties": {
          "fieldName": { "type": "string" },
          "value": { "type": "string" },
          "numericValue": { "type": ["number", "string", "null"] },
          "sourceStage": { "type": "string" },
          "sourceRawNoticeId": { "type": ["integer", "string", "null"] },
          "sourceRawAttachmentId": { "type": ["integer", "string", "null"] },
          "evidenceText": { "type": "string" },
          "confidence": { "type": "number" },
          "reason": { "type": "string" }
        },
        "required": ["fieldName", "value", "numericValue", "sourceStage", "sourceRawNoticeId", "sourceRawAttachmentId", "evidenceText", "confidence", "reason"]
      }
    },
    "confidence": { "type": "number" },
    "requiresManualReview": { "type": "boolean" },
    "summary": { "type": "string" },
    "conflicts": {
      "type": "array",
      "items": { "type": "string" }
    }
  },
  "required": ["fields", "confidence", "requiresManualReview", "summary", "conflicts"]
}
""";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IBidOpsAiCallDiagnostics _diagnostics;
    private readonly ILogger<BidOpsLifecycleFieldEnrichmentAiService> _logger;
    private readonly IBidOpsCodexCliClient? _codexCli;
    private readonly IBidOpsAiSettingsService? _aiSettings;

    public BidOpsLifecycleFieldEnrichmentAiService(
        HttpClient httpClient,
        IConfiguration configuration,
        IBidOpsAiCallDiagnostics diagnostics,
        ILogger<BidOpsLifecycleFieldEnrichmentAiService> logger,
        IBidOpsCodexCliClient? codexCli = null,
        IBidOpsAiSettingsService? aiSettings = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _codexCli = codexCli;
        _aiSettings = aiSettings;
    }

    public async Task<BidOpsLifecycleFieldEnrichmentResult> EnrichAsync(
        BidOpsLifecycleFieldEnrichmentRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Evidence.Count == 0)
            return EmptyResult("没有可用于字段补全的公告文本或附件文本。");

        var runtimeSettings = await ResolveRuntimeSettingsAsync(GetCodexCliScenario(request), ct);
        if (BidOpsCodexCliSettingsFactory.TryCreate(
            _configuration,
            BidOpsAiUse.OutcomeSuppliers,
            runtimeSettings?.Provider,
            runtimeSettings?.CodexCliModel,
            runtimeSettings?.CodexCliReasoningEffort,
            out var codexSettings))
        {
            if (_codexCli == null)
            {
                _logger.LogWarning("BidOps lifecycle field enrichment skipped because IBidOpsCodexCliClient is not registered.");
                return EmptyResult("Codex CLI 未注册，无法执行 AI 字段补全。");
            }

            return await EnrichWithCodexCliAsync(request, codexSettings, ct);
        }

        if (!BidOpsAiHttpSettingsFactory.TryCreate(
            _configuration,
            BidOpsAiUse.OutcomeSuppliers,
            runtimeSettings?.Provider,
            runtimeSettings?.HttpApiKey,
            out var settings))
        {
            LogUnavailableSettings(runtimeSettings?.Provider, runtimeSettings?.HttpApiKey);
            return EmptyResult("AI Provider 未启用或缺少可用 token/model/endpoint。");
        }

        return await EnrichWithOpenAiCompatibleAsync(request, settings, ct);
    }

    private async Task<BidOpsLifecycleFieldEnrichmentResult> EnrichWithOpenAiCompatibleAsync(
        BidOpsLifecycleFieldEnrichmentRequest request,
        BidOpsAiHttpSettings settings,
        CancellationToken ct)
    {
        var prompt = BuildPrompt(request, settings.MaxInputCharacters);
        var requestBody = new Dictionary<string, object?>
        {
            ["model"] = settings.Model,
            ["temperature"] = 0,
            ["response_format"] = new { type = "json_object" },
            ["messages"] = new object[]
            {
                new
                {
                    role = "system",
                    content = "你负责对公开采购生命周期证据做字段级补全，只能基于提供的公告文本和附件文本输出 JSON。不要编造事实，不要输出推理过程、Markdown 或 JSON 之外的文字。"
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        };
        if (settings.MaxOutputTokens.HasValue)
            requestBody["max_tokens"] = settings.MaxOutputTokens.Value;

        var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "BidOps lifecycle field enrichment AI request started. provider={Provider}, model={Model}, endpoint={Endpoint}, linkId={LinkId}, promptChars={PromptChars}, reviewerPrompt={HasReviewerPrompt}.",
            settings.Provider,
            settings.Model,
            FormatEndpointForLog(settings.Endpoint),
            request.LinkId,
            prompt.Length,
            !string.IsNullOrWhiteSpace(request.ReviewerPrompt));

        await BidOpsAiHttpRateLimiter.WaitAsync(settings, _configuration, ct);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        stopwatch.Stop();
        var content = BidOpsAiJsonLogging.ExtractAssistantContentOrRaw(responseText);
        var finishReason = BidOpsAiJsonLogging.ExtractFinishReason(responseText);
        _diagnostics.Record(new BidOpsAiCallDiagnosticEntry(
            "LifecycleFieldEnrichment",
            settings.Provider,
            settings.Model,
            FormatEndpointForLog(settings.Endpoint),
            (int)response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            responseText.Length,
            content.Length,
            finishReason,
            responseText,
            content));

        if ((int)response.StatusCode == 429)
            BidOpsAiHttpRateLimiter.RegisterRateLimit(settings, _configuration);

        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning(
                "BidOps lifecycle field enrichment AI request failed. provider={Provider}, model={Model}, endpoint={Endpoint}, statusCode={StatusCode}, responseChars={ResponseChars}.",
                settings.Provider,
                settings.Model,
                FormatEndpointForLog(settings.Endpoint),
                (int)response.StatusCode,
                responseText.Length);
            return EmptyResult($"AI 字段补全失败：HTTP {(int)response.StatusCode}");
        }

        return ParseResult(content);
    }

    private async Task<BidOpsLifecycleFieldEnrichmentResult> EnrichWithCodexCliAsync(
        BidOpsLifecycleFieldEnrichmentRequest request,
        BidOpsCodexCliSettings settings,
        CancellationToken ct)
    {
        var prompt = BuildPrompt(request, settings.MaxInputCharacters);
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "BidOps lifecycle field enrichment Codex CLI request started. provider={Provider}, binary={BinaryPath}, model={Model}, reasoningEffort={ReasoningEffort}, linkId={LinkId}, promptChars={PromptChars}, reviewerPrompt={HasReviewerPrompt}.",
            settings.Provider,
            FormatEndpointForLog(settings.BinaryPath),
            settings.Model,
            settings.ReasoningEffort,
            request.LinkId,
            prompt.Length,
            !string.IsNullOrWhiteSpace(request.ReviewerPrompt));

        var result = await _codexCli!.ExecuteJsonAsync(
            BidOpsCodexCliSettingsFactory.CreateRequest(
                settings,
                BidOpsAiUse.OutcomeSuppliers,
                BuildCodexExtractionPrompt(prompt),
                FieldEnrichmentJsonSchema),
            ct);
        stopwatch.Stop();

        var content = BidOpsAiJsonLogging.ExtractJsonObjectOrRaw(result.AssistantContent);
        var combinedResponse = CombineCodexOutput(result.Stdout, result.Stderr);
        _diagnostics.Record(new BidOpsAiCallDiagnosticEntry(
            "LifecycleFieldEnrichment",
            settings.Provider,
            settings.Model,
            FormatEndpointForLog(settings.BinaryPath),
            result.ExitCode == 0 ? 200 : result.ExitCode,
            stopwatch.ElapsedMilliseconds,
            combinedResponse.Length,
            content.Length,
            $"exit:{result.ExitCode}",
            combinedResponse,
            content));

        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(content))
            return EmptyResult($"Codex CLI 字段补全失败：exit {result.ExitCode}");

        return ParseResult(content);
    }

    private async Task<BidOpsEffectiveAiRuntimeSettings?> ResolveRuntimeSettingsAsync(
        string scenario,
        CancellationToken ct)
    {
        if (_aiSettings == null)
            return null;

        try
        {
            var settings = await _aiSettings.GetSettingsAsync(ct);
            var codexSettings = settings.CodexCliScenarios
                .FirstOrDefault(x => x.Scenario.Equals(scenario, StringComparison.OrdinalIgnoreCase)) ??
                settings.CodexCliScenarios.FirstOrDefault(x => x.Scenario.Equals(BidOpsCodexCliScenarios.Default, StringComparison.OrdinalIgnoreCase));
            var httpApiKey = settings.EffectiveProvider.Equals(BidOpsSystemValues.AiProviderMimo, StringComparison.OrdinalIgnoreCase)
                ? await _aiSettings.GetEffectiveMimoApiKeyAsync(ct)
                : settings.EffectiveProvider.Equals(BidOpsSystemValues.AiProviderDeepSeek, StringComparison.OrdinalIgnoreCase)
                    ? await _aiSettings.GetEffectiveDeepSeekApiKeyAsync(ct)
                    : string.Empty;
            return new BidOpsEffectiveAiRuntimeSettings(
                settings.EffectiveProvider,
                codexSettings?.Model ?? settings.CodexCliModel,
                codexSettings?.ReasoningEffort ?? settings.CodexCliReasoningEffort,
                httpApiKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "BidOps lifecycle field enrichment AI runtime settings could not be read; appsettings provider/model/reasoning settings will be used.");
            return null;
        }
    }

    private static string GetCodexCliScenario(BidOpsLifecycleFieldEnrichmentRequest request)
    {
        return string.IsNullOrWhiteSpace(request.ReviewerPrompt)
            ? BidOpsCodexCliScenarios.Complex
            : BidOpsCodexCliScenarios.ReviewerPrompt;
    }

    private void LogUnavailableSettings(string? providerOverride, string? apiKeyOverride)
    {
        var diagnostics = BidOpsAiHttpSettingsFactory.Diagnose(
            _configuration,
            BidOpsAiUse.OutcomeSuppliers,
            providerOverride,
            apiKeyOverride);
        _logger.LogWarning(
            "BidOps lifecycle field enrichment skipped because AI settings are unavailable. enabled={Enabled}, useEnabled={UseEnabled}, provider={Provider}, supportedProvider={SupportedProvider}, apiKeySource={ApiKeySource}, hasApiKey={HasApiKey}, hasModel={HasModel}, hasEndpoint={HasEndpoint}.",
            diagnostics.Enabled,
            diagnostics.UseEnabled,
            diagnostics.Provider,
            diagnostics.SupportedProvider,
            diagnostics.ApiKeySource,
            diagnostics.HasApiKey,
            diagnostics.HasModel,
            diagnostics.HasEndpoint);
    }

    private static string BuildPrompt(BidOpsLifecycleFieldEnrichmentRequest request, int maxInputCharacters)
    {
        var sourceMaxCharacters = string.IsNullOrWhiteSpace(request.ReviewerPrompt)
            ? Math.Min(maxInputCharacters, DefaultSourceMaxCharacters)
            : Math.Min(maxInputCharacters, ReviewerPromptSourceMaxCharacters);
        var reviewerPrompt = string.IsNullOrWhiteSpace(request.ReviewerPrompt)
            ? "审核人员没有提供额外提示。"
            : request.ReviewerPrompt.Trim();
        var sourceBundle = BuildSourceBundle(request.Evidence, sourceMaxCharacters);

        return $$"""
输出要求：
- 只返回一个 JSON 对象；第一个字符必须是 {，最后一个字符必须是 }。
- JSON 必须包含 fields、confidence、requiresManualReview、summary、conflicts。
- fields 中每个对象必须包含 fieldName、value、numericValue、sourceStage、sourceRawNoticeId、sourceRawAttachmentId、evidenceText、confidence、reason。

目标：
对一条采购生命周期闭环记录做字段级补全。可补字段包括但不限于：
- lotNo：分标编号/标段编号。
- lotName：分标名称/标段名称。
- packageNo：包号/包件号。
- packageName：包名称/包件名称。
- supplierName：中标/成交商家。
- finalAwardAmount：中标/成交金额，统一人民币元，numericValue 必须是元。
- finalAwardAmountSource：金额来源说明，例如 DirectAwardAmount、TenderBudget、TenderMaxPrice、Missing、Unknown。
- projectCode、projectName。

字段来源优先级：
- 如果中标/成交结果公告明确给出某字段，优先使用结果公告。
- 结果公告缺字段时，可结合候选人公示。
- 结果公告和候选人公示都缺字段时，可结合采购/招标公告。
- 金额字段尤其要保守：中标/成交金额优先；没有中标金额时，采购公告中的预算/最高限价/指导价只能作为补全建议，finalAwardAmountSource 必须清楚标为 TenderBudget/TenderMaxPrice/TenderGuidePrice，且 requiresManualReview=true。
- 如果采购公告和结果公告都没有金额，不要编造金额，返回 finalAwardAmount 的 value 为空、numericValue 为 null，或不返回该字段。

匹配规则：
- 采购编号/项目编号必须一致或有明确同项目证据。
- 包号相同但采购公告中多个分标重复使用同一包号时，不能仅凭包号唯一定位；需要分标编号、分标名称、包名称、供应商或原文行证据辅助判断。
- 如果无法唯一定位，返回 conflicts 并把 requiresManualReview 设为 true。
- evidenceText 必须是公开公告或附件中的简短原文片段，不要写总结当证据。
- 不要补公告中不存在的事实；不确定时给低 confidence 或 conflicts。

当前闭环字段：
linkId: {{request.LinkId}}
projectCode: {{request.ProjectCode}}
projectName: {{request.ProjectName}}
lotNo: {{request.LotNo}}
lotName: {{request.LotName}}
packageNo: {{request.PackageNo}}
packageName: {{request.PackageName}}
supplierName: {{request.SupplierName}}
finalAwardAmount: {{request.FinalAwardAmount?.ToString(CultureInfo.InvariantCulture) ?? ""}}
finalAwardAmountSource: {{request.FinalAwardAmountSource}}

当前闭环 EvidenceJson：
{{Truncate(request.EvidenceJson, 6000)}}

审核人员提示：
{{reviewerPrompt}}

公开来源材料：
{{sourceBundle}}
""";
    }

    private static string BuildSourceBundle(IReadOnlyList<BidOpsLifecycleFieldEvidenceInput> evidence, int maxCharacters)
    {
        var builder = new StringBuilder(maxCharacters);
        var remaining = maxCharacters;
        foreach (var item in evidence)
        {
            if (remaining <= 0)
                break;

            var header = $"""

## {item.Stage}
RawNoticeId: {item.RawNoticeId?.ToString(CultureInfo.InvariantCulture) ?? ""}
RawAttachmentId: {item.RawAttachmentId?.ToString(CultureInfo.InvariantCulture) ?? ""}
Title: {item.Title}
NoticeType: {item.NoticeType}
SourceUrl: {item.SourceUrl}
AttachmentName: {item.AttachmentName}
Text:
""";
            AppendBudgeted(builder, header, ref remaining);
            AppendBudgeted(builder, ExtractRelevantText(item.Text, Math.Min(remaining, maxCharacters / Math.Max(1, evidence.Count))), ref remaining);
        }

        return builder.ToString();
    }

    private static string ExtractRelevantText(string value, int maxCharacters)
    {
        if (maxCharacters <= 0 || string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = BidOpsTextQuality.CleanExtractedValue(value);
        if (text.Length <= maxCharacters)
            return text;

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsRelevantLine)
            .ToList();
        var relevant = string.Join(Environment.NewLine, lines);
        return Truncate(string.IsNullOrWhiteSpace(relevant) ? text : relevant, maxCharacters);
    }

    private static bool IsRelevantLine(string line)
    {
        return ContainsAny(
                line,
                "采购编号",
                "项目编号",
                "项目名称",
                "分标编号",
                "标段编号",
                "分标名称",
                "标段名称",
                "包号",
                "包名称",
                "包件",
                "中标",
                "成交",
                "供应商",
                "金额",
                "报价",
                "预算",
                "最高限价",
                "指导价",
                "万元",
                "元") ||
            line.Contains("有限公司", StringComparison.Ordinal) ||
            line.Contains("公司", StringComparison.Ordinal);
    }

    private static void AppendBudgeted(StringBuilder builder, string value, ref int remaining)
    {
        if (remaining <= 0 || string.IsNullOrWhiteSpace(value))
            return;

        var text = value.Length <= remaining ? value : value[..remaining];
        builder.AppendLine(text);
        remaining -= text.Length;
    }

    private static BidOpsLifecycleFieldEnrichmentResult ParseResult(string content)
    {
        using var document = JsonDocument.Parse(StripJsonFence(content));
        var root = document.RootElement;
        var fields = new List<BidOpsLifecycleFieldSuggestion>();
        if (TryGetProperty(root, "fields", out var fieldsElement) &&
            fieldsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in fieldsElement.EnumerateArray())
            {
                var fieldName = Trim(GetString(item, "fieldName"), 128);
                if (string.IsNullOrWhiteSpace(fieldName))
                    continue;

                fields.Add(new BidOpsLifecycleFieldSuggestion(
                    fieldName,
                    Trim(GetString(item, "value"), 1000),
                    GetDecimal(item, "numericValue"),
                    Trim(GetString(item, "sourceStage"), 64),
                    GetNullableLong(item, "sourceRawNoticeId"),
                    GetNullableLong(item, "sourceRawAttachmentId"),
                    Trim(GetString(item, "evidenceText"), 2000),
                    ClampConfidence(GetDecimal(item, "confidence") ?? 0m),
                    Trim(GetString(item, "reason"), 1000)));
            }
        }

        return new BidOpsLifecycleFieldEnrichmentResult(
            fields,
            ClampConfidence(GetDecimal(root, "confidence") ?? (fields.Count == 0 ? 0m : fields.Max(x => x.Confidence))),
            GetBoolean(root, "requiresManualReview") ?? true,
            Trim(GetString(root, "summary"), 1000),
            ReadStringArray(root, "conflicts"));
    }

    private static BidOpsLifecycleFieldEnrichmentResult EmptyResult(string summary)
    {
        return new BidOpsLifecycleFieldEnrichmentResult([], 0m, true, summary, []);
    }

    private static string BuildCodexExtractionPrompt(string prompt)
    {
        return $$"""
任务：公开采购生命周期字段级补全

执行边界：
- 只使用下面提供的公开来源材料。
- 不要读取工作目录文件，不要执行 shell 命令，不要联网搜索，不要修改任何文件。
- 最终只输出符合 JSON Schema 的 JSON 对象，不要输出 Markdown、解释或代码块。

{{prompt}}
""";
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static string GetString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind == JsonValueKind.Null)
            return string.Empty;

        return BidOpsTextQuality.CleanExtractedValue(value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText().Trim('"'));
    }

    private static decimal? GetDecimal(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number;

        var text = GetString(element, name);
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static long? GetNullableLong(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            return number;

        return long.TryParse(GetString(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static bool? GetBoolean(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();

        return bool.TryParse(GetString(element, name), out var parsed) ? parsed : null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind != JsonValueKind.Array)
            return [];

        return value
            .EnumerateArray()
            .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? string.Empty : x.GetRawText())
            .Select(x => Trim(x, 1000))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static decimal ClampConfidence(decimal value)
    {
        return Math.Clamp(value, 0m, 1m);
    }

    private static string Trim(string value, int maxLength)
    {
        var trimmed = BidOpsTextQuality.CleanExtractedValue(value);
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength];
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string StripJsonFence(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var firstLineEnd = trimmed.IndexOf('\n');
        if (firstLineEnd < 0)
            return trimmed;

        var body = trimmed[(firstLineEnd + 1)..];
        var lastFence = body.LastIndexOf("```", StringComparison.Ordinal);
        return lastFence >= 0 ? body[..lastFence].Trim() : body.Trim();
    }

    private static string CombineCodexOutput(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
            return stdout;

        if (string.IsNullOrWhiteSpace(stdout))
            return stderr;

        return $"{stdout}{Environment.NewLine}{stderr}";
    }

    private static string FormatEndpointForLog(string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";

        return endpoint;
    }

    private sealed record BidOpsEffectiveAiRuntimeSettings(
        string Provider,
        string CodexCliModel,
        string CodexCliReasoningEffort,
        string HttpApiKey);
}
