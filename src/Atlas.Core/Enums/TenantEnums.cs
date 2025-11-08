using System.ComponentModel;

namespace Atlas.Core.Enums;

/// <summary>
/// 租户状态
/// </summary>
public enum TenantStatus
{
    /// <summary>
    /// 未激活
    /// </summary>
    Inactive = 0,

    /// <summary>
    /// 激活
    /// </summary>
    Active = 1,

    /// <summary>
    /// 试用中
    /// </summary>
    Trial = 2,

    /// <summary>
    /// 已过期
    /// </summary>
    Expired = 3,

    /// <summary>
    /// 已停用
    /// </summary>
    Suspended = 4
}

/// <summary>
/// 租户类型
/// </summary>
public enum TenantType : byte
{
    /// <summary>
    /// 公司
    /// </summary>
    Company = 0,

    /// <summary>
    /// 个人
    /// </summary>
    Person
}

/// <summary>
/// 连锁类型
/// </summary>
public enum BusinessType : byte
{
    /// <summary>
    /// 加盟连锁
    /// </summary>
    [Description("加盟")]
    FranchiseChain = 0,

    /// <summary>
    /// 直营连锁
    /// </summary>
    [Description("直营")]
    RegularChain
}