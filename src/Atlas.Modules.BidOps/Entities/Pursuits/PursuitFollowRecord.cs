using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Pursuits;

/// <summary>
/// 投标作业跟进记录。
/// </summary>
public sealed class PursuitFollowRecord : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的投标作业主键。
    /// </summary>
    public long PursuitId { get; set; }

    /// <summary>
    /// 跟进记录类型。
    /// </summary>
    public string FollowType { get; set; } = BidOpsPursuitFollowTypes.Note;

    /// <summary>
    /// 跟进内容。
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 下一步跟进时间（UTC）。
    /// </summary>
    public DateTime? NextActionAtUtc { get; set; }

    /// <summary>
    /// 创建跟进记录的用户主键。
    /// </summary>
    public long? CreatedByUserId { get; set; }

    /// <summary>
    /// 创建跟进记录的用户名。
    /// </summary>
    public string CreatedByUserName { get; set; } = string.Empty;
}

/// <summary>
/// 投标作业跟进类型枚举值。
/// </summary>
public static class BidOpsPursuitFollowTypes
{
    /// <summary>
    /// 普通记录。
    /// </summary>
    public const string Note = "Note";
    /// <summary>
    /// 电话沟通。
    /// </summary>
    public const string Call = "Call";
    /// <summary>
    /// 会议沟通。
    /// </summary>
    public const string Meeting = "Meeting";
    /// <summary>
    /// 状态变更记录。
    /// </summary>
    public const string StatusChange = "StatusChange";
    /// <summary>
    /// 风险记录。
    /// </summary>
    public const string Risk = "Risk";
    /// <summary>
    /// 其他类型。
    /// </summary>
    public const string Other = "Other";
}
