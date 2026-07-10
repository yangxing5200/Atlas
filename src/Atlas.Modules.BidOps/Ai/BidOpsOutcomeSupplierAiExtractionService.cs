using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Ai;

public sealed class BidOpsOutcomeSupplierAiExtractionService : IBidOpsOutcomeSupplierAiExtractionService
{
    private const int MaxReferenceExtracts = 40;
    private const int DeterministicReferenceMaxCharacters = 6_000;
    private const int DefaultSourceMaxCharacters = 12_000;
    private const int ReviewerPromptSourceMaxCharacters = 18_000;
    private const int SourceLineContextWindow = 2;
    private const int ComplexSourceAttachmentCount = 3;
    private const int ComplexSourceCharacters = 60_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    private const string OutcomeSupplierJsonSchema = """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "records": {
      "type": "array",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "properties": {
          "sourceSequenceNo": { "type": "string" },
          "sourcePageNo": { "type": ["integer", "null"] },
          "sourceTableTitle": { "type": "string" },
          "sourceRowText": { "type": "string" },
          "supplierName": { "type": "string" },
          "supplierNameRaw": { "type": "string" },
          "outcomeType": { "type": "string" },
          "rank": { "type": ["integer", "null"] },
          "awardAmount": { "type": ["number", "string", "null"] },
          "procurementAgencyServiceFeeAmount": { "type": ["number", "string", "null"] },
          "projectName": { "type": "string" },
          "projectCode": { "type": "string" },
          "buyerName": { "type": "string" },
          "rawLotNo": { "type": "string" },
          "lotNo": { "type": "string" },
          "rawLotName": { "type": "string" },
          "lotName": { "type": "string" },
          "rawPackageNo": { "type": "string" },
          "packageNo": { "type": "string" },
          "packageName": { "type": "string" },
          "category": { "type": "string" },
          "evidenceText": { "type": "string" },
          "fieldEvidence": {
            "type": "object",
            "additionalProperties": { "type": "string" }
          },
          "warnings": {
            "type": "array",
            "items": { "type": "string" }
          },
          "confidence": { "type": "number" }
        },
        "required": ["sourceSequenceNo", "sourcePageNo", "sourceTableTitle", "sourceRowText", "supplierName", "supplierNameRaw", "outcomeType", "rank", "awardAmount", "procurementAgencyServiceFeeAmount", "projectName", "projectCode", "buyerName", "rawLotNo", "lotNo", "rawLotName", "lotName", "rawPackageNo", "packageNo", "packageName", "category", "evidenceText", "fieldEvidence", "warnings", "confidence"]
      }
    }
  },
  "required": ["records"]
}
""";

    private static readonly Regex AmountNumberRegex = new(
        @"(?<amount>[0-9]+(?:,[0-9]{3})*(?:\.[0-9]+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IBidOpsAiCallDiagnostics _diagnostics;
    private readonly ILogger<BidOpsOutcomeSupplierAiExtractionService> _logger;
    private readonly IBidOpsCodexCliClient? _codexCli;
    private readonly IBidOpsAiSettingsService? _aiSettings;

    public BidOpsOutcomeSupplierAiExtractionService(
        HttpClient httpClient,
        IConfiguration configuration,
        IBidOpsAiCallDiagnostics diagnostics,
        ILogger<BidOpsOutcomeSupplierAiExtractionService> logger,
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

    public async Task<IReadOnlyList<BidOpsOutcomeSupplierExtract>> ExtractAsync(
        BidOpsOutcomeSupplierAiExtractionRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text) &&
            string.IsNullOrWhiteSpace(request.Html) &&
            (request.Attachments == null || request.Attachments.Count == 0))
        {
            _logger.LogInformation(
                "BidOps outcome supplier AI request skipped because no public source text, HTML, or extracted attachment text was available. noticeType={NoticeType}, titleLength={TitleLength}, reviewerPrompt={HasReviewerPrompt}.",
                request.NoticeType,
                request.Title.Length,
                !string.IsNullOrWhiteSpace(request.ReviewerPrompt));
            return [];
        }

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
                _logger.LogWarning("BidOps outcome supplier Codex CLI extraction skipped because IBidOpsCodexCliClient is not registered.");
                return [];
            }

            try
            {
                return await ExtractWithCodexCliAsync(request, codexSettings, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _diagnostics.Record(new BidOpsAiCallDiagnosticEntry(
                    BidOpsAiUse.OutcomeSuppliers.ToString(),
                    codexSettings.Provider,
                    codexSettings.Model,
                    FormatEndpointForLog(codexSettings.BinaryPath),
                    -1,
                    0,
                    ex.ToString().Length,
                    0,
                    $"exception:{ex.GetType().Name}",
                    Truncate(ex.ToString(), 4000),
                    string.Empty));
                _logger.LogWarning(
                    ex,
                    "BidOps outcome supplier Codex CLI extraction failed; deterministic outcome extraction will be used.");
                return [];
            }
        }

