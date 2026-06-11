using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Ai;

public sealed class BidOpsStructuredExtractionService : IBidOpsAiExtractionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BidOpsStructuredExtractionService> _logger;

    public BidOpsStructuredExtractionService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BidOpsStructuredExtractionService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BidOpsNoticeExtract> ExtractAsync(
        string title,
        string text,
        CancellationToken cancellationToken = default)
    {
        var deterministic = BidOpsDeterministicNoticeParser.Extract(title, text);
        if (!IsExternalAiEnabled())
            return deterministic;

        try
        {
            return await ExtractWithOpenAiCompatibleAsync(title, text, deterministic, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "BidOps external structured AI extraction failed; deterministic extraction was used.");
            return deterministic;
        }
    }

    private bool IsExternalAiEnabled()
    {
        var provider = _configuration["BidOps:Ai:Provider"];
        return _configuration.GetValue<bool>("BidOps:Ai:Enabled") &&
               string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(_configuration["BidOps:Ai:Endpoint"]) &&
               !string.IsNullOrWhiteSpace(_configuration["BidOps:Ai:ApiKey"]) &&
               !string.IsNullOrWhiteSpace(_configuration["BidOps:Ai:Model"]);
    }

    private async Task<BidOpsNoticeExtract> ExtractWithOpenAiCompatibleAsync(
        string title,
        string text,
        BidOpsNoticeExtract fallback,
        CancellationToken ct)
    {
        var endpoint = _configuration["BidOps:Ai:Endpoint"]!.Trim();
        var apiKey = _configuration["BidOps:Ai:ApiKey"]!.Trim();
        var model = _configuration["BidOps:Ai:Model"]!.Trim();
        var maxInputCharacters = Math.Clamp(
            _configuration.GetValue<int?>("BidOps:Ai:MaxInputCharacters") ?? 24_000,
            4_000,
            80_000);

        var prompt = BuildPrompt(title, Truncate(text, maxInputCharacters), fallback);
        var requestBody = new
        {
            model,
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You extract public procurement notices into strict JSON. Do not invent values. Use empty strings or null when unknown."
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var responseText = await response.Content.ReadAsStringAsync(ct);
        var content = ExtractAssistantContent(responseText);
        var extracted = ParseAiJson(content, fallback);
        return EnsureUsable(extracted, fallback);
    }

    private static string BuildPrompt(
        string title,
        string text,
        BidOpsNoticeExtract fallback)
    {
        var fallbackJson = JsonSerializer.Serialize(fallback, JsonOptions);
        return $$"""
Return one JSON object with this exact shape:
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

Rules:
- Use only facts present in the public notice text.
- Keep original Chinese names and requirement text.
- Do not invent buyer, agency, budget, package, or dates.
- If package data is absent, return one package with packageNo "UNSPECIFIED" and packageName equal to projectName.
- Include qualification, deadline, bid document, warranty, performance, and rejection-risk requirements when present.
- Fallback deterministic extraction for reference: {{fallbackJson}}

Notice title:
{{title}}

Notice text:
{{text}}
""";
    }

    private static string ExtractAssistantContent(string responseText)
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

        return responseText;
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

                var packageName = GetString(item, "packageName");
                packages.Add(new BidOpsPackageExtract(
                    Trim(EmptyToDefault(GetString(item, "lotNo"), "UNSPECIFIED"), 128),
                    Trim(EmptyToDefault(GetString(item, "lotName"), "未分标段"), 300),
                    Trim(EmptyToDefault(GetString(item, "packageNo"), "UNSPECIFIED"), 128),
                    Trim(EmptyToDefault(packageName, GetString(root, "projectName")), 500),
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
        return TryGetProperty(element, name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() ?? string.Empty : value.GetRawText().Trim('"')
            : string.Empty;
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
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
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
        var trimmed = value.Trim();
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

        var requirements = ExtractRequirements(text, signupDeadline, bidDeadline, openBidTime);
        var package = new BidOpsPackageExtract(
            LotNo: FirstNonEmpty(values.First("LOT_NO", "BID_SECTION_NO", "SECTION_NO"), "UNSPECIFIED"),
            LotName: FirstNonEmpty(values.First("LOT_NAME", "BID_SECTION_NAME", "SECTION_NAME"), "未分标段"),
            PackageNo: FirstNonEmpty(values.First("PACKAGE_NO", "PKG_NO", "BID_PACKAGE_NO"), "UNSPECIFIED"),
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

        var normalized = value
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
        var match = ProjectCodeRegex().Match(text);
        return match.Success ? match.Groups["code"].Value.Trim() : string.Empty;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
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
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    [GeneratedRegex("[。；;\\r\\n]+", RegexOptions.Compiled)]
    private static partial Regex SentenceSeparatorRegex();

    [GeneratedRegex("(预算|最高限价|控制价|估算)[^\\d]{0,12}(?<amount>\\d+(?:\\.\\d+)?\\s*(?:万)?元?)", RegexOptions.IgnoreCase)]
    private static partial Regex AmountRegex();

    [GeneratedRegex("\\d+(?:\\.\\d+)?")]
    private static partial Regex NumberRegex();

    [GeneratedRegex("(?:项目编号|招标编号|采购编号|分标编号|PURPRJ_CODE|ProjectCode)[:：\\s]+(?<code>[A-Za-z0-9_.\\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectCodeRegex();

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
                var value = line[(separator + 1)..].Trim();
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
