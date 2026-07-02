namespace Atlas.Modules.BidOps.Services;

public static class BidOpsSourceNoticeTypes
{
    public const string TenderNotice = "tender_notice";
    public const string BidInvitation = "bid_invitation";
    public const string ProcurementNotice = "procurement_notice";
    public const string ProcurementInvitation = "procurement_invitation";
    public const string PrequalificationNotice = "prequalification_notice";
    public const string ChangeNotice = "change_notice";
    public const string Unknown = "unknown";
}

public static class BidOpsProjectProcessTypes
{
    public const string Bidding = "bidding";
    public const string NonBidding = "non_bidding";
    public const string Prequalification = "prequalification";
    public const string Unknown = "unknown";
}

public sealed record BidOpsSourceNoticeClassification(
    string ProjectProcessType,
    string ProcurementMethod,
    IReadOnlyList<string> PreferredSourceNoticeTypes,
    decimal Confidence,
    string Reason);

public static class BidOpsSourceNoticeClassifier
{
    private static readonly string[] BiddingPreferred =
    [
        BidOpsSourceNoticeTypes.TenderNotice,
        BidOpsSourceNoticeTypes.BidInvitation,
        BidOpsSourceNoticeTypes.ProcurementNotice,
        BidOpsSourceNoticeTypes.ProcurementInvitation
    ];

    private static readonly string[] InvitationBiddingPreferred =
    [
        BidOpsSourceNoticeTypes.BidInvitation,
        BidOpsSourceNoticeTypes.TenderNotice,
        BidOpsSourceNoticeTypes.ProcurementNotice,
        BidOpsSourceNoticeTypes.ProcurementInvitation
    ];

    private static readonly string[] NonBiddingPreferred =
    [
        BidOpsSourceNoticeTypes.ProcurementNotice,
        BidOpsSourceNoticeTypes.ProcurementInvitation,
        BidOpsSourceNoticeTypes.TenderNotice,
        BidOpsSourceNoticeTypes.BidInvitation
    ];

    public static BidOpsSourceNoticeClassification Classify(
        string? title,
        string? text,
        string? column = null,
        string? projectCode = null)
    {
        var signal = string.Join(' ', title, column, projectCode, text);

        if (ContainsAny(signal, "资格预审公告"))
        {
            return new BidOpsSourceNoticeClassification(
                BidOpsProjectProcessTypes.Prequalification,
                string.Empty,
                [BidOpsSourceNoticeTypes.PrequalificationNotice, .. BiddingPreferred],
                0.9m,
                "标题或正文包含资格预审公告。");
        }

        if (ContainsAny(signal, "邀请招标"))
        {
            return new BidOpsSourceNoticeClassification(
                BidOpsProjectProcessTypes.Bidding,
                "邀请招标",
                InvitationBiddingPreferred,
                0.95m,
                "标题或正文包含邀请招标，优先匹配投标邀请书。");
        }

        if (ContainsAny(signal, "公开招标"))
        {
            return new BidOpsSourceNoticeClassification(
                BidOpsProjectProcessTypes.Bidding,
                "公开招标",
                BiddingPreferred,
                0.95m,
                "标题或正文包含公开招标，优先匹配招标公告。");
        }

        if (ContainsAny(signal, "竞争性谈判", "公开谈判", "询价", "单一来源", "非招标"))
        {
            return new BidOpsSourceNoticeClassification(
                BidOpsProjectProcessTypes.NonBidding,
                ResolveNonBiddingMethod(signal),
                NonBiddingPreferred,
                0.9m,
                "标题或正文包含非招标采购方式关键词。");
        }

        if (ContainsAny(signal, "成交结果公告", "成交公告", "采购文件", "应答人", "应答文件"))
        {
            return new BidOpsSourceNoticeClassification(
                BidOpsProjectProcessTypes.NonBidding,
                ResolveNonBiddingMethod(signal),
                NonBiddingPreferred,
                0.82m,
                "标题或正文包含成交/应答类非招标关键词。");
        }

        if (ContainsAny(signal, "中标结果公告", "中标候选人公示", "中标公告", "招标采购", "招标编号", "投标人", "招标文件"))
        {
            var confidence = ContainsAny(signal, "招标编号") ? 0.9m : 0.85m;
            return new BidOpsSourceNoticeClassification(
                BidOpsProjectProcessTypes.Bidding,
                string.Empty,
                BiddingPreferred,
                confidence,
                "标题或正文包含中标/投标/招标编号等招标流程关键词。");
        }

        return new BidOpsSourceNoticeClassification(
            BidOpsProjectProcessTypes.Unknown,
            string.Empty,
            BiddingPreferred,
            0.5m,
            "未识别到明确流程类型，保守优先检索招标公告和投标邀请书。");
    }

    public static string GetDisplayColumn(string sourceNoticeType)
    {
        return sourceNoticeType switch
        {
            BidOpsSourceNoticeTypes.TenderNotice => "招标公告",
            BidOpsSourceNoticeTypes.BidInvitation => "投标邀请书",
            BidOpsSourceNoticeTypes.ProcurementNotice => "前置公告",
            BidOpsSourceNoticeTypes.ProcurementInvitation => "采购邀请",
            BidOpsSourceNoticeTypes.PrequalificationNotice => "资格预审公告",
            BidOpsSourceNoticeTypes.ChangeNotice => "变更公告",
            _ => "未知"
        };
    }

    public static int PreferredIndex(
        BidOpsSourceNoticeClassification classification,
        string sourceNoticeType)
    {
        var index = classification.PreferredSourceNoticeTypes
            .Select((value, order) => new { value, order })
            .FirstOrDefault(x => string.Equals(x.value, sourceNoticeType, StringComparison.OrdinalIgnoreCase))
            ?.order;

        return index ?? int.MaxValue;
    }

    private static string ResolveNonBiddingMethod(string signal)
    {
        if (ContainsAny(signal, "竞争性谈判"))
            return "竞争性谈判";
        if (ContainsAny(signal, "公开谈判"))
            return "公开谈判";
        if (ContainsAny(signal, "询价"))
            return "询价";
        if (ContainsAny(signal, "单一来源"))
            return "单一来源";
        return string.Empty;
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}
