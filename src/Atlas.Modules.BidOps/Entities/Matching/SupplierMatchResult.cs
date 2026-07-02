using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Matching;

/// <summary>
/// 供应商与包件匹配结果。
/// </summary>
public sealed class SupplierMatchResult : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的运行记录主键。
    /// </summary>
    public long RunId { get; set; }

    /// <summary>
    /// 关联的包件主键。
    /// </summary>
    public long PackageId { get; set; }

    /// <summary>
    /// 关联的供应商主键。
    /// </summary>
    public long SupplierId { get; set; }

    /// <summary>
    /// 匹配时的供应商名称快照。
    /// </summary>
    public string SupplierNameSnapshot { get; set; } = string.Empty;

    /// <summary>
    /// 排序名次或中标候选人排名。
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// 匹配评分。
    /// </summary>
    public decimal Score { get; set; }

    /// <summary>
    /// 供应商匹配等级。
    /// </summary>
    public string MatchLevel { get; set; } = BidOpsSupplierMatchLevels.Low;

    /// <summary>
    /// 匹配建议。
    /// </summary>
    public string Recommendation { get; set; } = BidOpsSupplierMatchRecommendations.NotRecommended;

    /// <summary>
    /// 供应商品类能力是否匹配包件要求。
    /// </summary>
    public bool CategoryMatched { get; set; }

    /// <summary>
    /// 供应商服务区域是否匹配包件地区要求。
    /// </summary>
    public bool RegionMatched { get; set; }

    /// <summary>
    /// 已匹配证明材料数量。
    /// </summary>
    public int EvidenceMatchedCount { get; set; }

    /// <summary>
    /// 缺失证明材料数量。
    /// </summary>
    public int MissingEvidenceCount { get; set; }

    /// <summary>
    /// 风险标记集合。
    /// </summary>
    public string RiskFlags { get; set; } = string.Empty;

    /// <summary>
    /// 规则或 AI 给出的解释。
    /// </summary>
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>
/// 供应商匹配等级枚举值。
/// </summary>
public static class BidOpsSupplierMatchLevels
{
    /// <summary>
    /// 高等级。
    /// </summary>
    public const string High = "High";
    /// <summary>
    /// 中等级。
    /// </summary>
    public const string Medium = "Medium";
    /// <summary>
    /// 低等级。
    /// </summary>
    public const string Low = "Low";
}

/// <summary>
/// 供应商匹配建议枚举值。
/// </summary>
public static class BidOpsSupplierMatchRecommendations
{
    /// <summary>
    /// 候选状态或候选建议。
    /// </summary>
    public const string Candidate = "Candidate";
    /// <summary>
    /// 谨慎推进建议。
    /// </summary>
    public const string Caution = "Caution";
    /// <summary>
    /// 不推荐。
    /// </summary>
    public const string NotRecommended = "NotRecommended";
}
