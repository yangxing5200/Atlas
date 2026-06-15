using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Ai;

public sealed class BidOpsOutcomeSupplierAiExtractionService : IBidOpsOutcomeSupplierAiExtractionService
{
    private const int MaxReferenceExtracts = 80;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex AmountNumberRegex = new(
        @"(?<amount>[0-9]+(?:,[0-9]{3})*(?:\.[0-9]+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BidOpsOutcomeSupplierAiExtractionService> _logger;

    public BidOpsOutcomeSupplierAiExtractionService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BidOpsOutcomeSupplierAiExtractionService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<BidOpsOutcomeSupplierExtract>> ExtractAsync(
        BidOpsOutcomeSupplierAiExtractionRequest request,
        CancellationToken ct = default)
    {
        if (!BidOpsAiHttpSettingsFactory.TryCreate(_configuration, BidOpsAiUse.OutcomeSuppliers, out var settings))
            return [];

        if (string.IsNullOrWhiteSpace(request.Text) &&
            string.IsNullOrWhiteSpace(request.Html) &&
            (request.Attachments == null || request.Attachments.Count == 0))
        {
            return [];
        }

        try
        {
            var prompt = BuildPrompt(request, settings.MaxInputCharacters);
            var requestBody = new
            {
                model = settings.Model,
                temperature = 0,
                max_tokens = settings.MaxOutputTokens,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "你负责从公开中文采购中标/成交结果公告、候选人公示中提取结构化 JSON。只返回 JSON，不要编造公告中没有的事实，不要推断非公开信息。"
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };
            var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);
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

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

            using var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseText = await response.Content.ReadAsStringAsync(ct);
            stopwatch.Stop();
            _logger.LogInformation(
                "BidOps outcome supplier AI raw DeepSeek response. statusCode={StatusCode}, responseBody={ResponseBody}",
                (int)response.StatusCode,
                responseText);
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

            var content = ExtractAssistantContent(responseText);
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

    private static string BuildPrompt(
        BidOpsOutcomeSupplierAiExtractionRequest request,
        int maxInputCharacters)
    {
        var deterministicJson = JsonSerializer.Serialize(
            request.DeterministicExtracts.Take(MaxReferenceExtracts),
            JsonOptions);
        var reviewerPrompt = string.IsNullOrWhiteSpace(request.ReviewerPrompt)
            ? "审核人员没有提供额外修正提示。"
            : request.ReviewerPrompt.Trim();
        var sourceBundle = BuildSourceBundle(request);
        var expectedFields = BuildExpectedFields(request.NoticeType, request.Title);

        return $$"""
请只返回一个 JSON 对象，结构必须严格符合下面的形状：
{
  "records": [
    {
      "supplierName": "",
      "outcomeType": "Awarded|Candidate|Shortlisted",
      "rank": null,
      "awardAmount": null,
      "procurementAgencyServiceFeeAmount": null,
      "projectName": "",
      "projectCode": "",
      "buyerName": "",
      "lotNo": "",
      "lotName": "",
      "packageNo": "",
      "packageName": "",
      "category": "",
      "evidenceText": "",
      "confidence": 0.0
    }
  ]
}

规则：
- 只提取公开公告中的中标/成交结果、入围、推荐候选人或成交候选人厂家明细。
- 中文组织名称必须保持公告原文写法，不要改写、翻译或补全。
- 必填/重点字段取决于公告类型：
{{expectedFields}}
- 推荐候选人公示必须尽量提取：采购编号 -> projectCode，分标编号 -> lotNo，分标名称 -> lotName，包号 -> packageNo，包名称 -> packageName，排名 -> rank，推荐的成交候选人/推荐中标候选人 -> supplierName，公开的最终报价 -> awardAmount。
- 中标/成交结果公告必须尽量提取：采购编号 -> projectCode，分标编号 -> lotNo，分标名称 -> lotName，包号 -> packageNo；中标/成交/成交供应商行的 outcomeType 必须为 Awarded，成交供应商/中标人 -> supplierName。
- 如果表格行里只有包号和厂家，但正文公共部分明确写了采购编号、采购方、分标编号或分标名称，可以从正文公共部分继承这些字段。
- 不要把“采购编号/采购项目编号”和“分标编号”混淆。采购编号/项目编号放 projectCode，分标编号/标段编号放 lotNo。
- 不要把“分标名称”和“分标编号”混淆。名称放 lotName/packageName，编号放 lotNo/packageNo。
- packageNo 必须保留原文写法，包括“包”“第...包”等前缀。原文是“包1”时不要返回裸数字“1”。
- 除非附件文件名里的值也出现在正文或附件内容里，否则不要把附件文件名前缀当成事实。
- 流标、废标、失败行不要作为中标/候选明细返回，除非该行明确给出了中标或推荐厂家。
- 采购方、代理机构、联系人、银行账户、服务费收取单位、投诉受理单位都不是厂家。
- “采购代理服务费”“代理服务费”“服务费金额”放到 procurementAgencyServiceFeeAmount，不能放到 awardAmount。
- 金额如果单位是“万元”，返回 JSON 前必须换算为“元”。未知金额、折扣率、费率、百分比返回 null。
- evidenceText 必须是公告正文或附件文本中的简短原文片段。候选人公示优先包含公开的评审情况、排名或推荐上下文。
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
{{Truncate(sourceBundle, maxInputCharacters)}}
""";
    }

    private static string FormatEndpointForLog(string endpoint)
    {
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            ? $"{uri.Host}{uri.AbsolutePath}"
            : endpoint;
    }

    private static string BuildExpectedFields(string noticeType, string title)
    {
        var signal = $"{noticeType} {title}";
        if (ContainsAny(signal, "CandidateAnnouncement", "中标候选人", "成交候选人", "推荐"))
        {
            return """
  - 公告类型为 CandidateAnnouncement（推荐候选人公示）时：每一行候选人返回一条记录；需要尽量包含 projectCode、lotNo、lotName、packageNo、packageName、rank、supplierName；公开最终报价放 awardAmount；outcomeType 用 Candidate；evidenceText 包含公开评审情况、排名或推荐上下文。
""";
        }

        if (ContainsAny(signal, "AwardAnnouncement", "ResultAnnouncement", "中标结果", "成交结果", "结果公告"))
        {
            return """
  - 公告类型为 AwardAnnouncement 或 ResultAnnouncement（中标/成交结果公告）时：每一行中标/成交厂家返回一条记录；需要尽量包含 projectCode、lotNo、lotName、packageNo、packageName、supplierName；outcomeType 用 Awarded；公开成交金额放 awardAmount；公开代理服务费放 procurementAgencyServiceFeeAmount；evidenceText 保留原文证据。
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

    private static string BuildSourceBundle(BidOpsOutcomeSupplierAiExtractionRequest request)
    {
        var builder = new StringBuilder();
        AppendSection(builder, "公告正文 HTML", request.Html);
        AppendSection(builder, "公告正文纯文本", request.Text);

        var attachments = request.Attachments ?? [];
        if (attachments.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("附件：无");
            return builder.ToString();
        }

        for (var i = 0; i < attachments.Count; i++)
        {
            var attachment = attachments[i];
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
        foreach (var item in recordsElement.EnumerateArray())
        {
            var supplierName = Trim(GetString(item, "supplierName"), 300);
            if (string.IsNullOrWhiteSpace(supplierName) || LooksLikeNonSupplierName(supplierName))
                continue;

            var evidenceText = Trim(GetString(item, "evidenceText"), 2000);
            records.Add(new BidOpsOutcomeSupplierExtract
            {
                SupplierName = supplierName,
                OutcomeType = NormalizeOutcomeType(GetString(item, "outcomeType")),
                Rank = GetNullableInt(item, "rank"),
                AwardAmount = GetAmount(item, "awardAmount"),
                ProcurementAgencyServiceFeeAmount = GetAmount(item, "procurementAgencyServiceFeeAmount"),
                ProjectName = Trim(GetString(item, "projectName"), 500),
                ProjectCode = Trim(GetString(item, "projectCode"), 128),
                BuyerName = Trim(GetString(item, "buyerName"), 300),
                LotNo = Trim(GetString(item, "lotNo"), 128),
                LotName = Trim(GetString(item, "lotName"), 300),
                PackageNo = RestorePackagePrefix(Trim(GetString(item, "packageNo"), 128), evidenceText),
                PackageName = Trim(GetString(item, "packageName"), 500),
                Category = Trim(GetString(item, "category"), 128),
                EvidenceText = evidenceText,
                Confidence = ClampConfidence(GetDecimal(item, "confidence") ?? 0.72m)
            });
        }

        return records;
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
            "中标" or "成交" or "中选" or "Award" or "Winner" => BidOpsOutcomeTypes.Awarded,
            "入围" or "Shortlist" => BidOpsOutcomeTypes.Shortlisted,
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
}
