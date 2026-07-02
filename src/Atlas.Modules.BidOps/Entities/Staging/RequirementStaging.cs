using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Staging;

/// <summary>
/// 资质/响应要求暂存记录。
/// </summary>
public sealed class RequirementStaging : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的包件暂存记录主键。
    /// </summary>
    public long PackageStagingId { get; set; }

    /// <summary>
    /// 要求类型。
    /// </summary>
    public string RequirementType { get; set; } = string.Empty;

    /// <summary>
    /// 原始要求文本。
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// 关联的来源文件主键。
    /// </summary>
    public long? SourceFileId { get; set; }

    /// <summary>
    /// 来源文件页码。
    /// </summary>
    public int? SourcePage { get; set; }

    /// <summary>
    /// 是否为必须满足的要求。
    /// </summary>
    public bool IsMandatory { get; set; }

    /// <summary>
    /// 是否可能构成否决投标风险。
    /// </summary>
    public bool IsRejectRisk { get; set; }

    /// <summary>
    /// 要求提交或匹配的证明材料类型。
    /// </summary>
    public string RequiredEvidenceType { get; set; } = string.Empty;

    /// <summary>
    /// 风险等级。
    /// </summary>
    public string RiskLevel { get; set; } = "Medium";

    /// <summary>
    /// AI 给出的解析说明。
    /// </summary>
    public string AiExplanation { get; set; } = string.Empty;

    /// <summary>
    /// AI 解析置信度。
    /// </summary>
    public decimal AiConfidence { get; set; }

    /// <summary>
    /// 人工审核状态。
    /// </summary>
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;
}
