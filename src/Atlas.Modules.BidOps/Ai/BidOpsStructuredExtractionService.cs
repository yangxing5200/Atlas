using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Modules.BidOps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Ai;

public sealed class BidOpsStructuredExtractionService : IBidOpsAiExtractionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IBidOpsAiCallDiagnostics _diagnostics;
    private readonly ILogger<BidOpsStructuredExtractionService> _logger;

    public BidOpsStructuredExtractionService(
        HttpClient httpClient,
        IConfiguration configuration,
        IBidOpsAiCallDiagnostics diagnostics,
        ILogger<BidOpsStructuredExtractionService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BidOpsNoticeExtract> ExtractAsync(
        string title,
        string text,
        CancellationToken cancellationToken = default)
    {
        return await ExtractAsync(
            new BidOpsNoticeAiExtractionRequest(title, string.Empty, string.Empty, null, text, string.Empty, []),
            cancellationToken);
    }

    public async Task<BidOpsNoticeExtract> ExtractAsync(
        BidOpsNoticeAiExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        var sourceText = BuildDeterministicSourceText(request);
        var deterministic = BidOpsDeterministicNoticeParser.Extract(request.Title, sourceText);
        if (!BidOpsAiHttpSettingsFactory.TryCreate(_configuration, BidOpsAiUse.NoticeStaging, out var settings))
        {
            LogUnavailableSettings();
            return deterministic;
        }

        try
        {
            return await ExtractWithOpenAiCompatibleAsync(request, deterministic, settings, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "BidOps external structured AI extraction failed; deterministic extraction was used.");
            return deterministic;
        }
    }

    private void LogUnavailableSettings()
    {
        var diagnostics = BidOpsAiHttpSettingsFactory.Diagnose(_configuration, BidOpsAiUse.NoticeStaging);
        var level = diagnostics.Enabled && diagnostics.UseEnabled ? LogLevel.Warning : LogLevel.Debug;
        _logger.Log(
            level,
            "BidOps structured AI request skipped because AI HTTP settings are unavailable. enabled={Enabled}, useEnabled={UseEnabled}, provider={Provider}, supportedProvider={SupportedProvider}, apiKeySource={ApiKeySource}, hasApiKey={HasApiKey}, hasModel={HasModel}, hasEndpoint={HasEndpoint}.",
            diagnostics.Enabled,
            diagnostics.UseEnabled,
            diagnostics.Provider,
            diagnostics.SupportedProvider,
            diagnostics.ApiKeySource,
            diagnostics.HasApiKey,
            diagnostics.HasModel,
            diagnostics.HasEndpoint);
    }

    private async Task<BidOpsNoticeExtract> ExtractWithOpenAiCompatibleAsync(
        BidOpsNoticeAiExtractionRequest request,
        BidOpsNoticeExtract fallback,
        BidOpsAiHttpSettings settings,
        CancellationToken ct)
    {
        var prompt = BuildPrompt(request, Truncate(BuildSourceBundle(request), settings.MaxInputCharacters), fallback);
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
                    content = "你负责从公开招投标/采购公告中提取结构化 JSON。必须只返回一个 JSON 对象：第一个字符必须是 {，最后一个字符必须是 }。不要输出推理过程、分析、解释、摘要、Markdown、代码块或任何 JSON 之外的文字。不要编造字段；无法从公告或附件确认的内容用空字符串或 null。"
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
            "BidOps structured AI request started. provider={Provider}, model={Model}, endpoint={Endpoint}, noticeTypeHint={NoticeTypeHint}, titleLength={TitleLength}, promptChars={PromptChars}, attachmentCount={AttachmentCount}.",
            settings.Provider,
            settings.Model,
            FormatEndpointForLog(settings.Endpoint),
            request.NoticeType,
            request.Title.Length,
            prompt.Length,
            request.Attachments.Count);
        _logger.LogInformation(
            "BidOps structured AI request body before DeepSeek call. requestBody={RequestBody}",
            requestJson);

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
            BidOpsAiUse.NoticeStaging.ToString(),
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
        _logger.LogInformation(
            "BidOps structured AI raw DeepSeek response. statusCode={StatusCode}, responseBody={ResponseBody}",
            (int)response.StatusCode,
            BidOpsAiJsonLogging.FormatJsonForLog(responseText));
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "BidOps structured AI request failed. provider={Provider}, model={Model}, endpoint={Endpoint}, statusCode={StatusCode}, elapsedMs={ElapsedMs}, responseChars={ResponseChars}.",
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
                "BidOps structured AI response had empty assistant content; deterministic extraction was used. provider={Provider}, model={Model}, endpoint={Endpoint}, statusCode={StatusCode}, elapsedMs={ElapsedMs}, responseChars={ResponseChars}, finishReason={FinishReason}.",
                settings.Provider,
                settings.Model,
                FormatEndpointForLog(settings.Endpoint),
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                responseText.Length,
                finishReason);
            return fallback;
        }

        var extracted = ParseAiJson(content, fallback);
        var usable = EnsureUsable(extracted, fallback);
        _logger.LogInformation(
            "BidOps structured AI request completed. provider={Provider}, model={Model}, endpoint={Endpoint}, statusCode={StatusCode}, elapsedMs={ElapsedMs}, responseChars={ResponseChars}, assistantChars={AssistantChars}, noticeType={NoticeType}, packageCount={PackageCount}, requirementCount={RequirementCount}.",
            settings.Provider,
            settings.Model,
            FormatEndpointForLog(settings.Endpoint),
            (int)response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            responseText.Length,
            content.Length,
            usable.NoticeType,
            usable.Packages.Count,
            usable.Packages.Sum(x => x.Requirements.Count));
        return usable;
    }

    private static string BuildPrompt(
        BidOpsNoticeAiExtractionRequest request,
        string sourceBundle,
        BidOpsNoticeExtract fallback)
    {
        var fallbackJson = JsonSerializer.Serialize(fallback, JsonOptions);
        return $$"""
输出限制：
- 只返回一个 JSON 对象；第一个字符必须是 {，最后一个字符必须是 }。
- 不要输出推理过程、分析、解释、摘要、Markdown、代码块、前缀或后缀文字。
- JSON 结构必须严格符合下面的形状：
{
  "noticeType": "TenderAnnouncement|ProcurementAnnouncement|CandidateAnnouncement|AwardAnnouncement|ChangeAnnouncement|Other",
  "projectName": "",
  "projectCode": "",
  "buyerName": "",
  "agencyName": "",
  "region": "",
  "budgetAmount": null,
  "publishTime": null,
  "signupDeadline": null,
  "bidDeadline": null,
  "openBidTime": null,
  "confidence": 0.0,
  "packages": [
    {
      "lotNo": "",
      "lotName": "",
      "packageNo": "",
      "packageName": "",
      "category": "Goods|Service|Construction|Other",
      "quantity": null,
      "unit": "",
      "budgetAmount": null,
      "maxPrice": null,
      "deliveryPlace": "",
      "deliveryPeriod": "",
      "confidence": 0.0,
      "requirements": [
        {
          "requirementType": "",
          "originalText": "",
          "sourcePage": null,
          "isMandatory": true,
          "isRejectRisk": false,
          "requiredEvidenceType": "",
          "riskLevel": "Low|Medium|High",
          "aiExplanation": "",
          "confidence": 0.0
        }
      ]
    }
  ]
}

规则：
- 只能使用公开公告正文、公告 HTML、附件提取文本中明确出现的事实。
- 中文项目名、采购方、代理机构、分标/包件名称、资格要求等必须保持原文写法。
- 不要编造采购方、代理机构、预算、包件、日期或资格要求；无法确认时用空字符串或 null。
- 如果没有明确包件表格，返回一个包件：packageNo 为空，packageName 使用 projectName。
- 如公告中出现资格、截止时间、标书、质保、履约、否决/废标风险等要求，需要写入 requirements。
- 根据公告类型、标题和内容判断字段重点：
  - TenderAnnouncement 或 ProcurementAnnouncement：提取公告级项目、采购方、代理机构、时间字段，以及包件和要求。
  - CandidateAnnouncement：提取公告级字段和可见包件字段；候选厂家明细由中标/候选明细解析器处理，不要把厂家行编造成 requirements。
  - AwardAnnouncement 或 ResultAnnouncement：提取公告级 projectCode、buyerName、agencyName、publishTime，以及可见的分标/包件身份字段；中标厂家行由中标/候选明细解析器处理。
  - CorrectionAnnouncement 或 ChangeAnnouncement：只提取正文明确变更的公告级字段，以及受影响的包件/截止时间字段。
- 公告正文 HTML 对 Word/HTML 表格（例如 MsoNormalTable）优先级最高。包件或资格要求在 PDF/Word/Excel/ZIP 附件中时，使用附件提取文本。
- 下面是规则解析参考结果，只作为参考；如果公开原文更准确，以公开原文为准：{{fallbackJson}}

公告标题：
{{request.Title}}

公告元数据：
公告类型提示：{{request.NoticeType}}
发布时间：{{request.PublishTime?.ToString("O", CultureInfo.InvariantCulture) ?? ""}}

公开来源材料：
{{sourceBundle}}
""";
    }

    private static string FormatEndpointForLog(string endpoint)
    {
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            ? $"{uri.Host}{uri.AbsolutePath}"
            : endpoint;
    }

    private static string BuildDeterministicSourceText(BidOpsNoticeAiExtractionRequest request)
    {
        var builder = new StringBuilder();
        AppendSection(builder, "Announcement Text", request.Text);
        AppendSection(builder, "Announcement HTML", request.Html);
        foreach (var attachment in request.Attachments)
        {
            AppendSection(builder, $"Attachment: {attachment.FileName}", attachment.Text);
        }

        return builder.ToString();
    }

    private static string BuildSourceBundle(BidOpsNoticeAiExtractionRequest request)
    {
        var builder = new StringBuilder();
        AppendSection(builder, "公告正文 HTML", request.Html);
        AppendSection(builder, "公告正文纯文本", request.Text);

        if (request.Attachments.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("附件：无");
            return builder.ToString();
        }

        for (var i = 0; i < request.Attachments.Count; i++)
        {
            var attachment = request.Attachments[i];
            builder.AppendLine();
            builder.AppendLine($"附件 {i + 1}");
            builder.AppendLine($"文件名：{attachment.FileName}");
            builder.AppendLine($"文件类型：{attachment.FileType}");
            builder.AppendLine($"文件地址：{attachment.FileUrl}");
            builder.AppendLine($"文件大小：{attachment.FileSize?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}");
            AppendSection(builder, "附件提取文本", attachment.Text);
        }

        return builder.ToString();
    }

    private static void AppendSection(StringBuilder builder, string title, string content)
    {
        builder.AppendLine();
        builder.AppendLine($"## {title}");
        builder.AppendLine(string.IsNullOrWhiteSpace(content) ? "(empty)" : content.Trim());
    }

    private static BidOpsNoticeExtract ParseAiJson(
        string json,
        BidOpsNoticeExtract fallback)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var packages = new List<BidOpsPackageExtract>();
        if (TryGetProperty(root, "packages", out var packagesElement) &&
            packagesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in packagesElement.EnumerateArray())
            {
                var requirements = new List<BidOpsRequirementExtract>();
                if (TryGetProperty(item, "requirements", out var reqs) &&
                    reqs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var req in reqs.EnumerateArray())
                    {
                        var originalText = GetString(req, "originalText");
                        if (string.IsNullOrWhiteSpace(originalText))
                            continue;

                        requirements.Add(new BidOpsRequirementExtract(
                            Trim(GetString(req, "requirementType"), 128),
                            Trim(originalText, 2000),
                            GetNullableInt(req, "sourcePage"),
                            GetBool(req, "isMandatory"),
                            GetBool(req, "isRejectRisk"),
                            Trim(GetString(req, "requiredEvidenceType"), 128),
                            NormalizeRisk(GetString(req, "riskLevel")),
                            Trim(GetString(req, "aiExplanation"), 1000),
                            ClampConfidence(GetDecimal(req, "confidence") ?? 0.7m)));
                    }
                }

                var rootProjectName = FirstNonEmpty(GetString(root, "projectName"), fallback.ProjectName);
                var packageName = GetString(item, "packageName");
                packages.Add(new BidOpsPackageExtract(
                    Trim(GetString(item, "lotNo"), 128),
                    Trim(EmptyToDefault(GetString(item, "lotName"), "未分标段"), 300),
                    Trim(GetString(item, "packageNo"), 128),
                    Trim(EmptyToDefault(packageName, rootProjectName), 500),
                    NormalizeCategory(GetString(item, "category")),
                    GetDecimal(item, "quantity"),
                    Trim(GetString(item, "unit"), 64),
                    GetDecimal(item, "budgetAmount"),
                    GetDecimal(item, "maxPrice"),
                    Trim(GetString(item, "deliveryPlace"), 300),
                    Trim(GetString(item, "deliveryPeriod"), 200),
                    ClampConfidence(GetDecimal(item, "confidence") ?? 0.7m),
                    requirements));
            }
        }

        if (packages.Count == 0)
            packages.Add(fallback.Packages.First());

        return new BidOpsNoticeExtract(
            Trim(EmptyToDefault(GetString(root, "noticeType"), fallback.NoticeType), 64),
            Trim(EmptyToDefault(GetString(root, "projectName"), fallback.ProjectName), 500),
            Trim(GetString(root, "projectCode"), 128),
            Trim(GetString(root, "buyerName"), 300),
            Trim(GetString(root, "agencyName"), 300),
            Trim(GetString(root, "region"), 128),
            GetDecimal(root, "budgetAmount"),
            GetDate(root, "publishTime"),
            GetDate(root, "signupDeadline"),
            GetDate(root, "bidDeadline"),
            GetDate(root, "openBidTime"),
            ClampConfidence(GetDecimal(root, "confidence") ?? 0.75m),
            packages);
    }

    private static BidOpsNoticeExtract EnsureUsable(
        BidOpsNoticeExtract extract,
        BidOpsNoticeExtract fallback)
    {
        if (string.IsNullOrWhiteSpace(extract.ProjectName))
            return fallback;

        if (extract.Packages.Count == 0)
        {
            return extract with
            {
                Packages = fallback.Packages
            };
        }

        return extract;
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
        var raw = TryGetProperty(element, name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() ?? string.Empty : value.GetRawText().Trim('"')
            : string.Empty;
        return BidOpsTextQuality.CleanExtractedValue(raw);
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

    private static bool GetBool(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => value.TryGetInt32(out var number) && number != 0,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    private static DateTime? GetDate(JsonElement element, string name)
    {
        var text = GetString(element, name);
        return BidOpsDeterministicNoticeParser.TryParseDate(text);
    }

    private static string EmptyToDefault(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? BidOpsTextQuality.CleanExtractedValue(fallback)
            : BidOpsTextQuality.CleanExtractedValue(value);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
            if (!string.IsNullOrWhiteSpace(cleaned))
                return cleaned;
        }

        return string.Empty;
    }

    private static decimal ClampConfidence(decimal value)
    {
        return Math.Clamp(value, 0m, 1m);
    }

    private static string NormalizeRisk(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "high" or "高" => "High",
            "low" or "低" => "Low",
            _ => "Medium"
        };
    }

    private static string NormalizeCategory(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is "goods" or "物资" or "货物")
            return "Goods";
        if (normalized is "service" or "services" or "服务")
            return "Service";
        if (normalized is "construction" or "工程" or "施工")
            return "Construction";

        return "Other";
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
}

