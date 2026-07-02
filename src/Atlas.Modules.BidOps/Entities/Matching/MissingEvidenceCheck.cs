using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Matching;

/// <summary>
/// 供应商匹配缺失证据检查结果。
/// </summary>
public sealed class MissingEvidenceCheck : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的运行记录主键。
    /// </summary>
    public long RunId { get; set; }

    /// <summary>
    /// 关联的匹配结果主键。
    /// </summary>
    public long ResultId { get; set; }

    /// <summary>
    /// 关联的包件主键。
    /// </summary>
    public long PackageId { get; set; }

    /// <summary>
    /// 关联的供应商主键。
    /// </summary>
    public long SupplierId { get; set; }

    /// <summary>
    /// 关联的要求条目主键。
    /// </summary>
    public long? RequirementId { get; set; }

    /// <summary>
    /// 匹配到的供应商证明材料主键。
    /// </summary>
    public long? MatchedEvidenceDocumentId { get; set; }

    /// <summary>
    /// 要求提交或匹配的证明材料类型。
    /// </summary>
    public string RequiredEvidenceType { get; set; } = string.Empty;

    /// <summary>
    /// 要求原文。
    /// </summary>
    public string RequirementText { get; set; } = string.Empty;

    /// <summary>
    /// 记录状态。
    /// </summary>
    public string Status { get; set; } = BidOpsMissingEvidenceStatuses.Missing;

    /// <summary>
    /// 规则或 AI 给出的解释。
    /// </summary>
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>
/// 缺失证据状态枚举值。
/// </summary>
public static class BidOpsMissingEvidenceStatuses
{
    /// <summary>
    /// 缺失证明材料。
    /// </summary>
    public const string Missing = "Missing";
    /// <summary>
    /// 证明材料已过期。
    /// </summary>
    public const string Expired = "Expired";
    /// <summary>
    /// 证明材料即将过期。
    /// </summary>
    public const string ExpiringSoon = "ExpiringSoon";
}
