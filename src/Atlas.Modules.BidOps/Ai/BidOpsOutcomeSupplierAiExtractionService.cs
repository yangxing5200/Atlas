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
                        content = "You extract public Chinese procurement award/candidate notices into strict JSON. Return JSON only. Do not infer private or unavailable facts."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

            using var response = await _httpClient.SendAsync(httpRequest, ct);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync(ct);
            var content = ExtractAssistantContent(responseText);
            return ParseRecords(content);
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
            ? "No reviewer correction prompt was provided."
            : request.ReviewerPrompt.Trim();
        var sourceBundle = BuildSourceBundle(request);
        var expectedFields = BuildExpectedFields(request.NoticeType, request.Title);

        return $$"""
Return one JSON object with this exact shape:
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

Rules:
- Extract only public result, award, shortlisted, or candidate supplier rows.
- Keep original Chinese organization names exactly as written.
- Required fields depend on the notice type:
{{expectedFields}}
- Candidate/public recommendation notices must include 采购编号 in projectCode, 分标编号 in lotNo, 分标名称 in lotName, 包号 in packageNo, 包名称 in packageName, 排名 in rank, 推荐的成交候选人/推荐中标候选人 in supplierName, and 最终报价 in awardAmount when public.
- Award/result notices must include 采购编号 in projectCode, 分标编号 in lotNo, 分标名称 in lotName, 包号 in packageNo, outcomeType Awarded for 中标/成交/成交供应商 rows, and 成交供应商/中标人 in supplierName.
- If a table row has only package number and supplier, inherit public project code, buyer, lot number, lot name, or procurement number from the surrounding body text when explicit.
- Do not confuse 采购编号/采购项目编号 with 分标编号. Use projectCode for 采购编号/项目编号 and lotNo for 分标编号/标段编号.
- Do not confuse 分标名称 with 分标编号. Put names in lotName/packageName, not lotNo/packageNo.
- Preserve packageNo exactly as written, including prefixes such as 包 or 第...包. Do not return bare 1 when the source text says 包1.
- Do not use attachment filename prefixes as facts unless the same value appears in the document content.
- Skip failed/流标/废标 rows unless a supplier is explicitly awarded or recommended.
- Buyer, agency, contact, bank account, service-fee collection, and complaint organization names are not suppliers.
- 采购代理服务费, 代理服务费, or 服务费金额 belongs in procurementAgencyServiceFeeAmount. It is not awardAmount.
- If amounts are in 万元, convert to yuan before returning JSON. If unknown or a discount/rate/percentage, use null.
- Evidence text must be a short source fragment from the public notice or attachment text. For candidate notices, prefer including the public 评审情况 or recommendation/ranking context in evidenceText.
- Prefer complete records over many uncertain fragments. If no supplier is present, return an empty records array.
- Deterministic extraction below is a reference only; correct it when it missed a PDF/table row or mixed up common body fields.
- When the reviewer correction prompt conflicts with deterministic extraction and the source text supports the correction, follow the reviewer correction prompt.
- Announcement body HTML is authoritative for inline HTML/Word tables such as MsoNormalTable. Attachments can contain the public result table; use extracted attachment text and attachment metadata to resolve rows.

Reviewer correction prompt:
{{reviewerPrompt}}

Notice metadata:
title: {{request.Title}}
noticeType: {{request.NoticeType}}
sourceUrl: {{request.SourceUrl}}
publishTime: {{request.PublishTime?.ToString("O", CultureInfo.InvariantCulture) ?? ""}}

Deterministic extraction reference:
{{deterministicJson}}

Public source materials:
{{Truncate(sourceBundle, maxInputCharacters)}}
""";
    }

    private static string BuildExpectedFields(string noticeType, string title)
    {
        var signal = $"{noticeType} {title}";
        if (ContainsAny(signal, "CandidateAnnouncement", "中标候选人", "成交候选人", "推荐"))
        {
            return """
  - CandidateAnnouncement/推荐候选人公示: return one record per candidate row with projectCode, lotNo, lotName, packageNo, packageName, rank, supplierName, awardAmount when final quote is public, outcomeType Candidate, evidenceText with public 评审情况/ranking context.
""";
        }

        if (ContainsAny(signal, "AwardAnnouncement", "ResultAnnouncement", "中标结果", "成交结果", "结果公告"))
        {
            return """
  - AwardAnnouncement/ResultAnnouncement/中标成交结果公告: return one record per awarded supplier row with projectCode, lotNo, lotName, packageNo, packageName if present, supplierName, outcomeType Awarded, awardAmount when public, procurementAgencyServiceFeeAmount when public, and evidenceText.
""";
        }

        if (ContainsAny(signal, "ProcurementAnnouncement", "TenderAnnouncement", "采购公告", "招标公告"))
        {
            return """
  - Procurement/Tender announcements normally do not have public winning suppliers. Return an empty records array unless the source explicitly contains awarded/candidate supplier rows.
""";
        }

        return """
  - Other/unknown notice type: return records only for explicit public awarded/candidate supplier rows; otherwise return an empty records array.
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
            builder.AppendLine($"fileName: {attachment.FileName}");
            builder.AppendLine($"fileType: {attachment.FileType}");
            builder.AppendLine($"fileUrl: {attachment.FileUrl}");
            builder.AppendLine($"fileSize: {attachment.FileSize?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}");
            AppendSection(builder, "extractedText", attachment.Text);
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