public static partial class BidOpsDeterministicNoticeParser
{
    private static readonly string[] ProvinceHints =
    [
        "北京", "天津", "上海", "重庆", "河北", "山西", "辽宁", "吉林", "黑龙江", "江苏", "浙江", "安徽", "福建", "江西",
        "山东", "河南", "湖北", "湖南", "广东", "海南", "四川", "贵州", "云南", "陕西", "甘肃", "青海", "内蒙古", "广西",
        "西藏", "宁夏", "新疆"
    ];

    public static BidOpsNoticeExtract Extract(string title, string text)
    {
        var values = KeyValueBag.Parse(text);
        var projectName = FirstNonEmpty(
            values.First("PURPRJ_NAME", "PROJECT_NAME", "PRJ_NAME", "NOTICE_TITLE", "TITLE"),
            title);
        var projectCode = FirstNonEmpty(
            values.First("PURPRJ_CODE", "PROJECT_CODE", "PRJ_CODE", "BID_CODE", "TENDER_NO", "CODE"),
            ExtractProjectCode(text));
        var buyerName = values.First("BID_ORG", "BUYER_NAME", "PURCHASER_NAME", "TENDEREE", "PUBLISH_ORG_NAME", "PublishOrgName");
        var agencyName = values.First("BID_AGT", "BID_AGENT", "AGENCY_NAME", "AGENT_NAME");
        var noticeType = DetectNoticeType(title, text, values.First("NOTICE_TYPE_NAME"));
        var publishTime = FirstDate(values, "PUB_TIME", "PUBLISH_TIME", "NOTICE_PUBLISH_TIME", "ListPublishTime", "PublishTime");
        var signupDeadline = FirstDate(values, "BIDBOOK_BUY_END_TIME", "BIDBOOK_SELL_END_TIME", "SIGNUP_DEADLINE", "SALE_END_TIME");
        var openBidTime = FirstDate(values, "OPENBID_TIME", "OPEN_BID_TIME", "BID_OPEN_TIME");
        var bidDeadline = FirstDate(values, "BID_DEADLINE", "BID_END_TIME", "SUBMIT_END_TIME") ?? openBidTime;
        var budget = FirstAmount(values, text, "BUDGET_AMOUNT", "BUDGET", "MAX_PRICE", "CONTROL_PRICE", "ESTIMATE_AMOUNT");
        var category = DetectCategory(text, values.First("PUR_TYPE_NAME", "CATEGORY"));
        var region = DetectRegion(title, buyerName, values.First("REGION", "PROVINCE", "PublishOrgName"));
        var ecpTablePackages = BidOpsEcpProcurementTableParser.ExtractPackages(text, category);
        if (ecpTablePackages.Count > 0)
        {
            return new BidOpsNoticeExtract(
                NoticeType: noticeType,
                ProjectName: Trim(projectName, 500),
                ProjectCode: Trim(projectCode, 128),
                BuyerName: Trim(buyerName, 300),
                AgencyName: Trim(agencyName, 300),
                Region: Trim(region, 128),
                BudgetAmount: budget,
                PublishTime: publishTime,
                SignupDeadline: signupDeadline,
                BidDeadline: bidDeadline,
                OpenBidTime: openBidTime,
                Confidence: HasCoreFields(projectCode, buyerName, agencyName) ? 0.92m : 0.86m,
                Packages: ecpTablePackages);
        }

        var lotNo = FirstNonEmpty(
            values.First("LOT_NO", "BID_SECTION_NO", "SECTION_NO", "LOTNO", "BIDSECTIONNO", "SECTIONNO", "分标编号", "分标号", "标段编号", "标段号"),
            ExtractLotNo(text));
        var packageNo = FirstNonEmpty(
            values.First("PACKAGE_NO", "PKG_NO", "BID_PACKAGE_NO", "PACKAGENO", "PKGNO", "BIDPACKAGENO", "包件编号", "包件号", "包号", "标包编号", "标包号", "分包编号", "分包号"),
            ExtractPackageNo(text));
        var requirements = ExtractRequirements(text, signupDeadline, bidDeadline, openBidTime);
        var package = new BidOpsPackageExtract(
            LotNo: lotNo,
            LotName: FirstNonEmpty(values.First("LOT_NAME", "BID_SECTION_NAME", "SECTION_NAME"), "未分标段"),
            PackageNo: packageNo,
            PackageName: projectName,
            Category: category,
            Quantity: null,
            Unit: string.Empty,
            BudgetAmount: budget,
            MaxPrice: budget,
            DeliveryPlace: values.First("DELIVERY_PLACE", "PLACE_OF_DELIVERY"),
            DeliveryPeriod: values.First("DELIVERY_PERIOD", "DELIVERY_TIME"),
            Confidence: HasCoreFields(projectCode, buyerName, agencyName) ? 0.86m : 0.72m,
            Requirements: requirements);

        return new BidOpsNoticeExtract(
            NoticeType: noticeType,
            ProjectName: Trim(projectName, 500),
            ProjectCode: Trim(projectCode, 128),
            BuyerName: Trim(buyerName, 300),
            AgencyName: Trim(agencyName, 300),
            Region: Trim(region, 128),
            BudgetAmount: budget,
            PublishTime: publishTime,
            SignupDeadline: signupDeadline,
            BidDeadline: bidDeadline,
            OpenBidTime: openBidTime,
            Confidence: HasCoreFields(projectCode, buyerName, agencyName) ? 0.88m : 0.74m,
            Packages: [package]);
    }