        if (!BidOpsAiHttpSettingsFactory.TryCreate(
            _configuration,
            BidOpsAiUse.OutcomeSuppliers,
            runtimeSettings?.Provider,
            runtimeSettings?.HttpApiKey,
            out var settings))
        {
            LogUnavailableSettings(runtimeSettings?.Provider, runtimeSettings?.HttpApiKey);
            return [];
        }

        try
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
                        content = "你负责从公开中文采购中标/成交结果公告、候选人公示中提取结构化 JSON。必须只返回一个 JSON 对象：第一个字符必须是 {，最后一个字符必须是 }。不要输出推理过程、分析、解释、摘要、Markdown、代码块或任何 JSON 之外的文字。不要编造公告中没有的事实，不要推断非公开信息。"
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
            var requestSummaryJson = BidOpsAiDiagnosticRequestCapture.BuildHttpSummary(
                BidOpsAiUse.OutcomeSuppliers.ToString(),
                settings.Provider,
                settings.Model,
                FormatEndpointForLog(settings.Endpoint),
                prompt,
                requestJson,
                new Dictionary<string, object?>
                {
                    ["noticeType"] = request.NoticeType,
                    ["titleLength"] = request.Title.Length,
                    ["attachmentCount"] = request.Attachments?.Count ?? 0,
                    ["reviewerPrompt"] = !string.IsNullOrWhiteSpace(request.ReviewerPrompt)
                });
            var requestBodyJson = BidOpsAiDiagnosticRequestCapture.CaptureHttpRequestBody(requestJson);
            var requestPrompt = BidOpsAiDiagnosticRequestCapture.CapturePrompt(prompt);
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation(
                "BidOps outcome supplier AI request started. provider={Provider}, model={Model}, endpoint={Endpoint}, noticeType={NoticeType}, titleLength={TitleLength}, promptChars={PromptChars}, attachmentCount={AttachmentCount}, reviewerPrompt={HasReviewerPrompt}.",
                settings.Provider,
                settings.Model,
                FormatEndpointForLog(settings.Endpoint),
                request.NoticeType,
                request.Title.Length,
                prompt.Length,
                request.Attachments?.Count ?? 0,
                !string.IsNullOrWhiteSpace(request.ReviewerPrompt));
            _logger.LogInformation(
                "BidOps outcome supplier AI request body before DeepSeek call. requestBody={RequestBody}",
                requestJson);

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
                BidOpsAiUse.OutcomeSuppliers.ToString(),
                settings.Provider,
                settings.Model,
                FormatEndpointForLog(settings.Endpoint),
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                responseText.Length,
                content.Length,
                finishReason,
                responseText,
                content,
                requestSummaryJson,
                requestBodyJson,
                requestPrompt));
            _logger.LogInformation(
                "BidOps outcome supplier AI raw DeepSeek response. statusCode={StatusCode}, responseBody={ResponseBody}",
                (int)response.StatusCode,
                BidOpsAiJsonLogging.FormatJsonForLog(responseText));
            if ((int)response.StatusCode == 429)
                BidOpsAiHttpRateLimiter.RegisterRateLimit(settings, _configuration);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "BidOps outcome supplier AI request failed. provider={Provider}, model={Model}, endpoint={Endpoint}, statusCode={StatusCode}, elapsedMs={ElapsedMs}, responseChars={ResponseChars}.",
                    settings.Provider,
                    settings.Model,
                    FormatEndpointForLog(settings.Endpoint),
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    responseText.Length);
                response.EnsureSuccessStatusCode();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning(
                    "BidOps outcome supplier AI response had empty assistant content; deterministic outcome extraction will be used. provider={Provider}, model={Model}, endpoint={Endpoint}, statusCode={StatusCode}, elapsedMs={ElapsedMs}, responseChars={ResponseChars}, finishReason={FinishReason}.",
                    settings.Provider,
                    settings.Model,
                    FormatEndpointForLog(settings.Endpoint),
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    responseText.Length,
                    finishReason);
                return [];
            }

            var records = ParseRecords(content);
            _logger.LogInformation(
                "BidOps outcome supplier AI request completed. provider={Provider}, model={Model}, endpoint={Endpoint}, statusCode={StatusCode}, elapsedMs={ElapsedMs}, responseChars={ResponseChars}, assistantChars={AssistantChars}, recordCount={RecordCount}.",
                settings.Provider,
                settings.Model,
                FormatEndpointForLog(settings.Endpoint),
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                responseText.Length,
                content.Length,
                records.Count);
            return records;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "BidOps outcome supplier AI extraction failed; deterministic outcome extraction will be used.");
            return [];
        }
    }

    private async Task<IReadOnlyList<BidOpsOutcomeSupplierExtract>> ExtractWithCodexCliAsync(
        BidOpsOutcomeSupplierAiExtractionRequest request,
        BidOpsCodexCliSettings settings,
        CancellationToken ct)
    {
        var prompt = BuildPrompt(request, settings.MaxInputCharacters);
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "BidOps outcome supplier Codex CLI request started. provider={Provider}, binary={BinaryPath}, model={Model}, reasoningEffort={ReasoningEffort}, sandbox={Sandbox}, noticeType={NoticeType}, titleLength={TitleLength}, promptChars={PromptChars}, attachmentCount={AttachmentCount}, reviewerPrompt={HasReviewerPrompt}.",
            settings.Provider,
            FormatEndpointForLog(settings.BinaryPath),
            settings.Model,
            settings.ReasoningEffort,
            settings.Sandbox,
            request.NoticeType,
            request.Title.Length,
            prompt.Length,
            request.Attachments?.Count ?? 0,
            !string.IsNullOrWhiteSpace(request.ReviewerPrompt));

        var codexRequest = BidOpsCodexCliSettingsFactory.CreateRequest(
            settings,
            BidOpsAiUse.OutcomeSuppliers,
            BuildCodexExtractionPrompt("公开中标/成交/候选厂家明细抽取", prompt),
            OutcomeSupplierJsonSchema);
        var requestSummaryJson = BidOpsAiDiagnosticRequestCapture.BuildCodexCliSummary(
            codexRequest,
            new Dictionary<string, object?>
            {
                ["noticeType"] = request.NoticeType,
                ["titleLength"] = request.Title.Length,
                ["attachmentCount"] = request.Attachments?.Count ?? 0,
                ["reviewerPrompt"] = !string.IsNullOrWhiteSpace(request.ReviewerPrompt)
            });
        var requestBodyJson = BidOpsAiDiagnosticRequestCapture.CaptureCodexCliRequestBody(codexRequest);
        var requestPrompt = BidOpsAiDiagnosticRequestCapture.CapturePrompt(codexRequest.Prompt);
        var result = await _codexCli!.ExecuteJsonAsync(codexRequest, ct);
        stopwatch.Stop();

        var content = BidOpsAiJsonLogging.ExtractJsonObjectOrRaw(result.AssistantContent);
        var combinedResponse = CombineCodexOutput(result.Stdout, result.Stderr);
        _diagnostics.Record(new BidOpsAiCallDiagnosticEntry(
            BidOpsAiUse.OutcomeSuppliers.ToString(),
            settings.Provider,
            settings.Model,
            FormatEndpointForLog(settings.BinaryPath),
            result.ExitCode == 0 ? 200 : result.ExitCode,
            stopwatch.ElapsedMilliseconds,
            combinedResponse.Length,
            content.Length,
            $"exit:{result.ExitCode}",
            combinedResponse,
            content,
            requestSummaryJson,
            requestBodyJson,
            requestPrompt));

        _logger.LogInformation(
            "BidOps outcome supplier Codex CLI response. exitCode={ExitCode}, elapsedMs={ElapsedMs}, stdoutChars={StdoutChars}, stderrChars={StderrChars}, assistantChars={AssistantChars}.",
            result.ExitCode,
            result.ElapsedMilliseconds,
            result.Stdout.Length,
            result.Stderr.Length,
            content.Length);

        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning(
                "BidOps outcome supplier Codex CLI request failed or returned empty content. provider={Provider}, model={Model}, binary={BinaryPath}, exitCode={ExitCode}, stderr={Stderr}.",
                settings.Provider,
                settings.Model,
                FormatEndpointForLog(settings.BinaryPath),
                result.ExitCode,
                Truncate(result.Stderr, 2000));
            return [];
        }

        var records = ParseRecords(content);
        _logger.LogInformation(
            "BidOps outcome supplier Codex CLI request completed. provider={Provider}, model={Model}, reasoningEffort={ReasoningEffort}, exitCode={ExitCode}, elapsedMs={ElapsedMs}, assistantChars={AssistantChars}, recordCount={RecordCount}.",
            settings.Provider,
            settings.Model,
            settings.ReasoningEffort,
            result.ExitCode,
            result.ElapsedMilliseconds,
            content.Length,
            records.Count);
        return records;
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
            _logger.LogWarning(ex, "BidOps outcome supplier AI runtime settings could not be read; appsettings provider/model/reasoning settings will be used.");
            return null;
        }
    }

    private static string GetCodexCliScenario(BidOpsOutcomeSupplierAiExtractionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ReviewerPrompt))
            return BidOpsCodexCliScenarios.ReviewerPrompt;

        return IsComplexSource(request)
            ? BidOpsCodexCliScenarios.Complex
            : BidOpsCodexCliScenarios.Default;
    }

    private static bool IsComplexSource(BidOpsOutcomeSupplierAiExtractionRequest request)
    {
        var attachmentCount = request.Attachments?.Count ?? 0;
        var sourceCharacters = request.Text.Length + (request.Attachments?.Sum(x => x.Text.Length) ?? 0);
        return attachmentCount >= ComplexSourceAttachmentCount ||
            sourceCharacters >= ComplexSourceCharacters;
    }

    private void LogUnavailableSettings(string? providerOverride, string? apiKeyOverride)
    {
        var diagnostics = BidOpsAiHttpSettingsFactory.Diagnose(
            _configuration,
            BidOpsAiUse.OutcomeSuppliers,
            providerOverride,
            apiKeyOverride);
        var level = diagnostics.Enabled && diagnostics.UseEnabled ? LogLevel.Warning : LogLevel.Debug;
        _logger.Log(
            level,
            "BidOps outcome supplier AI request skipped because AI HTTP settings are unavailable. enabled={Enabled}, useEnabled={UseEnabled}, provider={Provider}, supportedProvider={SupportedProvider}, apiKeySource={ApiKeySource}, hasApiKey={HasApiKey}, hasModel={HasModel}, hasEndpoint={HasEndpoint}.",
            diagnostics.Enabled,
            diagnostics.UseEnabled,
            diagnostics.Provider,
            diagnostics.SupportedProvider,
            diagnostics.ApiKeySource,
            diagnostics.HasApiKey,
            diagnostics.HasModel,
            diagnostics.HasEndpoint);
    }

    private static string BuildPrompt(
        BidOpsOutcomeSupplierAiExtractionRequest request,
        int maxInputCharacters)
    {
        var deterministicJson = Truncate(
            JsonSerializer.Serialize(
                request.DeterministicExtracts.Take(MaxReferenceExtracts),
                JsonOptions),
            DeterministicReferenceMaxCharacters);
        var reviewerPrompt = string.IsNullOrWhiteSpace(request.ReviewerPrompt)
            ? "审核人员没有提供额外修正提示。"
            : request.ReviewerPrompt.Trim();
        var sourceMaxCharacters = ResolveSourceMaxCharacters(maxInputCharacters, request.ReviewerPrompt);
        var sourceBundle = BuildSourceBundle(request, sourceMaxCharacters);
        var expectedFields = BuildExpectedFields(request.NoticeType, request.Title);

        return $$"""
输出限制：
- 只返回一个 JSON 对象；第一个字符必须是 {，最后一个字符必须是 }。
- 不要输出推理过程、分析、解释、摘要、Markdown、代码块、前缀或后缀文字。
- JSON 结构必须严格符合下面的形状：
{
  "records": [
    {
      "sourceSequenceNo": "",
      "sourcePageNo": null,
      "sourceTableTitle": "",
      "sourceRowText": "",
      "supplierName": "",
      "supplierNameRaw": "",
      "outcomeType": "Awarded|Candidate|Shortlisted|Failed",
      "rank": null,
      "awardAmount": null,
      "procurementAgencyServiceFeeAmount": null,
      "projectName": "",
      "projectCode": "",
      "buyerName": "",
      "rawLotNo": "",
      "lotNo": "",
      "rawLotName": "",
      "lotName": "",
      "rawPackageNo": "",
      "packageNo": "",
      "packageName": "",
      "category": "",
      "evidenceText": "",
      "fieldEvidence": {
        "supplierName": "",
        "lotNo": "",
        "lotName": "",
        "packageNo": ""
      },
      "warnings": [],
      "confidence": 0.0
    }
  ]
}

规则：
- 只提取公开公告中的中标/成交结果、入围、推荐候选人或成交候选人厂家明细。
- 中文组织名称必须保持公告原文写法，不要改写、翻译或补全。
- 每条 record 必须对应一个原始业务行；不要把同一业务行的换行片段、列片段或表头片段拆成多条 record。
- sourceRowText 必须尽量填写完整原始业务行，包含序号、分标编号、分标名称、包号、供应商等可定位信息；如果只能定位到普通正文片段，也要放入最短可解释原文。
- sourceSequenceNo 放原文行序号/序列号；没有则空字符串。sourcePageNo 放 PDF 页码；没有则 null。sourceTableTitle 放表格标题/附件标题；没有则空字符串。
- rawLotNo/rawLotName/rawPackageNo/supplierNameRaw 放原文中未清洗的字段值；lotNo/lotName/packageNo/supplierName 放清洗后的字段值。若没有换行、拆分或归一化，raw 字段可以与清洗字段相同。
- fieldEvidence 用于记录关键字段对应的原文短证据，至少尽量包含 supplierName、lotNo、lotName、packageNo；没有证据的字段填空字符串，不要臆造。
- warnings 用于记录不确定性，例如“lotNo_split_from_wrapped_text”“missing_amount”“row_boundary_uncertain”；确定无警告时返回空数组。
- 必填/重点字段取决于公告类型：
{{expectedFields}}
- 推荐候选人公示必须提取：采购编号 -> projectCode，分标名称 -> lotName，包号 -> packageNo，包名称 -> packageName，排名 -> rank，推荐的成交候选人/推荐中标候选人 -> supplierName，公开的最终报价 -> awardAmount。lotNo 只有原文表头/上下文明确出现“分标编号/标段编号/分标号/标段号”及对应值时才能填写。
- 中标/成交结果公告必须提取：采购编号 -> projectCode，包号 -> packageNo，中标/成交/成交供应商行的 outcomeType 必须为 Awarded，成交供应商/中标人 -> supplierName。lotNo 只有原文明确给出分标编号时填写；PDF/表格只有包号和厂家时 lotNo 必须返回空字符串。
- projectName 只有原文明确出现“项目名称/工程名称/采购项目名称/招标项目名称/子项目名称”表头或标签并给出对应值时才能填写；没有明确项目名称时必须返回空字符串。
- 不要把公告标题、公告名称、采购批次名称、分标名称、包名称或附件文件名放入 projectName。只有分标名称时填 lotName，projectName 留空。
- 表格列名是“项目名称/工程名称/子项目名称”时，对应值必须放 projectName；只有列名明确是“包名称/包件名称/标包名称/分包名称”时才放 packageName。不要把项目名称列的值放到 packageName。
- 如果表格行里只有包号和厂家，但正文公共部分明确写了采购编号、采购方或分标名称，可以从正文公共部分继承这些字段；不得从采购编号、包号、分标名称或附件文件名推断 lotNo。
- 不要把“采购编号/采购项目编号”和“分标编号”混淆。采购编号/项目编号放 projectCode，分标编号/标段编号放 lotNo。
- 不要把“分标名称”和“分标编号”混淆。名称放 lotName/packageName，编号放 lotNo/packageNo。
- packageNo 必须保留原文写法，包括“包”“第...包”等前缀。原文是“包1”时不要返回裸数字“1”。
- 除非附件文件名里的值也出现在正文或附件内容里，否则不要把附件文件名前缀当成事实。
- 若“中标商家/成交供应商/中标人”列明确写的是“流标状态/流标/废标/采购失败”等状态，保留为展示行：supplierName 按原文填该状态，outcomeType 必须为 Failed，awardAmount 和 procurementAgencyServiceFeeAmount 必须为 null。
- 流标、废标、失败行不得作为中标/候选明细或金额依据返回；只有该行明确给出了真实中标或推荐厂家时，才按真实厂家返回。
- 采购方、代理机构、联系人、银行账户、服务费收取单位、投诉受理单位都不是厂家。
- “采购代理服务费”“代理服务费”“服务费金额”放到 procurementAgencyServiceFeeAmount，不能放到 awardAmount。
- 金额统一返回人民币“元”。只有公告单元格、同列表头或紧邻上下文明确标识“万元/万”时，才在返回 JSON 前乘以 10000；没有明确单位或表头未标识万元时，按“元”处理。未知金额、折扣率、费率、百分比返回 null。
- evidenceText 必须是公告正文或附件文本中的简短原文片段。候选人公示优先包含公开的评审情况、排名或推荐上下文。
- evidenceText 和 sourceRowText 都不能是模型总结；必须来自公告正文、HTML 表格或附件提取文本。
- 优先返回完整可信的记录，不要为了凑数量返回大量不确定碎片。如果没有厂家，返回空 records 数组。
- 下方“规则解析参考结果”只作为参考；如果它漏掉 PDF/表格行，或把正文公共字段混错，你需要根据原文纠正。
- 如果“审核人员修正提示”和规则解析参考结果冲突，并且公告原文支持审核人员修正提示，则优先按审核人员修正提示提取。
- 公告正文 HTML 对 Word/HTML 表格（例如 MsoNormalTable）优先级最高。附件可能包含公开结果表；需要结合附件提取文本和附件元数据识别明细。

审核人员修正提示：
{{reviewerPrompt}}

公告元数据：
标题：{{request.Title}}
公告类型提示：{{request.NoticeType}}
发布时间：{{request.PublishTime?.ToString("O", CultureInfo.InvariantCulture) ?? ""}}

规则解析参考结果：
{{deterministicJson}}

公开来源材料：
{{Truncate(sourceBundle, sourceMaxCharacters)}}
""";
    }

    private static string FormatEndpointForLog(string endpoint)
    {
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            ? $"{uri.Host}{uri.AbsolutePath}"
            : endpoint;
    }

    private static string BuildCodexExtractionPrompt(string taskName, string prompt)
    {
        return $$"""
任务：{{taskName}}

执行边界：
- 只使用下面提供的公开来源材料和规则解析参考结果。
- 不要读取工作目录文件，不要执行 shell 命令，不要联网搜索，不要修改任何文件。
- 最终只输出符合 JSON Schema 的 JSON 对象，不要输出 Markdown、解释或代码块。

{{prompt}}
""";
    }

    private static string CombineCodexOutput(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
            return stdout;

        if (string.IsNullOrWhiteSpace(stdout))
            return stderr;

        return $"{stdout}{Environment.NewLine}{stderr}";
    }

    private static string BuildExpectedFields(string noticeType, string title)
    {
        var signal = $"{noticeType} {title}";
        if (ContainsAny(signal, "CandidateAnnouncement", "中标候选人", "成交候选人", "推荐"))
        {
            return """
  - 公告类型为 CandidateAnnouncement（推荐候选人公示）时：每一行候选人返回一条记录；需要包含 projectCode、lotName、packageNo、packageName、rank、supplierName；公开最终报价放 awardAmount；outcomeType 用 Candidate；evidenceText 包含公开评审情况、排名或推荐上下文。projectName 仅在原文明确给出项目名称时填写；lotNo 仅在原文明确给出分标编号/标段编号时填写，否则空字符串。
""";
        }

        if (ContainsAny(signal, "AwardAnnouncement", "ResultAnnouncement", "中标结果", "成交结果", "结果公告"))
        {
            return """
  - 公告类型为 AwardAnnouncement 或 ResultAnnouncement（中标/成交结果公告）时：每一行中标/成交厂家返回一条记录；需要包含 projectCode、lotName、packageNo、packageName、supplierName；outcomeType 用 Awarded；公开成交金额放 awardAmount；公开代理服务费放 procurementAgencyServiceFeeAmount；若中标商家列是“流标状态/流标/废标/采购失败”，outcomeType 用 Failed 且金额字段必须为 null。evidenceText 保留原文证据。projectName 仅在原文明确给出项目名称时填写；lotNo 仅在原文明确给出分标编号/标段编号时填写，否则空字符串。
""";
        }

        if (ContainsAny(signal, "ProcurementAnnouncement", "TenderAnnouncement", "采购公告", "招标公告"))
        {
            return """
  - 公告类型为 ProcurementAnnouncement 或 TenderAnnouncement（采购/招标公告）时：通常没有中标/候选厂家。除非原文明确包含中标、成交或候选厂家行，否则返回空 records 数组。
""";
        }

        return """
  - 其他或未知公告类型：只在原文明确出现公开中标/成交/候选厂家行时返回记录，否则返回空 records 数组。
""";
    }

    private static int ResolveSourceMaxCharacters(int maxInputCharacters, string? reviewerPrompt)
    {
        var ceiling = string.IsNullOrWhiteSpace(reviewerPrompt)
            ? DefaultSourceMaxCharacters
            : ReviewerPromptSourceMaxCharacters;
        return Math.Clamp(Math.Min(maxInputCharacters, ceiling), 4_000, ceiling);
    }

    private static string BuildSourceBundle(
        BidOpsOutcomeSupplierAiExtractionRequest request,
        int maxCharacters)
    {
        var htmlBudget = maxCharacters / 10;
        var textBudget = maxCharacters / 4;
        var attachments = request.Attachments ?? [];
        var attachmentBudget = Math.Max(0, maxCharacters - htmlBudget - textBudget);
        var builder = new StringBuilder();
        AppendSection(builder, "公告正文纯文本", ExtractOutcomeRelevantText(request.Text, textBudget));
        AppendSection(builder, "公告正文 HTML", ExtractOutcomeRelevantText(request.Html, htmlBudget));

        if (attachments.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("附件：无");
            return builder.ToString();
        }

        for (var i = 0; i < attachments.Count; i++)
        {
            var attachment = attachments[i];
            var remainingAttachments = Math.Max(1, attachments.Count - i);
            var contentBudget = attachmentBudget / remainingAttachments;
            attachmentBudget -= contentBudget;
            builder.AppendLine();
            builder.AppendLine($"附件 {i + 1}");
            builder.AppendLine($"文件名：{attachment.FileName}");
            builder.AppendLine($"文件类型：{attachment.FileType}");
            builder.AppendLine($"文件地址：{attachment.FileUrl}");
            builder.AppendLine($"文件大小：{attachment.FileSize?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}");
            AppendSection(builder, "附件提取文本", ExtractOutcomeRelevantText(attachment.Text, contentBudget));
        }

        return builder.ToString();
    }

    private static string ExtractOutcomeRelevantText(string value, int maxCharacters)
    {
        if (maxCharacters <= 0 || string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed.Length <= maxCharacters)
            return trimmed;

        var lines = SplitSourceLines(trimmed);
        if (lines.Count == 0)
            return Truncate(trimmed, maxCharacters);

        var keep = new bool[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            if (!IsOutcomeRelevantLine(lines[i]))
                continue;

            var start = Math.Max(0, i - SourceLineContextWindow);
            var end = Math.Min(lines.Count - 1, i + SourceLineContextWindow);
            for (var j = start; j <= end; j++)
                keep[j] = true;
        }

        var builder = new StringBuilder(maxCharacters);
        for (var i = 0; i < lines.Count && builder.Length < maxCharacters; i++)
        {
            if (!keep[i])
                continue;

            AppendBudgetedLine(builder, lines[i], maxCharacters);
        }

        if (builder.Length == 0)
            return Truncate(trimmed, maxCharacters);

        var relevantText = builder.ToString().Trim();
        if (relevantText.Length < Math.Min(1000, maxCharacters / 2))
        {
            var prefixBudget = maxCharacters - relevantText.Length - 1;
            if (prefixBudget > 0)
                relevantText = $"{Truncate(trimmed, prefixBudget)}{Environment.NewLine}{relevantText}";
        }

        return Truncate(relevantText, maxCharacters);
    }

    private static IReadOnlyList<string> SplitSourceLines(string value)
    {
        return Regex
            .Split(
                value,
                @"\r?\n|<\s*/?\s*(?:tr|td|th|p|br|li|table)\b[^>]*>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(line => BidOpsTextQuality.CleanExtractedValue(line))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static bool IsOutcomeRelevantLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        return ContainsAny(
                line,
                "中标",
                "成交",
                "候选",
                "推荐",
                "入围",
                "供应商",
                "投标人",
                "成交人",
                "中标人",
                "分标编号",
                "分标名称",
                "标段编号",
                "标段名称",
                "包号",
                "包名称",
                "包件",
                "项目名称",
                "采购编号",
                "报价",
                "金额",
                "服务费",
                "万元",
                "元") ||
            Regex.IsMatch(line, @"[A-Za-z0-9]{3,}(?:[-_/][A-Za-z0-9]{2,}){2,}", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(line, @"(?:包|第)\s*[A-Za-z0-9一二三四五六七八九十]+", RegexOptions.CultureInvariant);
    }

    private static void AppendBudgetedLine(StringBuilder builder, string line, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(line) || builder.Length >= maxCharacters)
            return;

        var remaining = maxCharacters - builder.Length;
        if (remaining <= 1)
            return;

        var normalized = line.Trim();
        if (normalized.Length + Environment.NewLine.Length <= remaining)
        {
            builder.AppendLine(normalized);
            return;
        }

        builder.AppendLine(normalized[..Math.Max(0, remaining - Environment.NewLine.Length)]);
    }

    private static void AppendSection(StringBuilder builder, string title, string content)
    {
        builder.AppendLine();
        builder.AppendLine($"## {title}");
        builder.AppendLine(string.IsNullOrWhiteSpace(content) ? "(empty)" : content.Trim());
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> ParseRecords(string content)
    {
        using var document = JsonDocument.Parse(StripJsonFence(content));
        var root = document.RootElement;
        var recordsElement = root;
        if (root.ValueKind == JsonValueKind.Object &&
            TryGetProperty(root, "records", out var nested))
        {
            recordsElement = nested;
        }

        if (recordsElement.ValueKind != JsonValueKind.Array)
            return [];

        var records = new List<BidOpsOutcomeSupplierExtract>();
        var extractionOrder = 0;
        foreach (var item in recordsElement.EnumerateArray())
        {
            var sourceRowText = Trim(GetString(item, "sourceRowText"), 2000);
            var supplierNameRaw = Trim(GetString(item, "supplierNameRaw"), 300);
            var supplierName = Trim(FirstMeaningful(GetString(item, "supplierName"), supplierNameRaw), 300);
            if (string.IsNullOrWhiteSpace(supplierName) || LooksLikeNonSupplierName(supplierName))
                continue;

            var evidenceText = Trim(FirstMeaningful(GetString(item, "evidenceText"), sourceRowText), 2000);
            var outcomeType = NormalizeOutcomeType(GetString(item, "outcomeType"));
            outcomeType = BidOpsOutcomeRecordPolicy.NormalizeOutcomeTypeForPersistence(
                outcomeType,
                supplierName,
                evidenceText,
                outcomeType);
            var isNonAwardOutcome = outcomeType == BidOpsOutcomeTypes.Failed;
            var rawLotNo = Trim(GetString(item, "rawLotNo"), 300);
            var rawLotName = Trim(GetString(item, "rawLotName"), 500);
            var rawPackageNo = Trim(GetString(item, "rawPackageNo"), 300);
            records.Add(new BidOpsOutcomeSupplierExtract
            {
                SourceSequenceNo = Trim(GetString(item, "sourceSequenceNo"), 64),
                SourcePageNo = GetNullableInt(item, "sourcePageNo"),
                SourceTableTitle = Trim(GetString(item, "sourceTableTitle"), 300),
                SourceRowText = string.IsNullOrWhiteSpace(sourceRowText) ? evidenceText : sourceRowText,
                SupplierName = supplierName,
                OutcomeType = outcomeType,
                Rank = GetNullableInt(item, "rank"),
                AwardAmount = isNonAwardOutcome ? null : GetAmount(item, "awardAmount"),
                ProcurementAgencyServiceFeeAmount = isNonAwardOutcome ? null : GetAmount(item, "procurementAgencyServiceFeeAmount"),
                ExtractionOrder = extractionOrder++,
                SourceType = BidOpsOutcomeSupplierExtractSourceTypes.AiOutcomeSuppliers,
                SourceParserVersion = BidOpsOutcomeSupplierExtractParserVersions.AiOutcomeSuppliers,
                ProjectName = Trim(GetString(item, "projectName"), 500),
                ProjectCode = Trim(GetString(item, "projectCode"), 128),
                BuyerName = Trim(GetString(item, "buyerName"), 300),
                LotNo = Trim(FirstMeaningful(GetString(item, "lotNo"), rawLotNo), 128),
                RawLotNo = rawLotNo,
                RawLotName = rawLotName,
                LotName = Trim(FirstMeaningful(GetString(item, "lotName"), rawLotName), 300),
                RawPackageNo = rawPackageNo,
                PackageNo = RestorePackagePrefix(Trim(FirstMeaningful(GetString(item, "packageNo"), rawPackageNo), 128), evidenceText),
                PackageName = Trim(GetString(item, "packageName"), 500),
                Category = Trim(GetString(item, "category"), 128),
                EvidenceText = evidenceText,
                FieldEvidence = GetStringDictionary(item, "fieldEvidence"),
                Warnings = GetStringArray(item, "warnings"),
                Confidence = ClampConfidence(GetDecimal(item, "confidence") ?? 0.72m)
            });
        }

        return records;
    }

    private static string FirstMeaningful(params string?[] values)
    {
        foreach (var value in values)
        {
            var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
            if (!string.IsNullOrWhiteSpace(cleaned))
                return cleaned;
        }

        return string.Empty;
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

        var raw = value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText().Trim('"');
        return BidOpsTextQuality.CleanExtractedValue(raw);
    }

    private static Dictionary<string, string> GetStringDictionary(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            var text = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.GetRawText().Trim('"');
            result[property.Name] = Trim(BidOpsTextQuality.CleanExtractedValue(text), 500);
        }

        return result;
    }

    private static List<string> GetStringArray(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind != JsonValueKind.Array)
            return [];

        return value
            .EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.GetRawText().Trim('"'))
            .Select(item => Trim(BidOpsTextQuality.CleanExtractedValue(item), 300))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static decimal? GetDecimal(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number;

        return decimal.TryParse(GetString(element, name), NumberStyles.Number, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static decimal? GetAmount(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return Math.Round(number, 2);

        var text = GetString(element, name);
        if (string.IsNullOrWhiteSpace(text) || ContainsAny(text, "%", "％", "折", "折扣", "费率"))
            return null;

        var match = AmountNumberRegex.Match(text.Replace(",", string.Empty, StringComparison.Ordinal));
        if (!match.Success ||
            !decimal.TryParse(match.Groups["amount"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        if (ContainsAny(text, "万元", "万"))
            amount *= 10_000m;

        return Math.Round(amount, 2);
    }

    private static int? GetNullableInt(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;

        return int.TryParse(GetString(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static string NormalizeOutcomeType(string value)
    {
        return value.Trim() switch
        {
            BidOpsOutcomeTypes.Awarded => BidOpsOutcomeTypes.Awarded,
            BidOpsOutcomeTypes.Shortlisted => BidOpsOutcomeTypes.Shortlisted,
            BidOpsOutcomeTypes.Failed => BidOpsOutcomeTypes.Failed,
            "中标" or "成交" or "中选" or "Award" or "Winner" => BidOpsOutcomeTypes.Awarded,
            "入围" or "Shortlist" => BidOpsOutcomeTypes.Shortlisted,
            "流标" or "流标状态" or "废标" or "采购失败" or "招标失败" or "成交失败" or "中标失败" => BidOpsOutcomeTypes.Failed,
            _ => BidOpsOutcomeTypes.Candidate
        };
    }

    private static string RestorePackagePrefix(string packageNo, string evidenceText)
    {
        var cleaned = Trim(packageNo, 128);
        if (string.IsNullOrWhiteSpace(cleaned) || ContainsAny(cleaned, "包", "Package", "PKG"))
            return cleaned;

        var compactEvidence = new string(evidenceText
            .Where(x => !char.IsWhiteSpace(x))
            .ToArray());
        if (compactEvidence.Contains($"包{cleaned}", StringComparison.OrdinalIgnoreCase))
            return $"包{cleaned}";

        if (compactEvidence.Contains($"第{cleaned}包", StringComparison.OrdinalIgnoreCase))
            return $"第{cleaned}包";

        return cleaned;
    }

    private static bool LooksLikeNonSupplierName(string name)
    {
        return ContainsAny(
            name,
            "招标代理",
            "采购代理",
            "代理机构",
            "招标人",
            "采购人",
            "联系人",
            "联系电话",
            "开户银行",
            "银行账号",
            "服务费",
            "代理费");
    }

    private static decimal ClampConfidence(decimal value)
    {
        return Math.Clamp(value, 0m, 1m);
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string Trim(string value, int maxLength)
    {
        var trimmed = BidOpsTextQuality.CleanExtractedValue(value);
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
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

    private sealed record BidOpsEffectiveAiRuntimeSettings(
        string Provider,
        string CodexCliModel,
        string CodexCliReasoningEffort,
        string HttpApiKey);
}
