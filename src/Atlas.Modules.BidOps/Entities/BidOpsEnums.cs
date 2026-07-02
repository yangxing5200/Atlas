namespace Atlas.Modules.BidOps.Entities;

/// <summary>
/// 原始公告处理状态。
/// </summary>
public enum RawNoticeStatus
{
    /// <summary>
    /// 新采集，尚未进入解析队列。
    /// </summary>
    New = 0,
    /// <summary>
    /// 已进入解析队列。
    /// </summary>
    ParseQueued = 1,
    /// <summary>
    /// 解析完成，等待人工审核。
    /// </summary>
    ReviewPending = 2,
    /// <summary>
    /// 已审核通过并可导入正式业务库。
    /// </summary>
    Approved = 3,
    /// <summary>
    /// 已人工忽略。
    /// </summary>
    Ignored = 4,
    /// <summary>
    /// 处理失败。
    /// </summary>
    Failed = 5
}

/// <summary>
/// 暂存数据人工审核状态。
/// </summary>
public enum ReviewStatus
{
    /// <summary>
    /// 待审核。
    /// </summary>
    Pending = 0,
    /// <summary>
    /// 审核中。
    /// </summary>
    InReview = 1,
    /// <summary>
    /// 审核通过。
    /// </summary>
    Approved = 2,
    /// <summary>
    /// 审核忽略。
    /// </summary>
    Ignored = 3,
    /// <summary>
    /// 需要重新解析。
    /// </summary>
    ReparseRequired = 4
}

/// <summary>
/// 审核任务状态。
/// </summary>
public enum ReviewTaskStatus
{
    /// <summary>
    /// 待审核。
    /// </summary>
    Pending = 0,
    /// <summary>
    /// 审核中。
    /// </summary>
    InReview = 1,
    /// <summary>
    /// 审核通过。
    /// </summary>
    Approved = 2,
    /// <summary>
    /// 已忽略。
    /// </summary>
    Ignored = 3,
    /// <summary>
    /// 已合并到其他审核任务。
    /// </summary>
    Merged = 4,
    /// <summary>
    /// 需要重新解析。
    /// </summary>
    ReparseRequired = 5
}

/// <summary>
/// 审核质量风险等级。
/// </summary>
public enum ReviewQualityRiskLevel
{
    /// <summary>
    /// 低风险。
    /// </summary>
    Low = 0,
    /// <summary>
    /// 中风险。
    /// </summary>
    Medium = 1,
    /// <summary>
    /// 高风险。
    /// </summary>
    High = 2
}

/// <summary>
/// 审核建议类型。
/// </summary>
public enum ReviewRecommendation
{
    /// <summary>
    /// 建议批量确认候选数据。
    /// </summary>
    BatchConfirmCandidate = 0,
    /// <summary>
    /// 建议人工逐项复核。
    /// </summary>
    NeedsReview = 1,
    /// <summary>
    /// 建议重新解析。
    /// </summary>
    NeedsReparse = 2
}

/// <summary>
/// 附件下载状态。
/// </summary>
public enum DownloadStatus
{
    /// <summary>
    /// 等待下载。
    /// </summary>
    Pending = 0,
    /// <summary>
    /// 下载成功。
    /// </summary>
    Succeeded = 1,
    /// <summary>
    /// 下载失败。
    /// </summary>
    Failed = 2,
    /// <summary>
    /// 跳过下载。
    /// </summary>
    Skipped = 3
}

/// <summary>
/// 附件文本抽取状态。
/// </summary>
public enum TextExtractStatus
{
    /// <summary>
    /// 等待文本抽取。
    /// </summary>
    Pending = 0,
    /// <summary>
    /// 文本抽取成功。
    /// </summary>
    Succeeded = 1,
    /// <summary>
    /// 文本抽取失败。
    /// </summary>
    Failed = 2,
    /// <summary>
    /// 跳过文本抽取。
    /// </summary>
    Skipped = 3
}