    public static DateTime? TryParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = BidOpsTextQuality.CleanExtractedValue(value)
            .Trim()
            .Replace('年', '-')
            .Replace('月', '-')
            .Replace("日", string.Empty)
            .Replace('/', '-');
        return DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }

    private static IReadOnlyList<BidOpsRequirementExtract> ExtractRequirements(
        string text,
        DateTime? signupDeadline,
        DateTime? bidDeadline,
        DateTime? openBidTime)
    {
        var requirements = new List<BidOpsRequirementExtract>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sentence in SplitSentences(text))
        {
            var type = DetectRequirementType(sentence);
            if (type == null)
                continue;

            AddRequirement(requirements, seen, type, sentence, InferEvidenceType(type), InferRisk(type, sentence), 0.72m);
            if (requirements.Count >= 20)
                break;
        }

        if (signupDeadline.HasValue)
        {
            AddRequirement(
                requirements,
                seen,
                "Deadline",
                $"标书购买或报名截止时间：{signupDeadline:yyyy-MM-dd HH:mm:ss}",
                "BidDocument",
                "Medium",
                0.82m);
        }

        if (bidDeadline.HasValue)
        {
            AddRequirement(
                requirements,
                seen,
                "Deadline",
                $"投标文件递交截止时间：{bidDeadline:yyyy-MM-dd HH:mm:ss}",
                "BidDocument",
                "High",
                0.84m);
        }

        if (openBidTime.HasValue)
        {
            AddRequirement(
                requirements,
                seen,
                "Deadline",
                $"开标时间：{openBidTime:yyyy-MM-dd HH:mm:ss}",
                "BidDocument",
                "High",
                0.84m);
        }

        if (requirements.Count == 0)
        {
            AddRequirement(
                requirements,
                seen,
                "Qualification",
                "资格与响应要求以公告正文及公开附件为准。",
                "QualificationDocument",
                "Medium",
                0.45m);
        }

        return requirements;
    }

    private static void AddRequirement(
        List<BidOpsRequirementExtract> requirements,
        HashSet<string> seen,
        string type,
        string originalText,
        string evidenceType,
        string riskLevel,
        decimal confidence)
    {
        var text = Trim(originalText, 2000);
        if (string.IsNullOrWhiteSpace(text) || !seen.Add(text))
            return;

        requirements.Add(new BidOpsRequirementExtract(
            type,
            text,
            null,
            IsMandatoryRequirement(type, text),
            riskLevel == "High",
            evidenceType,
            riskLevel,
            BuildExplanation(type, riskLevel),
            confidence));
    }

    private static IEnumerable<string> SplitSentences(string text)
    {
        foreach (var raw in SentenceSeparatorRegex().Split(text))
        {
            var value = raw.Trim();
            if (value.Length is < 8 or > 1000)
                continue;

            yield return value;
        }
    }

    private static string? DetectRequirementType(string sentence)
    {
        if (ContainsAny(sentence, "资格", "资质", "业绩", "信誉", "财务", "能力", "认证", "证书"))
            return "Qualification";
        if (ContainsAny(sentence, "截止", "递交", "开标", "报名", "购买", "发售"))
            return "Deadline";
        if (ContainsAny(sentence, "保证金", "担保"))
            return "Guarantee";
        if (ContainsAny(sentence, "废标", "否决", "无效", "拒收", "不得", "不接受"))
            return "RejectionRisk";
        if (ContainsAny(sentence, "交货", "交付", "服务期", "工期", "供货期"))
            return "Delivery";

        return null;
    }

    private static string InferEvidenceType(string type)
    {
        return type switch
        {
            "Qualification" => "QualificationDocument",
            "Deadline" => "BidDocument",
            "Guarantee" => "GuaranteeDocument",
            "Delivery" => "TechnicalDocument",
            "RejectionRisk" => "BidDocument",
            _ => "SupportingDocument"
        };
    }

    private static string InferRisk(string type, string sentence)
    {
        if (type is "Deadline" or "RejectionRisk")
            return "High";

        return ContainsAny(sentence, "必须", "应当", "须", "不得", "不接受", "否决", "无效")
            ? "High"
            : "Medium";
    }

    private static bool IsMandatoryRequirement(string type, string sentence)
    {
        return type is "Deadline" or "RejectionRisk" ||
               ContainsAny(sentence, "必须", "应当", "须", "不得", "不接受", "否决", "无效");
    }

    private static string BuildExplanation(string type, string riskLevel)
    {
        return riskLevel == "High"
            ? $"{type} requirement was extracted as a high review risk from public notice text."
            : $"{type} requirement was extracted from public notice text.";
    }

    private static DateTime? FirstDate(KeyValueBag values, params string[] keys)
    {
        foreach (var key in keys)
        {
            var parsed = TryParseDate(values.First(key));
            if (parsed.HasValue)
                return parsed;
        }

        return null;
    }

    private static decimal? FirstAmount(KeyValueBag values, string text, params string[] keys)
    {
        foreach (var key in keys)
        {
            var amount = TryParseAmount(values.First(key));
            if (amount.HasValue)
                return amount;
        }

        var match = AmountRegex().Match(text);
        if (!match.Success)
            return null;

        var parsed = TryParseAmount(match.Value);
        return parsed;
    }

    private static decimal? TryParseAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Replace(",", string.Empty, StringComparison.Ordinal);
        var match = NumberRegex().Match(normalized);
        if (!match.Success ||
            !decimal.TryParse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        if (normalized.Contains('万'))
            amount *= 10_000m;

        return amount;
    }

    private static string DetectNoticeType(string title, string text, string noticeTypeName)
    {
        var source = $"{title} {noticeTypeName} {text}";
        if (ContainsAny(source, "变更公告", "澄清", "延期"))
            return "ChangeAnnouncement";
        if (ContainsAny(source, "中标候选人", "成交候选人"))
            return "CandidateAnnouncement";
        if (ContainsAny(source, "中标结果", "成交结果", "结果公告"))
            return "AwardAnnouncement";
        if (ContainsAny(source, "采购公告", "竞争性谈判", "询价", "单一来源"))
            return "ProcurementAnnouncement";

        return "TenderAnnouncement";
    }

    private static string DetectCategory(string text, string purTypeName)
    {
        if (ContainsAny(purTypeName, "物资", "货物", "设备", "材料"))
            return "Goods";
        if (ContainsAny(purTypeName, "服务"))
            return "Service";
        if (ContainsAny(purTypeName, "工程", "施工"))
            return "Construction";

        var source = text;
        if (ContainsAny(source, "物资", "货物", "设备", "材料"))
            return "Goods";
        if (ContainsAny(source, "服务"))
            return "Service";
        if (ContainsAny(source, "工程", "施工"))
            return "Construction";

        return "Other";
    }

    private static string DetectRegion(string title, string buyerName, string explicitRegion)
    {
        var source = $"{explicitRegion} {buyerName} {title}";
        foreach (var province in ProvinceHints)
        {
            if (source.Contains(province, StringComparison.OrdinalIgnoreCase))
                return province;
        }

        return string.IsNullOrWhiteSpace(explicitRegion) ? "公开来源" : explicitRegion.Trim();
    }

    private static string ExtractProjectCode(string text)
    {
        foreach (var candidate in EnumerateTextSources(text))
        {
            foreach (Match match in ProjectCodeRegex().Matches(candidate))
            {
                var code = NormalizeExtractedCode(match.Groups["code"].Value);
                if (!IsInvalidProjectCode(code))
                    return code;
            }
        }

        return string.Empty;
    }

    private static string ExtractLotNo(string text)
    {
        return ExtractCode(text, LotNoRegex(), IsInvalidPackageCode);
    }

    private static string ExtractPackageNo(string text)
    {
        return ExtractCode(text, PackageNoRegex(), IsInvalidPackageCode);
    }

    private static string ExtractCode(
        string text,
        Regex regex,
        Func<string, bool> isInvalid)
    {
        foreach (var candidate in EnumerateTextSources(text))
        {
            foreach (Match match in regex.Matches(candidate))
            {
                var code = NormalizeExtractedCode(match.Groups["code"].Value);
                if (!isInvalid(code))
                    return code;
            }
        }

        return string.Empty;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
            if (!string.IsNullOrWhiteSpace(cleaned))
                return cleaned;
        }

        return string.Empty;
    }

    private static bool HasCoreFields(string projectCode, string buyerName, string agencyName)
    {
        return !string.IsNullOrWhiteSpace(projectCode) &&
               (!string.IsNullOrWhiteSpace(buyerName) || !string.IsNullOrWhiteSpace(agencyName));
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string Trim(string value, int maxLength)
    {
        var trimmed = BidOpsTextQuality.CleanExtractedValue(value);
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static IEnumerable<string> EnumerateTextSources(string text)
    {
        yield return text;

        var decoded = System.Net.WebUtility.HtmlDecode(text);
        if (!string.Equals(decoded, text, StringComparison.Ordinal))
            yield return decoded;

        var strippedWithSpaces = HtmlTagRegex().Replace(decoded, " ");
        if (!string.Equals(strippedWithSpaces, text, StringComparison.Ordinal))
            yield return strippedWithSpaces;

        var strippedWithoutSpaces = HtmlTagRegex().Replace(decoded, string.Empty);
        if (!string.Equals(strippedWithoutSpaces, text, StringComparison.Ordinal) &&
            !string.Equals(strippedWithoutSpaces, strippedWithSpaces, StringComparison.Ordinal))
        {
            yield return strippedWithoutSpaces;
        }
    }

    private static string NormalizeExtractedCode(string value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return CodeWhitespaceRegex().Replace(cleaned, string.Empty);
    }

    private static bool IsInvalidProjectCode(string value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               BidOpsTextQuality.IsUnknownMarker(value) ||
               BidOpsTextQuality.LooksUnreadablePlaceholder(value) ||
               value.Equals("ListPublishTime", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("PublishTime", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Doctype", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("MenuId", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("NoticeId", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("FirstPageDocId", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("ProjectCode", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("SourceUrl", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInvalidPackageCode(string value)
    {
        if (IsInvalidProjectCode(value))
            return true;

        return ContainsAny(value, "详见", "见附件", "附件", "公告", "正文", "无");
    }

    [GeneratedRegex("[。；;\\r\\n]+", RegexOptions.Compiled)]
    private static partial Regex SentenceSeparatorRegex();

    [GeneratedRegex("(预算|最高限价|控制价|估算)[^\\d]{0,12}(?<amount>\\d+(?:\\.\\d+)?\\s*(?:万)?元?)", RegexOptions.IgnoreCase)]
    private static partial Regex AmountRegex();

    [GeneratedRegex("\\d+(?:\\.\\d+)?")]
    private static partial Regex NumberRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("(?:项[^\\S\\r\\n]*目[^\\S\\r\\n]*编[^\\S\\r\\n]*号|招[^\\S\\r\\n]*标[^\\S\\r\\n]*编[^\\S\\r\\n]*号|采[^\\S\\r\\n]*购[^\\S\\r\\n]*编[^\\S\\r\\n]*号|分[^\\S\\r\\n]*标[^\\S\\r\\n]*编[^\\S\\r\\n]*号|PURPRJ_CODE|ProjectCode)[^\\S\\r\\n]*[:：][^\\S\\r\\n]*(?<code>[A-Za-z0-9_.\\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectCodeRegex();

    [GeneratedRegex("(?:分[^\\S\\r\\n]*标[^\\S\\r\\n]*(?:编[^\\S\\r\\n]*)?号|标[^\\S\\r\\n]*段[^\\S\\r\\n]*(?:编[^\\S\\r\\n]*)?号|LOT_NO|BID_SECTION_NO|SECTION_NO)[^\\S\\r\\n]*[:：][^\\S\\r\\n]*(?<code>[\\p{L}\\p{N}_.\\-/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex LotNoRegex();

    [GeneratedRegex("(?:包[^\\S\\r\\n]*件[^\\S\\r\\n]*(?:编[^\\S\\r\\n]*)?号|包[^\\S\\r\\n]*号|标[^\\S\\r\\n]*包[^\\S\\r\\n]*(?:编[^\\S\\r\\n]*)?号|分[^\\S\\r\\n]*包[^\\S\\r\\n]*(?:编[^\\S\\r\\n]*)?号|PACKAGE_NO|BID_PACKAGE_NO|PKG_NO)[^\\S\\r\\n]*[:：][^\\S\\r\\n]*(?<code>[\\p{L}\\p{N}_.\\-/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PackageNoRegex();

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex CodeWhitespaceRegex();

    private sealed class KeyValueBag
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public static KeyValueBag Parse(string text)
        {
            var bag = new KeyValueBag();
            foreach (var raw in text.Split(["\r\n", "\n"], StringSplitOptions.None))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue;

                var separator = FindSeparator(line);
                if (separator <= 0 || separator >= line.Length - 1)
                    continue;

                var key = line[..separator].Trim();
                var value = BidOpsTextQuality.CleanExtractedValue(line[(separator + 1)..]);
                if (key.Length == 0 || value.Length == 0)
                    continue;

                bag.Add(key, value);
                var leaf = key.Split('.', '[', ']').LastOrDefault(x => !string.IsNullOrWhiteSpace(x));
                if (!string.IsNullOrWhiteSpace(leaf))
                    bag.Add(leaf, value);
            }

            return bag;
        }

        public string First(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (_values.TryGetValue(NormalizeKey(key), out var value) && !string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private void Add(string key, string value)
        {
            _values.TryAdd(NormalizeKey(key), value);
        }

        private static int FindSeparator(string line)
        {
            var colon = line.IndexOf(':');
            var chineseColon = line.IndexOf('：');
            if (colon < 0)
                return chineseColon;
            if (chineseColon < 0)
                return colon;

            return Math.Min(colon, chineseColon);
        }

        private static string NormalizeKey(string value)
        {
            return value
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(".", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Trim()
                .ToLowerInvariant();
        }
    }
}
