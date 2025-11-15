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
    /// 企业版
    /// </summary>
    Enterprise = 1,

    /// <summary>
    /// 个人版（Mobile版）
    /// </summary>
    Individual = 2
}

/// <summary>
/// 连锁类型
/// </summary>
public enum BusinessType : byte
{
    /// <summary>
    /// 单店
    /// </summary>
    Single = 1,

    /// <summary>
    /// 连锁直营
    /// </summary>
    Chain = 2,

    /// <summary>
    /// 连锁加盟
    /// </summary>
    Franchise = 3
}
public enum StoreType
{
    /// <summary>
    /// 平台总部
    /// </summary>
    Headquarters = 0,

    /// <summary>
    /// 加盟商总部
    /// </summary>
    FranchiseHeadquarters = 1,

    /// <summary>
    /// 直营门店
    /// </summary>
    DirectOperated = 2,

    /// <summary>
    /// 加盟门店
    /// </summary>
    Franchised = 3
}



/// <summary>
/// 门店状态
/// </summary>
public enum StoreStatus
{
    /// <summary>
    /// 激活/营业中
    /// </summary>
    Active = 1,

    /// <summary>
    /// 停用/关闭
    /// </summary>
    Inactive = 2,

    /// <summary>
    /// 暂停营业
    /// </summary>
    Suspended = 3,

    /// <summary>
    /// 装修中
    /// </summary>
    UnderRenovation = 4,

    /// <summary>
    /// 筹备中
    /// </summary>
    Preparing = 5
}
public enum OrderStatus
{
    Pending = 0,
    Comfirm = 1,
    Shipped = 2,
}
public enum PaymentMethod
{
    Cash = 0,
    CreditCard = 1,
    MobilePayment = 2,
}