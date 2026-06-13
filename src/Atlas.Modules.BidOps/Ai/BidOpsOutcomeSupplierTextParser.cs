using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Atlas.Modules.BidOps.Entities.Outcomes;

namespace Atlas.Modules.BidOps.Ai;

public sealed class BidOpsOutcomeSupplierExtract
{
    public string SupplierName { get; set; } = string.Empty;

    public string OutcomeType { get; set; } = BidOpsOutcomeTypes.Candidate;

    public int? Rank { get; set; }

    public decimal? AwardAmount { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public string ProjectCode { get; set; } = string.Empty;

    public string LotNo { get; set; } = string.Empty;

    public string LotName { get; set; } = string.Empty;

    public string PackageNo { get; set; } = string.Empty;

    public string PackageName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string EvidenceText { get; set; } = string.Empty;

    public decimal Confidence { get; set; }
}

public static class BidOpsOutcomeSupplierTextParser
{
    private static readonly Regex CompanyRegex = new(
        @"(?<name>[\u4e00-\u9fa5A-Za-z0-9（）()·\-\s]{2,90}(?:有限责任公司|股份有限公司|集团有限公司|有限公司|分公司|集团|公司|工厂|厂|勘测设计研究院|工程设计有限公司|研究院|设计院|测绘院|勘测院|勘察院|规划院|科学院|检验院|检测院|计量院|研究所|事务所|大学|学院|学校|医院|中心))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PackageCodeRegex = new(
        @"(?:包件号|包件编号|包号|标包号|标包编号|分包编号|分包号|采购包号|采购包编号|PACKAGE_NO|BID_PACKAGE_NO|PKG_NO)\s*[:：=]?\s*(?<value>[A-Za-z0-9一二三四五六七八九十\-_/（）()#号第]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LoosePackageCodeRegex = new(
        @"(?<![\u4e00-\u9fa5A-Za-z0-9])(?<value>包\s*[A-Za-z0-9一二三四五六七八九十]+|第\s*[A-Za-z0-9一二三四五六七八九十]+\s*包)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LeadingLotCodeRegex = new(
        @"^\s*(?<value>[A-Za-z0-9]{2,}(?:[-_/][A-Za-z0-9]{2,})+)\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LeadingOutcomeTableSupplierPrefixRegex = new(
        @"^\s*(?:[A-Za-z0-9]{2,}(?:[-_/][A-Za-z0-9]{2,})*\s+)?(?:包\s*[A-Za-z0-9一二三四五六七八九十]+|第\s*[A-Za-z0-9一二三四五六七八九十]+\s*包)\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LotCodeRegex = new(
        @"(?:标段号|标段编号|标号|LOT_NO)\s*[:：=]?\s*(?<value>[A-Za-z0-9一二三四五六七八九十\-_/（）()#号第]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AmountRegex = new(
        @"(?:中标金额|成交金额|中标价|成交价|投标报价|应答报价|评审价|评标价|报价金额|报价|金额|WIN_BID_AMOUNT|BID_AMOUNT)\s*[:：=]?\s*(?<amount>[0-9]+(?:,[0-9]{3})*(?:\.[0-9]+)?)(?![0-9.,])\s*(?<unit>万元|万|元)?(?!\s*[%％])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NumericAmountCandidateRegex = new(
        @"(?<![A-Za-z0-9])(?<amount>[0-9]{1,12}(?:,[0-9]{3})*(?:\.[0-9]+)?)(?![0-9.,])(?:\s*(?<unit>万元|万|元))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RankDigitRegex = new(
        @"第?\s*(?<rank>\d{1,2})\s*(?:名|中标候选人|成交候选人|候选人)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NamePrefixRegex = new(
        @"^(?:第?[一二三四五六七八九十\d]+(?:名|位)?|中标候选人|成交候选人|成交供应商|中标供应商|中标人|成交人|中标单位|成交单位|候选供应商|供应商名称|供应商|投标人|单位名称|名称)+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ProjectNameRegex = new(
        @"(?:项目名称|工程名称|采购项目名称|招标项目名称|PURPRJ_NAME)\s*[:：=]\s*(?<value>[^。；;\r\n]{2,200})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ProjectCodeRegex = new(
        @"(?:项目编号|招标编号|采购编号|批次编号|PURPRJ_CODE|BID_BATCH_CODE)\s*[:：=]\s*(?<value>[A-Za-z0-9\-_/（）()]{3,80})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] ExplicitSupplierLabels =
    [
        "中标人",
        "中标单位",
        "中标供应商",
        "成交人",
        "成交单位",
        "成交供应商",
        "中标候选人",
        "成交候选人",
        "候选供应商",
        "供应商名称",
        "投标人名称",
        "BIDDER_NAME",
        "SUPPLIER_NAME",
        "VENDOR_NAME",
        "WINNER_NAME"
    ];

    private static readonly string[] NonSupplierHints =
    [
        "招标人",
        "采购人",
        "代理机构",
        "招标代理",
        "采购代理",
        "发布单位",
        "公告发布",
        "联系人",
        "联系电话",
        "联系地址",
        "PUBLISH_ORG",
        "BID_ORG",
        "BID_AGT",
        "AGENCY"
    ];

    private static readonly string[] FormalOrganizationSuffixes =
    [
        "有限责任公司",
        "股份有限公司",
        "集团有限公司",
        "工程设计有限公司",
        "有限公司",
        "分公司"
    ];

    private static readonly string[] GenericOrganizationPrefixFragments =
    [
        "工程",
        "技术",
        "科技",
        "服务",
        "咨询",
        "电气",
        "研究院",
        "设计院",
        "规划院",
        "勘测院",
        "勘察院",
        "检测院",
        "检验院",
        "科学院",
        "计量院"
    ];

    public static IReadOnlyList<BidOpsOutcomeSupplierExtract> Extract(
        string? title,
        string? noticeType,
        string? text)
    {
        var source = $"{title}\n{text}";
        if (!LooksLikeOutcomeNotice(title, noticeType, source))
            return [];

        var results = new List<BidOpsOutcomeSupplierExtract>();
        var current = new PackageContext();
        var projectName = ExtractFirst(ProjectNameRegex, source);
        var projectCode = ExtractFirst(ProjectCodeRegex, source);
        var defaultOutcome = DetermineOutcomeType($"{title}\n{noticeType}");
        var outcomeTableContext = OutcomeTableContext.None;

        foreach (var rawLine in SplitCandidateLines(source))
        {
            var line = CleanLine(rawLine);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var linePackage = ExtractPackageContext(line);
            if (outcomeTableContext.Active)
                linePackage = linePackage.Merge(ExtractOutcomeTableRowPackageContext(line));

            if (!linePackage.IsEmpty)
                current = current.Merge(linePackage);

            if (LooksLikeOutcomeSupplierTableHeader(line))
            {
                outcomeTableContext = BuildOutcomeTableContext(line);
                continue;
            }

            if (outcomeTableContext.Active && LooksLikeOutcomeSupplierTableBoundary(line))
            {
                outcomeTableContext = OutcomeTableContext.None;
            }

            if (outcomeTableContext.Active)
            {
                var tableSupplierNames = ExtractSupplierNames(line);
                if (tableSupplierNames.Count > 0 &&
                    (!linePackage.IsEmpty ||
                     !string.IsNullOrWhiteSpace(current.PackageNo) ||
                     !string.IsNullOrWhiteSpace(current.LotNo)))
                {
                    var tableOutcomeType = DetermineOutcomeType(line);
                    if (tableOutcomeType == string.Empty)
                        tableOutcomeType = defaultOutcome;

                    foreach (var supplierName in tableSupplierNames)
                    {
                        results.Add(new BidOpsOutcomeSupplierExtract
                        {
                            SupplierName = supplierName,
                            OutcomeType = tableOutcomeType,
                            Rank = ExtractRank(line),
                            AwardAmount = ExtractAmount(line, supplierName, outcomeTableContext),
                            ProjectName = projectName,
                            ProjectCode = projectCode,
                            LotNo = current.LotNo,
                            LotName = current.LotName,
                            PackageNo = current.PackageNo,
                            PackageName = current.PackageName,
                            Category = current.Category,
                            EvidenceText = Truncate(line, 1000),
                            Confidence = 0.84m
                        });
                    }

                    continue;
                }
            }

            if (!HasSupplierContext(line))
                continue;

            if (LooksLikeAnnouncementIntro(line))
                continue;

            if (LooksLikeAnnouncementTitleOnly(line))
                continue;

            var supplierNames = ExtractSupplierNames(line);
            if (supplierNames.Count == 0)
                continue;

            var outcomeType = DetermineOutcomeType(line);
            if (outcomeType == string.Empty)
                outcomeType = defaultOutcome;

            foreach (var supplierName in supplierNames)
            {
                results.Add(new BidOpsOutcomeSupplierExtract
                {
                    SupplierName = supplierName,
                    OutcomeType = outcomeType,
                    Rank = ExtractRank(line),
                    AwardAmount = ExtractAmount(line, supplierName, OutcomeTableContext.None),
                    ProjectName = projectName,
                    ProjectCode = projectCode,
                    LotNo = current.LotNo,
                    LotName = current.LotName,
                    PackageNo = current.PackageNo,
                    PackageName = current.PackageName,
                    Category = current.Category,
                    EvidenceText = Truncate(line, 1000),
                    Confidence = HasKeyHint(line) ? 0.9m : 0.76m
                });
            }
        }

        return results
            .GroupBy(x => new
            {
                Supplier = NormalizeSupplierName(x.SupplierName),
                x.OutcomeType,
                x.Rank,
                Package = NormalizeCode(x.PackageNo),
                Lot = NormalizeCode(x.LotNo),
                Evidence = x.EvidenceText
            })
            .Select(x => x.First())
            .ToList();
    }

    public static bool LooksLikeOutcomeNotice(string? title, string? noticeType, string? text)
    {
        var signal = $"{title}\n{noticeType}\n{text}";
        return ContainsAny(
            signal,
            "AwardAnnouncement",
            "CandidateAnnouncement",
            "中标候选人",
            "成交候选人",
            "中标结果",
            "成交结果",
            "中标公告",
            "成交公告",
            "中标人",
            "成交供应商",
            "中选人",
            "入围供应商");
    }

    public static string NormalizeSupplierName(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        var chars = cleaned
            .Where(x => !char.IsWhiteSpace(x) && !"()（）[]【】{}<>《》,，.。;；:：-_—–/\\|".Contains(x))
            .ToArray();
        return new string(chars).ToUpperInvariant();
    }

    private static IEnumerable<string> SplitCandidateLines(string source)
    {
        return source
            .Replace('\t', ' ')
            .Split(['\r', '\n', '。', '；', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string CleanLine(string value)
    {
        return BidOpsTextQuality.CleanExtractedValue(value)
            .Replace("　", " ")
            .Trim();
    }

    private static bool HasSupplierContext(string line)
    {
        if (HasKeyHint(line))
            return true;

        var normalized = line.ToUpperInvariant();
        if (!ExplicitSupplierLabels.Any(label => normalized.Contains(label, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (HasDelimitedSupplierLabel(line))
            return !ContainsBlockingNonSupplierHint(normalized);

        if (Regex.IsMatch(
                line,
                @"^\s*(?:第?[一二三四五六七八九十\d]+(?:名|位)?\s*)?(?:中标候选人|成交候选人|成交供应商|中标供应商|中标人|成交人|中标单位|成交单位|候选供应商|供应商名称|投标人名称)\s+",
                RegexOptions.CultureInvariant))
        {
            return !ContainsBlockingNonSupplierHint(normalized);
        }

        return false;
    }

    private static bool HasKeyHint(string line)
    {
        var key = line.Split([':', '：', '='], 2)[0].ToUpperInvariant();
        return key.Contains("BIDDER_NAME", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("SUPPLIER_NAME", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("VENDOR_NAME", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("WINNER_NAME", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeAnnouncementTitleOnly(string line)
    {
        if (!ContainsAny(line, "公告", "公示"))
            return false;

        if (HasKeyHint(line))
            return false;

        return !HasDelimitedSupplierLabel(line);
    }

    private static bool LooksLikeAnnouncementIntro(string line)
    {
        if (HasKeyHint(line))
            return false;

        if (HasDelimitedSupplierLabel(line))
            return false;

        return ContainsAny(
            line,
            "评审工作",
            "评标工作",
            "采购活动",
            "推荐的成交候选人",
            "推荐的中标候选人",
            "现将",
            "予以公示",
            "公示如下",
            "公告如下",
            "已经结束");
    }

    private static bool HasDelimitedSupplierLabel(string line)
    {
        return Regex.IsMatch(
            line,
            @"(?:中标人|成交人|中标供应商|成交供应商|中标候选人|成交候选人|供应商名称|投标人名称|BIDDER_NAME|SUPPLIER_NAME|VENDOR_NAME|WINNER_NAME)\s*[:：=]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsBlockingNonSupplierHint(string normalizedLine)
    {
        if (NonSupplierHints.Any(hint => normalizedLine.Contains(hint, StringComparison.OrdinalIgnoreCase)))
            return true;

        return ContainsAny(
            normalizedLine,
            "须知",
            "电子商务平台",
            "请各中标人",
            "请各成交人",
            "邮寄",
            "公司全称",
            "开户银行",
            "账号",
            "保证金",
            "服务费",
            "代理费");
    }

    private static bool LooksLikeOutcomeSupplierTableHeader(string line)
    {
        if (ContainsBlockingNonSupplierHint(line.ToUpperInvariant()))
            return false;

        if (CompanyRegex.IsMatch(line))
            return false;

        var normalized = NormalizeHeaderText(line);
        var hasSupplierColumn = ContainsAny(
            normalized,
            "成交人",
            "中标人",
            "成交供应商",
            "中标供应商",
            "成交候选人",
            "中标候选人",
            "供应商名称",
            "投标人名称");
        if (!hasSupplierColumn)
            return false;

        return ContainsAny(
            normalized,
            "包号",
            "包件号",
            "分包号",
            "标包号",
            "分标编号",
            "标段编号",
            "分标",
            "标段",
            "项目名称",
            "项目编号");
    }

    private static bool LooksLikeOutcomeSupplierTableBoundary(string line)
    {
        if (HasKeyHint(line))
            return false;

        var normalized = NormalizeHeaderText(line);
        return normalized.StartsWith("采购人", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("招标人", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("代理机构", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("招标代理", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("采购代理", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("联系人", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("联系电话", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("联系地址", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("公示期", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("公告日期", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("发布日期", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(normalized, @"^20\d{2}年\d{1,2}月\d{1,2}日$", RegexOptions.CultureInvariant);
    }

    private static OutcomeTableContext BuildOutcomeTableContext(string line)
    {
        return new OutcomeTableContext(
            Active: true,
            HasAmountColumn: LooksLikeAmountHeader(line),
            AmountUnit: DetermineAmountUnitHint(line));
    }

    private static bool LooksLikeAmountHeader(string line)
    {
        var normalized = NormalizeHeaderText(line);
        return ContainsAny(
            normalized,
            "中标金额",
            "成交金额",
            "中标价",
            "成交价",
            "投标报价",
            "应答报价",
            "评审价",
            "评标价",
            "报价金额",
            "金额",
            "报价",
            "价格",
            "winbidamount",
            "bidamount");
    }

    private static AmountUnitHint DetermineAmountUnitHint(string line)
    {
        var normalized = NormalizeHeaderText(line);
        if (ContainsAny(normalized, "万元", "人民币万元"))
            return AmountUnitHint.TenThousandYuan;

        if (ContainsAny(normalized, "元", "人民币元"))
            return AmountUnitHint.Yuan;

        return AmountUnitHint.Unknown;
    }

    private static List<string> ExtractSupplierNames(string line)
    {
        return CompanyRegex.Matches(line)
            .Where(x => !ContinuesInsideWord(line, x.Index + x.Length))
            .Select(x => CleanSupplierName(x.Groups["name"].Value))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !LooksLikeNonSupplierName(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string CleanSupplierName(string value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        cleaned = LeadingOutcomeTableSupplierPrefixRegex.Replace(cleaned, string.Empty);
        cleaned = NamePrefixRegex.Replace(cleaned, string.Empty);
        cleaned = LeadingOutcomeTableSupplierPrefixRegex.Replace(cleaned, string.Empty);
        cleaned = TrimLeadingTableDescription(cleaned);
        cleaned = cleaned.Trim(' ', ':', '：', '=', '-', '、', ',', '，', '.', '。', '；', ';');
        if (cleaned.Length > 300)
            cleaned = cleaned[..300];
        return cleaned;
    }

    private static string TrimLeadingTableDescription(string value)
    {
        var tokens = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim(' ', ':', '：', '=', '-', '、', ',', '，', '.', '。', '；', ';'))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        if (tokens.Count <= 1)
            return value;

        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            if (LooksLikeStandaloneSupplierName(tokens[i]))
                return tokens[i];
        }

        return string.Empty;
    }

    private static bool LooksLikeStandaloneSupplierName(string value)
    {
        var match = CompanyRegex.Match(value);
        return match.Success &&
               match.Index == 0 &&
               match.Length == value.Length &&
               !LooksLikeNonSupplierName(value);
    }

    private static bool ContinuesInsideWord(string source, int endIndex)
    {
        if (endIndex < 0 || endIndex >= source.Length)
            return false;

        var next = source[endIndex];
        return char.IsLetterOrDigit(next) || (next >= '\u4e00' && next <= '\u9fff');
    }

    private static bool LooksLikeNonSupplierName(string name)
    {
        if (name.Length < 4)
            return true;

        if (char.IsDigit(name[0]))
            return true;

        if (HasUnbalancedParentheses(name))
            return true;

        if (LooksLikeTruncatedOrganizationSuffix(name))
            return true;

        if (name.Contains("供电公司", StringComparison.Ordinal))
            return true;

        if (name.EndsWith("公司", StringComparison.Ordinal) &&
            name.Length <= 8 &&
            !ContainsAny(name, "有限公司", "股份", "集团", "分公司"))
        {
            return true;
        }

        return ContainsAny(
            name,
            "招标代理",
            "采购代理",
            "招标有限公司招标",
            "电力公司物资部",
            "有限公司招标");
    }

    private static bool LooksLikeTruncatedOrganizationSuffix(string name)
    {
        var compact = new string(name
            .Where(x => !char.IsWhiteSpace(x) && !"()（）[]【】{}<>《》,，.。;；:：-_—–/\\|".Contains(x))
            .ToArray());
        if (compact.Length == 0)
            return true;

        foreach (var suffix in FormalOrganizationSuffixes)
        {
            if (!compact.EndsWith(suffix, StringComparison.Ordinal))
                continue;

            var prefix = compact[..^suffix.Length];
            return prefix.Length < 3 ||
                   GenericOrganizationPrefixFragments.Contains(prefix, StringComparer.Ordinal);
        }

        return false;
    }

    private static bool HasUnbalancedParentheses(string name)
    {
        return name.Count(x => x == '(') != name.Count(x => x == ')') ||
               name.Count(x => x == '（') != name.Count(x => x == '）');
    }

    private static PackageContext ExtractPackageContext(string line)
    {
        var packageNo = ExtractFirst(PackageCodeRegex, line);
        if (string.IsNullOrWhiteSpace(packageNo))
            packageNo = ExtractFirst(LoosePackageCodeRegex, line);

        var lotNo = ExtractFirst(LotCodeRegex, line);
        var packageName = ExtractNamedValue(line, "包件名称", "包名称", "标包名称", "分包名称", "项目包名称", "PACKAGE_NAME");
        var lotName = ExtractNamedValue(line, "标段名称", "LOT_NAME");
        var category = ExtractNamedValue(line, "物资类别", "品类", "分类", "CATEGORY");

        return new PackageContext(
            Truncate(BidOpsTextQuality.CleanExtractedValue(lotNo), 128),
            Truncate(BidOpsTextQuality.CleanExtractedValue(lotName), 300),
            Truncate(BidOpsTextQuality.CleanExtractedValue(packageNo), 128),
            Truncate(BidOpsTextQuality.CleanExtractedValue(packageName), 500),
            Truncate(BidOpsTextQuality.CleanExtractedValue(category), 128));
    }

    private static PackageContext ExtractOutcomeTableRowPackageContext(string line)
    {
        var lotNo = ExtractFirst(LeadingLotCodeRegex, line);
        if (string.IsNullOrWhiteSpace(lotNo))
            return new PackageContext();

        return new PackageContext(
            Truncate(BidOpsTextQuality.CleanExtractedValue(lotNo), 128),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private static string ExtractNamedValue(string line, params string[] names)
    {
        foreach (var name in names)
        {
            var pattern = $@"{Regex.Escape(name)}\s*[:：=]\s*(?<value>[^,，;；。|\r\n]{{1,160}})";
            var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
                return match.Groups["value"].Value;
        }

        return string.Empty;
    }

    private static string ExtractFirst(Regex regex, string source)
    {
        var match = regex.Match(source);
        return match.Success ? BidOpsTextQuality.CleanExtractedValue(match.Groups["value"].Value) : string.Empty;
    }

    private static string DetermineOutcomeType(string value)
    {
        if (ContainsAny(value, "入围", "Shortlisted"))
            return BidOpsOutcomeTypes.Shortlisted;

        if (ContainsAny(value, "候选", "CandidateAnnouncement"))
            return BidOpsOutcomeTypes.Candidate;

        if (ContainsAny(value, "中标人", "成交供应商", "中标结果", "成交结果", "中标公告", "成交公告", "AwardAnnouncement"))
            return BidOpsOutcomeTypes.Awarded;

        return string.Empty;
    }

    private static int? ExtractRank(string line)
    {
        if (ContainsAny(line, "第一", "第1", "排名1"))
            return 1;
        if (ContainsAny(line, "第二", "第2", "排名2"))
            return 2;
        if (ContainsAny(line, "第三", "第3", "排名3"))
            return 3;

        var match = RankDigitRegex.Match(line);
        if (!match.Success)
            return null;

        return int.TryParse(match.Groups["rank"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)
            ? rank
            : null;
    }

    private static decimal? ExtractAmount(
        string line,
        string? supplierName,
        OutcomeTableContext tableContext)
    {
        var match = AmountRegex.Match(line);
        if (!match.Success)
            return ExtractTableAmount(line, supplierName, tableContext);

        return ParseAmount(match.Groups["amount"].Value, match.Groups["unit"].Value, AmountUnitHint.Yuan);
    }

    private static decimal? ExtractTableAmount(
        string line,
        string? supplierName,
        OutcomeTableContext tableContext)
    {
        if (!tableContext.Active || !tableContext.HasAmountColumn)
            return null;

        var source = line;
        if (!string.IsNullOrWhiteSpace(supplierName))
        {
            var supplierIndex = line.IndexOf(supplierName, StringComparison.Ordinal);
            if (supplierIndex >= 0)
                source = line[(supplierIndex + supplierName.Length)..];
        }

        decimal? firstContextAmount = null;
        var firstNumericWasRateOrScore = false;
        var seenNumericCandidate = false;
        foreach (Match candidate in NumericAmountCandidateRegex.Matches(source))
        {
            if (LooksLikePercentOrScore(source, candidate.Index, candidate.Length))
            {
                if (!seenNumericCandidate)
                    firstNumericWasRateOrScore = true;

                seenNumericCandidate = true;
                continue;
            }

            seenNumericCandidate = true;

            var unit = candidate.Groups["unit"].Value;
            if (!string.IsNullOrWhiteSpace(unit))
                return ParseAmount(candidate.Groups["amount"].Value, unit, tableContext.AmountUnit);

            if (tableContext.AmountUnit == AmountUnitHint.Unknown)
                continue;

            firstContextAmount ??= ParseAmount(candidate.Groups["amount"].Value, string.Empty, tableContext.AmountUnit);
        }

        return firstNumericWasRateOrScore ? null : firstContextAmount;
    }

    private static bool LooksLikePercentOrScore(string source, int index, int length)
    {
        var nextIndex = index + length;
        while (nextIndex < source.Length && char.IsWhiteSpace(source[nextIndex]))
        {
            nextIndex++;
        }

        if (nextIndex >= source.Length)
            return false;

        return source[nextIndex] is '%' or '％' or '分';
    }

    private static decimal? ParseAmount(
        string amountText,
        string unit,
        AmountUnitHint fallbackUnit)
    {
        if (!decimal.TryParse(amountText.Replace(",", string.Empty, StringComparison.Ordinal), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return null;

        if (unit is "万元" or "万")
            amount *= 10000m;
        else if (string.IsNullOrWhiteSpace(unit) && fallbackUnit == AmountUnitHint.TenThousandYuan)
            amount *= 10000m;

        return Math.Round(amount, 2);
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeCode(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        return new string(cleaned
            .Where(x => !char.IsWhiteSpace(x) && !":：,，;；".Contains(x))
            .ToArray())
            .ToUpperInvariant();
    }

    private static string NormalizeHeaderText(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch) ||
                ch is '/' or '／' or '|' or ':' or '：' or '-' or '_' or '（' or '）' or '(' or ')' or ',' or '，')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    private static string Truncate(string? value, int maxLength)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private sealed record PackageContext(
        string LotNo = "",
        string LotName = "",
        string PackageNo = "",
        string PackageName = "",
        string Category = "")
    {
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(LotNo) &&
            string.IsNullOrWhiteSpace(LotName) &&
            string.IsNullOrWhiteSpace(PackageNo) &&
            string.IsNullOrWhiteSpace(PackageName) &&
            string.IsNullOrWhiteSpace(Category);

        public PackageContext Merge(PackageContext other)
        {
            return new PackageContext(
                First(other.LotNo, LotNo),
                First(other.LotName, LotName),
                First(other.PackageNo, PackageNo),
                First(other.PackageName, PackageName),
                First(other.Category, Category));
        }

        private static string First(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }

    private enum AmountUnitHint
    {
        Unknown,
        Yuan,
        TenThousandYuan
    }

    private sealed record OutcomeTableContext(
        bool Active,
        bool HasAmountColumn,
        AmountUnitHint AmountUnit)
    {
        public static OutcomeTableContext None { get; } = new(false, false, AmountUnitHint.Unknown);
    }
}
